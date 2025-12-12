using HelpDesk.Data;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace HelpDesk.Controllers
{
    /// <summary>
    /// 👤 Usuários do sistema - Gerentes, Solicitantes e Agentes
    /// </summary>
    /// <remarks>
    /// **Para que serve**  
    /// Entidade responsável por **cadastrar, consultar, atualizar e excluir** usuários do HelpDesk,
    /// controlando o **papel (Role)** de cada um no processo: <c>Requester</c>, <c>Agent</c> ou <c>Manager</c>.
    ///
    /// **Regras gerais**
    /// - Apenas **Managers** podem **criar**, **atualizar** e **excluir** usuários.
    /// - **E-mail** deve ter formato válido e ser **único**.
    /// - **Role** aceita apenas: <c>Requester</c>, <c>Agent</c>, <c>Manager</c>.
    /// - Exclusão bloqueada se o usuário possuir **tickets ativos** (como requester ou agent).
    /// </remarks>
    [ApiController]
    [Route("api/users")]
    [Tags("Users")]
    public class UsersController : ControllerBase
    {
        private static readonly HashSet<string> _allowedRoles = new(StringComparer.OrdinalIgnoreCase)
        { "Requester", "Agent", "Manager" };

        private readonly AppDbContext _db;
        public UsersController(AppDbContext db) => _db = db;

        private async Task<UserModel?> GetUserFromHeaderAsync(int userId)
        {
            return await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private static bool IsManager(UserModel u) => string.Equals(u.Role, "Manager", StringComparison.OrdinalIgnoreCase);

        /// <summary>Criar usuário</summary>
        /// <remarks>
        /// **Caso de uso**: Cadastrar um novo usuário com nome, e-mail e role.
        ///
        /// **Regras**
        /// - Apenas **Manager** pode criar.
        /// - **Name** e **Email** obrigatórios; e-mail **único**.
        /// - **Role** deve estar entre: <c>Requester</c>, <c>Agent</c>, <c>Manager</c>.
        ///
        /// **Responses**
        /// - 201: Usuário criado (<c>UserResponseDto</c>)
        /// - 400: Dados inválidos (nome/e-mail/role)
        /// - 401: Usuário autenticador inválido/não informado
        /// - 403: Autenticador não é Manager
        /// - 409: E-mail já cadastrado
        /// </remarks>
        [HttpPost]
        [SwaggerOperation(Summary = "Criar usuário", Description = "Cria um usuário (somente Managers).")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserResponseDto>> Create(
            [FromHeader, SwaggerParameter("ID do usuário autenticador (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Dados para criação do usuário.", Required = true)]
            CreateUserDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");
            if (!IsManager(user)) return StatusCode(StatusCodes.Status403Forbidden, "Apenas Managers podem inserir usuários.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Nome é obrigatório.");
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("E-mail é obrigatório.");

            var email = dto.Email.Trim();
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, emailRegex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return BadRequest("Formato de e-mail inválido.");

            if (!_allowedRoles.Contains(dto.Role))
                return BadRequest("Role inválida (Requester, Agent, Manager).");

            var exists = await _db.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower());
            if (exists)
                return Conflict("Já existe usuário com esse e-mail.");

            var u = new UserModel
            {
                Name = dto.Name.Trim(),
                Email = dto.Email.Trim(),
                Role = dto.Role
            };

            _db.Users.Add(u);
            await _db.SaveChangesAsync();

            var response = new UserResponseDto(
                u.Id,
                u.Name,
                u.Email,
                u.Role
            );

            return CreatedAtAction(nameof(GetById), new { id = u.Id }, response);
        }

        /// <summary>Obter usuário por ID (com tickets relacionados)</summary>
        /// <remarks>
        /// **Caso de uso**: Recuperar dados do usuário e seus tickets como **Requester** e **Agent**.
        ///
        /// **Responses**
        /// - 200: Usuário encontrado (<c>UserWithTicketsResponseDto</c>)
        /// - 404: Usuário não encontrado
        /// </remarks>
        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Detalhar usuário", Description = "Retorna usuário por ID, incluindo tickets solicitados e atribuídos.")]
        [ProducesResponseType(typeof(UserWithTicketsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserWithTicketsResponseDto>> GetById(
            [SwaggerParameter("ID do usuário.", Required = true)]
            int id)
        {
            var u = await _db.Users
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new UserWithTicketsResponseDto(
                    x.Id,
                    x.Name,
                    x.Email,
                    x.Role,
                    x.RequestedTickets.Select(t => new UserTicketDto(
                        t.Id,
                        t.Title,
                        t.Status,
                        t.PriorityLevel
                    )),
                    x.AssignedTickets.Select(t => new UserTicketDto(
                        t.Id,
                        t.Title,
                        t.Status,
                        t.PriorityLevel
                    ))
                ))
                .FirstOrDefaultAsync();

            if (u is null) return NotFound("Usuário não encontrado.");

            return Ok(u);
        }

        /// <summary>Listar usuários com filtros e paginação</summary>
        /// <remarks>
        /// **Caso de uso**: Consultar usuários por <c>role</c>, <c>email</c> e <c>name</c>.
        ///
        /// **Parâmetros**
        /// - <c>role</c>: deve ser <c>Requester</c>, <c>Agent</c> ou <c>Manager</c>.
        /// - <c>email</c>: filtro por parte do e-mail.
        /// - <c>name</c>: filtro por parte do nome.
        /// - <c>page</c> / <c>pageSize</c>: paginação (mínimo 1; padrão 1/20).
        ///
        /// **Responses**
        /// - 200: Lista de <c>UserResponseDto</c> (paginada por <c>page</c> e <c>pageSize</c>)
        /// - 400: Role inválida
        /// </remarks>
        [HttpGet]
        [SwaggerOperation(Summary = "Listar usuários", Description = "Retorna usuários filtrados por role, email e nome, com paginação.")]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAll(
            [FromQuery, SwaggerParameter("Filtra por role (Requester/Agent/Manager).")]
            string? role,
            [FromQuery, SwaggerParameter("Filtro por parte do e-mail.")]
            string? email,
            [FromQuery, SwaggerParameter("Filtro por parte do nome.")]
            string? name,
            [FromQuery, SwaggerParameter("Página (mínimo 1).")]
            int page = 1,
            [FromQuery, SwaggerParameter("Tamanho da página (mínimo 1).")]
            int pageSize = 20)
        {
            role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
            email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var q = _db.Users.AsNoTracking();

            if (!string.IsNullOrEmpty(role))
            {
                if (!_allowedRoles.Contains(role))
                    return BadRequest("Role inválida (Requester, Agent, Manager).");

                q = q.Where(u => u.Role == role);
            }

            if (!string.IsNullOrEmpty(email))
            {
                var e = email.ToLower();
                q = q.Where(u => u.Email.ToLower().Contains(e));
            }

            if (!string.IsNullOrEmpty(name))
            {
                var n = name.ToLower();
                q = q.Where(u => u.Name.ToLower().Contains(n));
            }

            var total = await q.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            var users = await q
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserResponseDto(
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role
                ))
                .ToListAsync();

            return Ok(users);
        }

        /// <summary>Atualizar parcialmente um usuário</summary>
        /// <remarks>
        /// **Caso de uso**: Alterar nome, e-mail e/ou role.
        ///
        /// **Regras**
        /// - Apenas **Manager** pode atualizar.
        /// - Se alterar **Email**, deve ser único.
        /// - Se alterar **Role**, deve estar entre <c>Requester</c>/<c>Agent</c>/<c>Manager</c>.
        ///
        /// **Responses**
        /// - 200: Usuário atualizado (<c>UserResponseDto</c>)
        /// - 400: Nenhuma mudança detectada / role inválida / e-mail já existente
        /// - 401: Usuário autenticador inválido/não informado
        /// - 403: Autenticador não é Manager
        /// - 404: Usuário não encontrado
        /// - 409: Conflito de e-mail
        /// </remarks>
        [HttpPatch("{id:int}")]
        [SwaggerOperation(Summary = "Atualizar usuário", Description = "Atualiza parcialmente um usuário (somente Managers).")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserResponseDto>> Patch(
            [SwaggerParameter("ID do usuário a ser atualizado.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário autenticador (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Campos a serem atualizados.")]
            UpdateUserRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null)
                return Unauthorized("Usuário inválido ou não informado.");
            if (!IsManager(user))
                return StatusCode(StatusCodes.Status403Forbidden, "Apenas Managers podem atualizar usuários.");

            var u = await _db.Users.FindAsync(new object?[] { id });
            if (u is null)
                return NotFound("Usuário não encontrado.");

            bool changed = false;

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var newName = dto.Name.Trim();
                if (!string.Equals(u.Name, newName, StringComparison.Ordinal))
                {
                    u.Name = newName;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var newEmail = dto.Email.Trim();

                var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(newEmail, emailRegex,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return BadRequest("Formato de e-mail inválido.");
                    
                if (!string.Equals(u.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var emailExists = await _db.Users.AnyAsync(x => x.Email.ToLower() == newEmail.ToLower() && x.Id != id);
                    if (emailExists)
                        return Conflict("Já existe usuário com esse e-mail.");

                    u.Email = newEmail;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                if (!_allowedRoles.Contains(dto.Role))
                    return BadRequest("Role inválida (Requester, Agent, Manager).");

                if (!string.Equals(u.Role, dto.Role, StringComparison.OrdinalIgnoreCase))
                {
                    u.Role = dto.Role;
                    changed = true;
                }
            }

            if (!changed)
                return BadRequest("Nenhuma alteração detectada.");

            await _db.SaveChangesAsync();

            var response = new UserResponseDto(u.Id, u.Name, u.Email, u.Role);
            return Ok(response);
        }

        /// <summary>Excluir usuário</summary>
        /// <remarks>
        /// **Caso de uso**: Remover um usuário que **não** possua vínculos **ativos** em tickets.
        ///
        /// **Regras**
        /// - Apenas **Manager** pode excluir.
        /// - Bloqueia exclusão se usuário possuir tickets **ativos**:
        ///   - Como **Agent** (atribuído)
        ///   - Como **Requester** (solicitante)
        ///
        /// **Responses**
        /// - 200: Usuário excluído com sucesso
        /// - 401: Usuário autenticador inválido/não informado
        /// - 403: Autenticador não é Manager
        /// - 404: Usuário não encontrado
        /// - 409: Possui tickets ativos (agent/requester)
        /// </remarks>
        [HttpDelete("{id:int}")]
        [SwaggerOperation(Summary = "Excluir usuário", Description = "Exclui um usuário sem tickets ativos vinculados (somente Managers).")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(
            [SwaggerParameter("ID do usuário a excluir.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário autenticador (header obrigatório).", Required = true)]
            int userId)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");
            if (!IsManager(user)) return StatusCode(StatusCodes.Status403Forbidden, "Apenas Managers podem excluir usuários.");

            var u = await _db.Users.FindAsync(new object?[] { id });
            if (u is null) return NotFound("Usuário não encontrado.");

            var hasActiveAsRequester = await _db.Tickets.AnyAsync(t =>
                t.RequesterId == id &&
                t.Status != TicketStatus.Fechado &&
                t.Status != TicketStatus.Cancelado);

            var hasActiveAsAssignee = await _db.Tickets.AnyAsync(t =>
                t.AssigneeId == id &&
                t.Status != TicketStatus.Fechado &&
                t.Status != TicketStatus.Cancelado);

            if (hasActiveAsAssignee)
                return Conflict("Usuário possui tickets ativos vinculados como agent!");

            if (hasActiveAsRequester)
                return Conflict("Usuário possui tickets ativos vinculados como requester!");

            _db.Users.Remove(u);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Usuário excluído com sucesso!" });
        }
    }
}