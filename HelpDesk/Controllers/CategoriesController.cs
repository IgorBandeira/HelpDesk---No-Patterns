using HelpDesk.Data;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace HelpDesk.Controllers
{
    /// <summary>
    /// 🗂️ Categorias de Tickets
    /// </summary>
    /// <remarks>
    /// **Para que serve**  
    /// Entidade responsável por organizar tickets por categorias com no máximo dois níveis (Categoria → Subcategoria).
    ///
    /// **Regras gerais**
    /// - Apenas **Managers** podem **criar** e **deletar** categorias.
    /// - Nomes possuem limite de **180 caracteres** e devem ser **únicos** (case-sensitive no banco).
    /// - Se informado um **ParentId**, o pai **não** pode ser subcategoria (garante apenas dois níveis).
    /// - Não é permitido deletar categoria com **subcategorias** ou com **tickets ativos** (Status ≠ Fechado/Cancelado).
    /// </remarks>
    [ApiController]
    [Route("api/categories")]
    [Tags("Categories")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CategoriesController(AppDbContext db) => _db = db;

        private async Task<UserModel?> GetUserFromHeaderAsync(int userId)
        {
            return await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private static bool IsManager(UserModel u) => string.Equals(u.Role, "Manager", StringComparison.OrdinalIgnoreCase);

        private const int MaxCategoryNameLength = 180;

        /// <summary>Cria uma categoria (opcionalmente como subcategoria)</summary>
        /// <remarks>
        /// **Caso de uso**: Cadastrar uma nova categoria para classificar tickets.
        ///
        /// **Regras**
        /// - Apenas **Manager** pode criar.
        /// - **Name** é obrigatório, **trimado** e **≤ 180** caracteres.
        /// - **Name** deve ser **único**.
        /// - Se **ParentId** informado:
        ///   - O pai deve **existir**.
        ///   - O pai **não** pode ter **ParentId** (ou seja, o pai não pode ser subcategoria).
        ///
        /// **Responses**
        /// - 201: Categoria criada (retorna <c>CategoryItemDto</c>)
        /// - 400: Nome ausente/maior que o limite, pai inexistente
        /// - 401: Usuário inválido/não informado
        /// - 403: Usuário não é Manager
        /// - 409: Nome já existente / nível hierárquico inválido
        /// </remarks>
        [HttpPost]
        [SwaggerOperation(Summary = "Criar categoria", Description = "Cria uma nova categoria ou subcategoria (somente Managers).")]
        [ProducesResponseType(typeof(CategoryItemDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CategoryItemDto>> Create(
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Dados para criação da categoria.")]
            CreateCategoryRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");
            if (!IsManager(user)) return StatusCode(StatusCodes.Status403Forbidden, "Apenas Managers podem criar categorias.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Nome é obrigatório.");

            var name = dto.Name.Trim();

            if (name.Length > MaxCategoryNameLength)
                return BadRequest($"O nome da categoria excede o limite de {MaxCategoryNameLength} caracteres (atual: {name.Length}).");

            var exists = await _db.Categories.AnyAsync(c => c.Name == name);
            if (exists)
                return Conflict("Já existe categoria com esse nome.");

            if (dto.ParentId.HasValue)
            {
                var parent = await _db.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == dto.ParentId.Value);

                if (parent is null)
                    return BadRequest("Categoria pai inexistente.");

                if (parent.ParentId.HasValue)
                    return Conflict("Categorias têm no máximo dois níveis. O pai informado já é uma subcategoria.");
            }

            var c = new CategoryModel { Name = name, ParentId = dto.ParentId };
            _db.Categories.Add(c);
            await _db.SaveChangesAsync();

            var response = new CategoryItemDto(c.Id, c.Name, c.ParentId);
            return CreatedAtAction(nameof(GetById), new { id = c.Id }, response);
        }

        /// <summary>Listar categorias com filtros e paginação</summary>
        /// <remarks>
        /// **Caso de uso**: Consultar categorias por nome e/ou por pai, com paginação.
        ///
        /// **Parâmetros**
        /// - <c>name</c> (query): filtra por <c>Contains</c>.
        /// - <c>parentId</c> (query): filtra categorias cujo pai é <c>parentId</c>.
        /// - <c>page</c> e <c>pageSize</c>: padrão 1 e 20 (mínimo 1).
        ///
        /// **Ordenação**
        /// - Ordena por <c>ParentId</c>, depois por <c>Name</c>.
        ///
        /// **Responses**
        /// - 200: Lista de <c>CategoryItemDto</c> (nome já vem no formato <c>Pai - Nome</c> quando aplicável)
        /// </remarks>
        [HttpGet]
        [SwaggerOperation(Summary = "Listar categorias", Description = "Retorna categorias filtradas/paginadas.")]
        [ProducesResponseType(typeof(IEnumerable<CategoryItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> List(
            [FromQuery, SwaggerParameter("Filtro por parte do nome (Contains).")]
            string? name,
            [FromQuery, SwaggerParameter("Filtra por ID do pai (retorna apenas subcategorias desse pai).")]
            int? parentId,
            [FromQuery, SwaggerParameter("Página (mínimo 1).")]
            int page = 1,
            [FromQuery, SwaggerParameter("Tamanho da página (mínimo 1).")]
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var q = _db.Categories.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(name))
                q = q.Where(c => c.Name.Contains(name));

            if (parentId.HasValue)
                q = q.Where(c => c.ParentId == parentId);

            var total = await q.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            var items = await q
                .OrderBy(c => c.ParentId)
                .ThenBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryItemDto(
                    c.Id,
                    c.Parent != null ? $"{c.Parent.Name} - {c.Name}" : c.Name,
                    c.ParentId
                ))
                .ToListAsync();


            return Ok(items);
        }

        /// <summary>Obter categoria por ID</summary>
        /// <remarks>
        /// **Caso de uso**: Recuperar uma categoria específica, retornando o nome já no formato <c>Pai - Nome</c> quando houver pai.
        ///
        /// **Responses**
        /// - 200: Categoria encontrada
        /// - 404: Categoria não encontrada
        /// </remarks>
        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Detalhar categoria", Description = "Retorna a categoria pelo ID.")]
        [ProducesResponseType(typeof(CategoryItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryItemDto>> GetById(
            [SwaggerParameter("ID da categoria.", Required = true)]
            int id)
        {
            var c = await _db.Categories
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new CategoryItemDto(
                    x.Id,
                    x.Parent != null ? $"{x.Parent.Name} - {x.Name}" : x.Name,
                    x.ParentId
                ))
                .FirstOrDefaultAsync();

            return c is null ? NotFound("Categoria não encontrada.") : Ok(c);
        }

        /// <summary>Deletar categoria</summary>
        /// <remarks>
        /// **Caso de uso**: Remover uma categoria sem filhos e sem tickets ativos associados.
        ///
        /// **Regras**
        /// - Apenas **Manager** pode deletar.
        /// - **Não** pode ter **subcategorias**.
        /// - **Não** pode estar associada a **tickets ativos** (Status ≠ Fechado/Cancelado).
        ///
        /// **Responses**
        /// - 200: Categoria deletada com sucesso
        /// - 401: Usuário inválido/não informado
        /// - 403: Usuário não é Manager
        /// - 404: Categoria não encontrada
        /// - 409: Categoria possui subcategorias ou está associada a tickets ativos
        /// </remarks>
        [HttpDelete("{id:int}")]
        [SwaggerOperation(Summary = "Excluir categoria", Description = "Exclui uma categoria (somente Managers).")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [SwaggerParameter("ID da categoria.", Required = true)]
            int id)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");
            if (!IsManager(user)) return StatusCode(StatusCodes.Status403Forbidden, "Apenas Managers podem deletar categorias.");

            var c = await _db.Categories.FindAsync(new object?[] { id });
            if (c is null) return NotFound("Categoria não encontrada.");

            var hasChildren = await _db.Categories.AnyAsync(x => x.ParentId == id);
            if (hasChildren) return Conflict("Categoria possui subcategorias (filhos).");

            bool hasActiveTickets = await _db.Tickets.AnyAsync(t =>
                t.CategoryId == id &&
                t.Status != TicketStatus.Fechado &&
                t.Status != TicketStatus.Cancelado);

            if (hasActiveTickets)
                return Conflict("Categoria está associada a tickets ativos.");

            _db.Categories.Remove(c);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Categoria deletada com sucesso!" });
        }
    }
}
