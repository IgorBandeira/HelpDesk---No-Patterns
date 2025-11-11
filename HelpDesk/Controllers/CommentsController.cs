using HelpDesk.Data;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using Swashbuckle.AspNetCore.Annotations;

namespace HelpDesk.Controllers
{
    /// <summary>
    /// 💬 Comentários de Ticket
    /// </summary>
    /// <remarks>
    /// **Para que serve**  
    /// Gerenciar **comentários** de um ticket com visibilidade **Público** ou **Interno**.
    ///
    /// **Regras gerais**
    /// - **Interno** só é visível para: **Manager**, **Requester** (solicitante) e **Assignee** (responsável).
    /// - Tamanho máximo de mensagem: **4000** caracteres.
    /// - Não é permitido **comentar/editar/excluir** em tickets **Fechados** ou **Cancelados**.
    /// - Somente o **autor** pode **editar** ou **excluir** seu comentário.
    /// - Header **userId** é obrigatório nas operações protegidas.
    /// </remarks>
    [ApiController]
    [Route("api/tickets/{ticketId:int}/comments")]
    [Tags("Comments")]
    public class CommentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CommentsController(AppDbContext db) => _db = db;

        private async Task<UserModel?> GetUserFromHeaderAsync(int userId)
        {
            return await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private static bool IsManager(UserModel u) => string.Equals(u.Role, "Manager", StringComparison.OrdinalIgnoreCase);

        private static bool Participants(UserModel u, TicketModel t) =>
            IsManager(u) || u.Id == t.RequesterId || u.Id == t.AssigneeId;

        private const int MaxCommentChars = 4000;

        /// <summary>Adicionar comentário ao ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Criar um comentário **Público** (visualização de qualquer pessoa) ou **Interno** (visualizaçãoo somente das pessoas relacionadas ao chamado).
        ///
        /// **Regras**
        /// - Mensagem obrigatória e ≤ 4000 caracteres.
        /// - Visibilidade aceita: **"Público"** ou **"Interno"**.
        /// - Comentário **Interno**: somente **Manager/Requester/Assignee** podem criar.
        /// - Proibido comentar em ticket **Fechado/Cancelado**.
        ///
        /// **Responses**
        /// - 201: Criado com sucesso (retorna <c>CommentResponse</c>)
        /// - 400: Mensagem inválida, visibilidade inválida, ticket inativo
        /// - 404: Ticket não encontrado
        /// - 403: Sem permissão para comentário interno
        /// </remarks>
        [HttpPost]
        [SwaggerOperation(Summary = "Adicionar comentário", Description = "Cria um comentário público ou interno para o ticket.")]
        [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CommentResponse>> Add(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Dados do comentário (mensagem e visibilidade).", Required = true)]
            AddCommentDto dto)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return BadRequest("Usuário inválido ou não informado.");

            var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            if (ticket.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível comentar em tickets não ativos.");

            var rawMessage = dto.Message ?? string.Empty;
            var message = rawMessage.Trim();

            if (string.IsNullOrWhiteSpace(message))
                return BadRequest("Mensagem é obrigatória.");

            if (message.Length > MaxCommentChars)
                return BadRequest($"Mensagem excede o limite de {MaxCommentChars} caracteres (atual: {message.Length}).");

            var visibility = string.IsNullOrWhiteSpace(dto.Visibility)
                ? CommentVisibility.Public
                : dto.Visibility;

            if (visibility != CommentVisibility.Public && visibility != CommentVisibility.Internal)
                return BadRequest("Visibilidade inválida. Use 'Público' ou 'Interno'.");

            if (visibility == CommentVisibility.Internal && !Participants(user, ticket))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente requester e assignee do ticket em questão ou manager podem criar comentários internos.");

            var c = new TicketCommentModel
            {
                TicketId = ticketId,
                AuthorId = userId,
                Visibility = visibility,
                Message = message,
                CreatedAt = DateTime.Now
            };

            _db.TicketComments.Add(c);
            await _db.SaveChangesAsync();

            var resp = new CommentResponse(
                c.Id,
                c.AuthorId,
                c.Visibility,
                c.Message,
                c.CreatedAt
            );

            return CreatedAtAction(nameof(GetById), new { ticketId, id = c.Id }, resp);
        }

        /// <summary>Obter comentário por ID</summary>
        /// <remarks>
        /// **Caso de uso**: Detalhar um comentário do ticket.
        ///
        /// **Regras**
        /// - Comentários **Internos** só são retornados para **Manager/Requester/Assignee** do respectivo ticket.
        ///
        /// **Responses**
        /// - 200: Comentário encontrado (<c>CommentDetailsDto</c>)
        /// - 401: Usuário inválido/não informado
        /// - 404: Ticket ou Comentário não encontrado
        /// </remarks>
        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Detalhar comentário", Description = "Retorna um comentário do ticket (respeita visibilidade).")]
        [ProducesResponseType(typeof(CommentDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CommentDetailsDto>> GetById(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId,
            [SwaggerParameter("ID do comentário.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var ticket = await _db.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            bool canSeeInternal = Participants(user, ticket);

            var c = await _db.TicketComments
                .Where(x => x.TicketId == ticketId && x.Id == id)
                .Where(x => canSeeInternal || x.Visibility == CommentVisibility.Public)
                .Select(x => new CommentDetailsDto(
                    x.Id,
                    new UserMiniDto(
                        x.AuthorId ?? 0,
                        x.Author != null && !string.IsNullOrEmpty(x.Author.Name)
                            ? x.Author.Name
                            : "(autor removido)"
                    ),
                    x.Visibility,
                    x.Message,
                    x.CreatedAt
                ))
                .FirstOrDefaultAsync();

            return c is null ? NotFound("Comentário não encontrado.") : Ok(c);
        }

        /// <summary>Listar comentários do ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Obter a lista de comentários do ticket decrescente.
        ///
        /// **Regras**
        /// - Comentários **Internos** só são exibidos para **Manager/Requester/Assignee** do respectivo ticket.
        ///
        /// **Responses**
        /// - 200: Lista de <c>CommentDetailsDto</c>
        /// - 401: Usuário inválido/não informado
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpGet]
        [SwaggerOperation(Summary = "Listar comentários", Description = "Retorna os comentários do ticket (aplica regra de visibilidade).")]
        [ProducesResponseType(typeof(IEnumerable<CommentDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<CommentDetailsDto>>> List(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var ticket = await _db.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            bool canSeeInternal = Participants(user, ticket);

            var q = _db.TicketComments
                .AsNoTracking()
                .Where(x => x.TicketId == ticketId);

            if (!canSeeInternal)
                q = q.Where(x => x.Visibility == CommentVisibility.Public);

            var items = await q
                .OrderByDescending(x => x.Id)
                .Select(x => new CommentDetailsDto(
                    x.Id,
                    new UserMiniDto(
                        x.AuthorId ?? 0,
                        x.Author != null && !string.IsNullOrEmpty(x.Author.Name)
                            ? x.Author.Name
                            : "(autor removido)"
                    ),
                    x.Visibility,
                    x.Message,
                    x.CreatedAt
                ))
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>Editar a mensagem do comentário</summary>
        /// <remarks>
        /// **Caso de uso**: Atualizar o conteúdo da mensagem do comentário feito anteriormente (do próprio autor).
        ///
        /// **Regras**
        /// - Somente o **autor** pode editar.
        /// - Ticket **não** pode estar **Fechado/Cancelado**.
        /// - Mensagem obrigatória e **≤ 4000** caracteres.
        /// - Se não houver alteração real, retorna **400**.
        /// - Edição persiste com prefixo **"editado: "** e atualiza <c>CreatedAt</c>.
        ///
        /// **Responses**
        /// - 200: Comentário atualizado (<c>CommentDetailsDto</c>)
        /// - 400: Mensagem inválida / sem mudança / ticket inativo
        /// - 404: Ticket ou Comentário não encontrado
        /// - 403: Edição de comentário de outro autor
        /// </remarks>
        [HttpPut("{id:int}")]
        [SwaggerOperation(Summary = "Editar comentário (replace)", Description = "Substitui integralmente a mensagem do comentário (apenas autor).")]
        [ProducesResponseType(typeof(CommentDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CommentDetailsDto>> ReplaceMessage(
           [SwaggerParameter("ID do ticket.", Required = true)]
           int ticketId,
           [SwaggerParameter("ID do comentário.", Required = true)]
           int id,
           [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
           int userId,
           [FromBody, SwaggerRequestBody("Nova mensagem do comentário.", Required = true)]
           UpdateCommentMessageDto dto)
        {
            var ticket = await _db.Tickets
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            if (ticket.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível editar comentários em tickets não ativos.");

            var rawMessage = dto.Message ?? string.Empty;
            var message = rawMessage.Trim();

            if (string.IsNullOrWhiteSpace(message))
                return BadRequest("Mensagem é obrigatória.");

            if (message.Length > MaxCommentChars)
                return BadRequest($"Mensagem excede o limite de {MaxCommentChars} caracteres (atual: {message.Length}).");

            var c = await _db.TicketComments
                .Include(x => x.Author)
                .FirstOrDefaultAsync(x => x.TicketId == ticketId && x.Id == id);

            if (c is null)
                return NotFound("Comentário não encontrado.");

            if (c.AuthorId != userId)
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Não é possível editar comentários de outras pessoas!");

            var currentNormalized = (c.Message ?? string.Empty).Trim();
            if (string.Equals(currentNormalized, message, StringComparison.Ordinal))
                return BadRequest("Não houve mudança.");

            c.Message = $"editado: {message}";
            c.CreatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            var resp = new CommentDetailsDto(
                c.Id,
                new UserMiniDto(
                        c.AuthorId ?? 0,
                        c.Author != null && !string.IsNullOrEmpty(c.Author.Name)
                            ? c.Author.Name
                            : "(autor removido)"
                    ),
                c.Visibility,
                c.Message,
                c.CreatedAt
            );

            return Ok(resp);
        }

        /// <summary>Excluir comentário</summary>
        /// <remarks>
        /// **Caso de uso**: Remover um comentário **do próprio autor**.
        ///
        /// **Regras**
        /// - Ticket **não** pode estar **Fechado/Cancelado**.
        /// - Somente o **autor** pode excluir.
        ///
        /// **Responses**
        /// - 200: Comentário deletado com sucesso
        /// - 400: Ticket inativo
        /// - 403: Exclusão de comentário de outro autor
        /// - 404: Ticket ou Comentário não encontrado
        /// </remarks>
        [HttpDelete("{id:int}")]
        [SwaggerOperation(Summary = "Excluir comentário", Description = "Exclui um comentário do ticket (apenas o autor).")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId,
            [SwaggerParameter("ID do comentário.", Required = true)]
            int id)
        {
            var ticket = await _db.Tickets
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            if (ticket.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível excluir comentários em tickets não ativos.");

            var c = await _db.TicketComments
                .FirstOrDefaultAsync(x => x.TicketId == ticketId && x.Id == id);

            if (c is null)
                return NotFound("Comentário não encontrado.");

            if (c.AuthorId != userId)
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Não é possível excluir comentários de outras pessoas!");

            _db.TicketComments.Remove(c);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Comentário deletado com sucesso!" });
        }
    }
}
