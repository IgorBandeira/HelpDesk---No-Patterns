using HelpDesk.Data;
using HelpDesk.Models;
using HelpDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Linq;

namespace HelpDesk.Controllers
{
    /// <summary>
    /// 🎫 Tickets (ciclo de vida completo)
    /// </summary>
    /// <remarks>
    /// **Para que serve**  
    /// Gerenciar o **ciclo de vida** de um ticket: criação, atualização, atribuição, mudança de status, reabertura e cancelamento,
    /// com controle de **SLA**, **comentários**, **anexos** e **notificações**.
    ///
    /// **Papéis e permissões (resumo)**
    /// - <b>Manager</b>: poderes administrativos (editar/atribuir/cancelar/reabrir conforme regras).
    /// - <b>Requester</b>: solicitante; pode criar ticket e editar atributos do próprio ticket (regras em cada ação).
    /// - <b>Agent</b>: responsável técnico; exigido em algumas transições de status.
    ///
    /// **Regras relevantes**
    /// - <b>SLA</b>: definido a partir da <c>Priority</c> (ex.: Baixa/Média/Alta/Crítica).
    /// - <b>Participantes</b> do ticket: Manager, Requester do ticket, Assignee (agent) — influenciam visibilidade de comentários e permissões.
    /// - Tickets **Fechados/Cancelados** não podem ser editados, atribuídos, comentados, etc., salvo ações específicas (ex.: reabrir).
    /// </remarks>
    [ApiController]
    [Route("api/tickets")]
    [Tags("Tickets")]
    public class TicketsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly NotificationService _notify;

        private static readonly HashSet<string> _allowedPriorities = new(StringComparer.Ordinal)
        {
            Priority.Baixa,
            Priority.Media,
            Priority.Alta,
            Priority.Critica
        };

        public TicketsController(AppDbContext db, NotificationService notify)
            => (_db, _notify) = (db, notify);

        private async Task<UserModel?> GetUserFromHeaderAsync(int userId)
        {
            return await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private static bool IsManager(UserModel u) => string.Equals(u.Role, "Manager", StringComparison.OrdinalIgnoreCase);
        private static bool IsRequester(UserModel u) => string.Equals(u.Role, "Requester", StringComparison.OrdinalIgnoreCase);
        private static bool IsAgent(UserModel u) => string.Equals(u.Role, "Agent", StringComparison.OrdinalIgnoreCase);

        private static bool Owner(UserModel u, TicketModel t) =>
            IsManager(u) || (IsRequester(u) && u.Id == t.RequesterId);

        private static bool Participants(UserModel u, TicketModel t) =>
            IsManager(u) || u.Id == t.RequesterId || u.Id == t.AssigneeId;

        private const int MaxTitleLength = 180;

        /// <summary>Listar tickets com filtros e paginação</summary>
        /// <remarks>
        /// **Caso de uso**: Consultar tickets por múltiplos filtros (status, prioridade, título, datas, envolvidos, categoria, SLA) com paginação.
        ///
        /// **Parâmetros de filtro**
        /// - <c>status</c>: <c>Novo</c>, <c>Em Análise</c>, <c>Em Andamento</c>, <c>Resolvido</c>, <c>Fechado</c>, <c>Cancelado</c>.
        /// - <c>priority</c>: <c>Baixa</c>, <c>Média</c>, <c>Alta</c>, <c>Crítica</c>.
        /// - <c>title</c>: trecho do título.
        /// - <c>createdFrom</c>/<c>createdTo</c>: intervalo de criação.
        /// - <c>requesterId</c>/<c>assigneeId</c>/<c>categoryId</c>.
        /// - <c>slaDueFrom</c>/<c>slaDueTo</c>: intervalo da data de vencimento de SLA.
        /// - <c>overdueOnly</c>: apenas vencidos (SLA Due &lt; agora, excluindo Fechado/Cancelado).
        /// - Paginação: <c>page</c> (min 1), <c>pageSize</c> (min 1).
        ///
        /// **Comportamento**
        /// - Se <c>status</c> não for informado, <b>exclui Cancelado</b> por padrão.
        ///
        /// **Responses**
        /// - 200: Lista de <c>TicketListItemDto</c>
        /// - 400: Status inválido
        /// </remarks>
        [HttpGet]
        [SwaggerOperation(Summary = "Listar tickets", Description = "Retorna tickets filtrados por status/prioridade/título/datas/envolvidos/categoria/SLA, com paginação.")]
        [ProducesResponseType(typeof(IEnumerable<TicketListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<TicketListItemDto>>> List(
            [FromQuery, SwaggerParameter("Status do ticket (Novo, Em Análise, Em Andamento, Resolvido, Fechado, Cancelado).")]
            string? status,
            [FromQuery, SwaggerParameter("Prioridade (Baixa, Média, Alta, Crítica).")]
            string? priority,
            [FromQuery, SwaggerParameter("Busca por título.")]
            string? title,
            [FromQuery, SwaggerParameter("Criado a partir de (>=).")]
            DateTime? createdFrom,
            [FromQuery, SwaggerParameter("Criado até (<=).")]
            DateTime? createdTo,
            [FromQuery, SwaggerParameter("Filtra por ID do solicitante (Requester).")]
            int? requesterId,
            [FromQuery, SwaggerParameter("Filtra por ID do responsável (Agent).")]
            int? assigneeId,
            [FromQuery, SwaggerParameter("Filtra por ID da categoria.")]
            int? categoryId,
            [FromQuery, SwaggerParameter("SLA vencendo a partir de (>=).")]
            DateTime? slaDueFrom,
            [FromQuery, SwaggerParameter("SLA vencendo até (<=).")]
            DateTime? slaDueTo,
            [FromQuery, SwaggerParameter("Apenas tickets vencidos (SLA Due < agora, exclui Fechado/Cancelado).")]
            bool? overdueOnly,
            [FromQuery, SwaggerParameter("Página (mínimo 1).")]
            int page = 1,
            [FromQuery, SwaggerParameter("Tamanho da página (mínimo 1).")]
            int pageSize = 20)
        {
            var validStatuses = new[]
            {
                TicketStatus.Novo,
                TicketStatus.EmAnalise,
                TicketStatus.EmAndamento,
                TicketStatus.Resolvido,
                TicketStatus.Fechado,
                TicketStatus.Cancelado
            };

            if (!string.IsNullOrWhiteSpace(status) && !validStatuses.Contains(status))
            {
                return BadRequest(new
                {
                    message = $"Status inválido: '{status}'.",
                    disponiveis = validStatuses
                });
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            priority = string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();
            title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

            var now = DateTime.Now;
            var q = _db.Tickets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(t => t.Status == status);
            else
                q = q.Where(t => t.Status != TicketStatus.Cancelado);

            if (!string.IsNullOrEmpty(priority))
                q = q.Where(t => t.PriorityLevel == priority);

            if (!string.IsNullOrEmpty(title))
            {
                var tsearch = title.ToLower();
                q = q.Where(t => t.Title.ToLower().Contains(tsearch));
            }

            if (createdFrom.HasValue)
                q = q.Where(t => t.CreatedAt >= createdFrom.Value);
            if (createdTo.HasValue)
                q = q.Where(t => t.CreatedAt <= createdTo.Value);

            if (requesterId.HasValue)
                q = q.Where(t => t.RequesterId == requesterId.Value);

            if (assigneeId.HasValue)
                q = q.Where(t => t.AssigneeId == assigneeId.Value);

            if (categoryId.HasValue)
                q = q.Where(t => t.CategoryId == categoryId.Value);

            if (slaDueFrom.HasValue)
                q = q.Where(t => t.SlaDueAt != null && t.SlaDueAt >= slaDueFrom.Value);
            if (slaDueTo.HasValue)
                q = q.Where(t => t.SlaDueAt != null && t.SlaDueAt <= slaDueTo.Value);

            if (overdueOnly == true)
                q = q.Where(t => t.SlaDueAt != null
                                 && t.SlaDueAt < now
                                 && t.Status != TicketStatus.Fechado
                                 && t.Status != TicketStatus.Cancelado);

            var items = await q
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TicketListItemDto(
                    t.Id,
                    t.Title,
                    t.Status,
                    t.PriorityLevel,
                    t.CreatedAt,
                    t.SlaDueAt,
                    new UserMiniDto(
                        t.RequesterId,
                        t.Requester != null && !string.IsNullOrEmpty(t.Requester.Name)
                            ? t.Requester.Name
                            : "(solicitante removido)"
                    ),
                    new UserMiniDto(
                        t.AssigneeId,
                        t.Assignee != null && !string.IsNullOrEmpty(t.Assignee.Name)
                            ? t.Assignee.Name
                            : "(sem responsável)"
                    ),
                    new CategoryMiniDto(
                        t.CategoryId,
                        t.Category != null && !string.IsNullOrEmpty(t.Category.Name)
                            ? t.Category.Name
                            : "(categoria removida)"
                    )
                ))
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>Obter ticket por ID (detalhes completos)</summary>
        /// <remarks>
        /// **Caso de uso**: Retornar o ticket com **detalhes**, incluindo comentários (respeitando visibilidade), anexos e histórico de ações.
        ///
        /// **Regras**
        /// - Requer header <c>userId</c>.
        /// - Comentários **Internos** apenas para **Participantes** do ticket (Manager/Requester/Assignee).
        ///
        /// **Responses**
        /// - 200: <c>TicketDetailsDto</c>
        /// - 401: Usuário inválido/não informado
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Detalhar ticket", Description = "Retorna um ticket com comentários, anexos e ações (respeita visibilidade).")]
        [ProducesResponseType(typeof(TicketDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TicketDetailsDto>> GetById(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var ticket = await _db.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            bool canSeeInternal = Participants(user, ticket);

            var dto = await _db.Tickets
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new TicketDetailsDto(
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.PriorityLevel,
                    t.CreatedAt,
                    t.AssignedAt,
                    t.ClosedAt,
                    t.SlaStartAt,
                    t.SlaDueAt,

                    new UserMiniDto(
                        t.RequesterId,
                        t.Requester != null && !string.IsNullOrEmpty(t.Requester.Name)
                            ? t.Requester.Name
                            : "(solicitante removido)"
                    ),

                    new UserMiniDto(
                        t.AssigneeId,
                        t.Assignee != null && !string.IsNullOrEmpty(t.Assignee.Name)
                            ? t.Assignee.Name
                            : "(sem responsável)"
                    ),

                    new CategoryMiniDto(
                        t.CategoryId,
                        t.Category != null && !string.IsNullOrEmpty(t.Category.Name)
                            ? t.Category.Name
                            : "(categoria removida)"
                    ),

                    t.Comments.OrderByDescending(c => c.Id)
                              .Where(x => canSeeInternal || x.Visibility == CommentVisibility.Public)
                              .Select(c => new CommentDto(
                                  c.Id,
                                  c.Author != null && !string.IsNullOrEmpty(c.Author.Name)
                                    ? new UserMiniDto(c.Author.Id, c.Author.Name)
                                    : new UserMiniDto(c.AuthorId, "(autor removido)"),

                                  c.Visibility,
                                  c.Message,
                                  c.CreatedAt))
                              .ToList(),

                    t.Attachments
                                .Select(a => new AttachmentListItemDto(
                                    a.Id,
                                    a.TicketId,
                                    a.FileName,
                                    a.ContentType,
                                    a.SizeBytes,
                                    a.StorageKey,
                                    a.PublicUrl,
                                    a.UploadedAt,
                                    (a.UploadedBy != null && !string.IsNullOrEmpty(a.UploadedBy.Name))
                                    ? new UserMiniDto(a.UploadedById, a.UploadedBy.Name)
                                    : new UserMiniDto(a.UploadedById, "(autor removido)")
                                ))
                                .ToList(),
                      t.Actions
                            .OrderByDescending(a => a.CreatedAt)
                            .Select(a => new TicketActionDto(a.Description, a.CreatedAt))
                            .ToList()
                        ))
                        .FirstOrDefaultAsync();

            return dto is null ? NotFound("Ticket não encontrado.") : Ok(dto);
        }

        /// <summary>Criar um novo ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Abrir um ticket com título, descrição, prioridade e categoria.
        ///
        /// **Regras**
        /// - Apenas **Requester** ou **Manager** podem criar.
        /// - <c>Title</c> obrigatório e ≤ 180 caracteres; <c>Description</c> obrigatória.
        /// - <c>Priority</c> deve ser: <c>Baixa</c>, <c>Média</c>, <c>Alta</c>,  ou <c>Crítica</c>.
        /// - <c>CategoryId</c> deve existir.
        /// - SLA calculado com base na prioridade.
        ///
        /// **Responses**
        /// - 201: Ticket criado (<c>TicketResponseDto</c>)
        /// - 400: Dados inválidos (título/descrição/prioridade/categoria)
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão
        /// </remarks>
        [HttpPost]
        [SwaggerOperation(Summary = "Criar ticket", Description = "Abre um novo ticket (Requester ou Manager).")]
        [ProducesResponseType(typeof(TicketResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TicketResponseDto>> Create(
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Dados do ticket.", Required = true)]
            CreateTicketDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            if (!(IsManager(user) || IsRequester(user)))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente Requester ou Manager pode criar ticket.");

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Título é obrigatório.");

            var title = dto.Title.Trim();
            if (title.Length > MaxTitleLength)
                return BadRequest($"Título excede o limite de {MaxTitleLength} caracteres (atual: {title.Length}).");

            if (string.IsNullOrWhiteSpace(dto.Description))
                return BadRequest("Descrição é obrigatória.");

            if (!_allowedPriorities.Contains(dto.Priority))
                return BadRequest("Prioridade inválida (Baixa, Média, Alta, Crítica).");

            await using var tx = await _db.Database.BeginTransactionAsync();

            var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == dto.CategoryId);
            if (!categoryExists)
                return BadRequest($"Categoria #{dto.CategoryId} não encontrada.");

            var now = DateTime.Now;

            var t = new TicketModel
            {
                Title = title,
                Description = dto.Description,
                PriorityLevel = dto.Priority,
                RequesterId = user.Id,
                CategoryId = dto.CategoryId,
                Status = TicketStatus.Novo,
                CreatedAt = now,
                SlaStartAt = now,
                SlaDueAt = now + Priority.ToSla(dto.Priority)
            };

            _db.Tickets.Add(t);
            await _db.SaveChangesAsync();

            _db.TicketActions.Add(new TicketActionModel
            {
                TicketId = t.Id,
                Description = $"Chamado criado por {user.Name}.",
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var response = new TicketResponseDto(
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.PriorityLevel,
                t.CreatedAt,
                t.SlaStartAt,
                t.SlaDueAt,
                t.RequesterId,
                t.CategoryId
            );

            return CreatedAtAction(nameof(GetById), new { id = t.Id }, response);
        }

        /// <summary>Atualizar parcialmente um ticket.</summary>
        /// <remarks>
        /// **Caso de uso**: Editar título, descrição, prioridade e/ou categoria.
        ///
        /// **Regras**
        /// - Somente **Owner** (Requester original) ou **Manager**.
        /// - Ticket **não** pode estar **Fechado/Cancelado**.
        /// - Se mudar **Priority**, reinicia <c>SlaStartAt</c> e recalcula <c>SlaDueAt</c>.
        /// - Cada alteração gera uma **TicketAction** e **notificação**.
        /// - Retorna **400** se campos inválidos ou sem mudanças.
        ///
        /// **Responses**
        /// - 200: Ticket atualizado (<c>TicketResponseDto</c>)
        /// - 400: Dados inválidos / sem mudanças / categoria inexistente
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPatch("{id:int}")]
        [SwaggerOperation(Summary = "Editar ticket", Description = "Atualiza título/descrição/prioridade/categoria (Requester ou Manager).")]
        [ProducesResponseType(typeof(TicketResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TicketResponseDto>> Update(
           [SwaggerParameter("ID do ticket.", Required = true)]
           int id,
           [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
           int userId,
           [FromBody, SwaggerRequestBody("Campos a serem atualizados.")]
           UpdateTicketDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var t = await _db.Tickets.FindAsync(new object?[] { id });
            if (t is null) return NotFound("Ticket não encontrado.");

            if (t.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível editar tickets não ativos.");

            if (!Owner(user, t))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente o solicitante do ticket ou um Manager pode editar este ticket.");

            await using var tx = await _db.Database.BeginTransactionAsync();

            var actions = new List<TicketActionModel>();
            var now = DateTime.Now;
            var changed = false;

            if (!string.IsNullOrWhiteSpace(dto.Title))
            {
                var newTitle = dto.Title.Trim();
                if (newTitle.Length > MaxTitleLength)
                    return BadRequest($"Título excede o limite de {MaxTitleLength} caracteres (atual: {newTitle.Length}).");

                if (!string.Equals(newTitle, t.Title, StringComparison.Ordinal))
                {
                    t.Title = newTitle;
                    changed = true;

                    var action = new TicketActionModel
                    {
                        TicketId = t.Id,
                        Description = $"Título do chamado alterado para: '{newTitle}' - por {user.Name}",
                        CreatedAt = now
                    };
                    actions.Add(action);
                    _db.TicketActions.Add(action);
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Description))
            {
                var newDesc = dto.Description.Trim();
                if (!string.Equals(newDesc, t.Description, StringComparison.Ordinal))
                {
                    t.Description = newDesc;
                    changed = true;

                    var action = new TicketActionModel
                    {
                        TicketId = t.Id,
                        Description = $"Descrição do chamado alterada para: '{newDesc}' - por {user.Name}",
                        CreatedAt = now
                    };
                    actions.Add(action);
                    _db.TicketActions.Add(action);
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Priority))
            {
                if (!_allowedPriorities.Contains(dto.Priority))
                    return BadRequest("Prioridade inválida (Baixa, Média, Alta, Crítica).");

                if (!string.Equals(dto.Priority, t.PriorityLevel, StringComparison.OrdinalIgnoreCase))
                {
                    t.PriorityLevel = dto.Priority;
                    t.SlaStartAt = now;
                    t.SlaDueAt = now + Priority.ToSla(dto.Priority);
                    changed = true; 

                    var action = new TicketActionModel
                    {
                        TicketId = t.Id,
                        Description = $"Prioridade alterada para {dto.Priority} por {user.Name}",
                        CreatedAt = now
                    };
                    actions.Add(action);
                    _db.TicketActions.Add(action);
                }
            }

            if (dto.CategoryId.HasValue)
            {
                var newCategoryId = dto.CategoryId.Value;

                var oldCategoryName = await _db.Categories
                    .AsNoTracking()
                    .Where(c => c.Id == t.CategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();

                var newCategoryName = await _db.Categories
                    .AsNoTracking()
                    .Where(c => c.Id == newCategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();

                if (newCategoryName is null)
                    return BadRequest($"Categoria #{newCategoryId} não encontrada.");

                if (t.CategoryId != newCategoryId)
                {
                    t.CategoryId = newCategoryId;
                    changed = true;

                    var descricaoAcao = oldCategoryName != null
                        ? $"Categoria '{oldCategoryName}' foi trocada para '{newCategoryName}' por {user.Name}."
                        : $"Categoria definida como '{newCategoryName}' por {user.Name}.";

                    var action = new TicketActionModel
                    {
                        TicketId = t.Id,
                        Description = descricaoAcao,
                        CreatedAt = now
                    };
                    actions.Add(action);
                    _db.TicketActions.Add(action);
                }
            }

            if (!changed)
                return BadRequest("Nenhuma alteração detectada.");

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync();

            foreach (var a in actions)
                await _notify.NotifyTicketActionAsync(t.Id, a.Description, HttpContext.RequestAborted);

            await tx.CommitAsync();

            var response = new TicketResponseDto(
                t.Id, t.Title, t.Description, t.Status, t.PriorityLevel,
                t.CreatedAt, t.SlaStartAt, t.SlaDueAt, t.RequesterId, t.CategoryId);

            return Ok(response);
        }

        /// <summary>Atribuir ticket a um Agent</summary>
        /// <remarks>
        /// **Caso de uso**: Definir o responsável técnico pelo ticket.
        ///
        /// **Regras**
        /// - Somente Owner (Requester do ticket ou qualquer Manager).
        /// - Ticket deve estar **ativo** (não Fechado/Cancelado).
        /// - O usuário atribuído deve ter **Role = Agent**.
        /// - Ao atribuir e se status for <c>Novo</c>, muda para <c>Em Análise</c>.
        /// - Gera **TicketAction** e envia **notificação** para requester, antigo agent e o agent atual.
        ///
        /// **Responses**
        /// - 200: <c>AssignResponseDto</c>
        /// - 400: Ticket inativo / agente inválido
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPost("{id:int}/assign")]
        [SwaggerOperation(Summary = "Atribuir agent", Description = "Atribui o ticket a um Agent (Owner ou Manager).")]
        [ProducesResponseType(typeof(AssignResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AssignResponseDto>> Assign(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Dados para atribuição ao Agent.", Required = true)]
            AssignRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var t = await _db.Tickets.FindAsync(new object?[] { id });
            if (t is null) return NotFound("Ticket não encontrado.");
            if (t.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível atribuir tickets não ativos.");

            if (!Owner(user, t))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente o solicitante do ticket ou um Manager pode atribuir este ticket a um agent.");

            var agent = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == dto.AgentId);
            if (agent is null)
                return BadRequest($"Usuário com não encontrado.");

            if (!IsAgent(agent))
                return BadRequest($"Usuário '{agent.Name}' não é um Agent e não pode ser atribuído a este ticket.");

            await using var tx = await _db.Database.BeginTransactionAsync();

            t.AssigneeId = dto.AgentId;
            t.AssignedAt = DateTime.Now;
            if (t.Status == TicketStatus.Novo) t.Status = TicketStatus.EmAnalise;

            var actionDescription = $"Chamado atribuído para agent {agent.Name} por {user.Name}.";
            _db.TicketActions.Add(new TicketActionModel
            {
                TicketId = t.Id,
                Description = actionDescription,
                CreatedAt = DateTime.Now
            });

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync();

            await _notify.NotifyTicketActionAsync(
                t.Id,
                actionDescription,
                ct: HttpContext.RequestAborted,
                extraEmail: agent.Email
            );

            await tx.CommitAsync();

            var response = new AssignResponseDto(
                t.Id,
                t.Status,
                t.AssignedAt!.Value,
                dto.AgentId,
                agent?.Name ?? "Agent desconhecido"
            );

            return Ok(response);
        }

        /// <summary>Alterar o Requester de um ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Transferir a responsabilidade do solicitante (Requester).
        ///
        /// **Regras**
        /// - Somente **Owner** (Requester original) ou **Manager**.
        /// - Ticket deve estar **ativo** (não Fechado/Cancelado).
        /// - Novo Requester deve ter **Role = Requester**.
        /// - Gera **TicketAction** e envia **notificação** aos envolvidos do ticket (incluindo e-mail do novo requester).
        ///
        /// **Responses**
        /// - 200: <c>RequesterResponseDto</c>
        /// - 400: Ticket inativo / requester inválido
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPost("{id:int}/requester")]
        [SwaggerOperation(Summary = "Trocar requester", Description = "Define outro usuário como Requester do ticket (Owner ou Manager).")]
        [ProducesResponseType(typeof(RequesterResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RequesterResponseDto>> Requester(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Dados para troca de requester.", Required = true)]
            RequesterRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var t = await _db.Tickets.FindAsync(new object?[] { id });
            if (t is null) return NotFound("Ticket não encontrado.");
            if (t.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível atribuir tickets não ativos.");

            if (!Owner(user, t))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente o solicitante do ticket ou um Manager pode atribuir este ticket a outro responsável.");

            var requester = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == dto.RequesterId);
            if (requester is null)
                return BadRequest($"Usuário não encontrado.");

            if (!IsRequester(requester))
                return BadRequest($"Usuário '{requester.Name}' não é um Requester e não pode ser atribuído a este ticket.");

            await using var tx = await _db.Database.BeginTransactionAsync();

            t.RequesterId = dto.RequesterId;

            var actionDescription = $"{user.Name} mudou requester para {requester.Name}.";

            _db.TicketActions.Add(new TicketActionModel
            {
                TicketId = t.Id,
                Description = actionDescription,
                CreatedAt = DateTime.Now
            });

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync();

            await _notify.NotifyTicketActionAsync(
                t.Id,
                actionDescription,
                ct: HttpContext.RequestAborted,
                extraEmail: requester.Email
            );
            await tx.CommitAsync();

            var response = new RequesterResponseDto(
                t.Id,
                t.Status,
                dto.RequesterId,
                requester?.Name ?? "Requester desconhecido"
            );

            return Ok(response);
        }

        /// <summary>Mudar status do ticket (workflow)</summary>
        /// <remarks>
        /// **Caso de uso**: Realizar transições válidas de status.
        ///
        /// **Transições permitidas**
        /// - <c>Em Análise → Em Andamento</c> (requer <b>Agent</b> atribuído e que o **Agent logado** execute)
        /// - <c>Em Andamento → Resolvido</c> (requer <b>Agent</b> atribuído e que o **Agent logado** execute)
        /// - <c>Resolvido → Fechado</c> (requer que **Requester logado** execute)
        ///
        /// **Responses**
        /// - 200: <c>ChangeStatusResponseDto</c>
        /// - 400: Transição inválida / pré-condições não atendidas
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão para a transição
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPost("{id:int}/status")]
        [SwaggerOperation(Summary = "Alterar status", Description = "Executa transições válidas do workflow do ticket.")]
        [ProducesResponseType(typeof(ChangeStatusResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ChangeStatusResponseDto>> ChangeStatus(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Nova situação do ticket.", Required = true)]
            ChangeStatusRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var t = await _db.Tickets.FindAsync(id);
            if (t is null) return NotFound("Ticket não encontrado.");

            var cur = t.Status;
            var next = dto.NewStatus;

            bool transitionAllowed = (cur, next) switch
            {
                (TicketStatus.EmAnalise, TicketStatus.EmAndamento) => true,
                (TicketStatus.EmAndamento, TicketStatus.Resolvido) => true,
                (TicketStatus.Resolvido, TicketStatus.Fechado) => true,
                _ => false
            };
            if (!transitionAllowed)
                return BadRequest($"Transição inválida: {cur} -> {next}.\nEssas são as opções válidas: Em Análise -> Em Andamento -> Resolvido -> Fechado");

            bool requiresAgent =
                (cur, next) is (TicketStatus.EmAnalise, TicketStatus.EmAndamento) ||
                (cur, next) is (TicketStatus.EmAndamento, TicketStatus.Resolvido);

            bool requiresRequester =
                (cur, next) is (TicketStatus.Resolvido, TicketStatus.Fechado);

            if (requiresAgent)
            {
                if (t.AssigneeId is null)
                    return BadRequest("Não há agent atribuído a este ticket para executar essa transição.");

                if (t.AssigneeId != userId)
                    return StatusCode(StatusCodes.Status403Forbidden, $"Usuário não permitido para atualizar {cur} -> {next}.");
            }

            if (requiresRequester)
            {
                if (t.RequesterId is null)
                    return BadRequest("Não há requester atribuído a este ticket para executar essa transição.");

                if (t.RequesterId != userId)
                    return StatusCode(StatusCodes.Status403Forbidden, $"Usuário não permitido para atualizar {cur} -> {next}.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync();

            t.Status = next;
            if (next == TicketStatus.Fechado)
                t.ClosedAt = DateTime.Now;

            var actionDescription = $"Status do Chamado atualizado de: '{cur}' para: '{next}' por {user.Name}.";
            _db.TicketActions.Add(new TicketActionModel
            {
                TicketId = t.Id,
                Description = actionDescription,
                CreatedAt = DateTime.Now
            });

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync();

            await _notify.NotifyTicketActionAsync(t.Id, actionDescription, HttpContext.RequestAborted);

            await tx.CommitAsync();

            var response = new ChangeStatusResponseDto(
                t.Id,
                cur,
                t.Status,
                t.ClosedAt
            );

            return Ok(response);
        }

        /// <summary>Reabrir ticket (Resolvido/Fechado → Em Análise)</summary>
        /// <remarks>
        /// **Caso de uso**: Reabrir um ticket para nova análise.
        ///
        /// **Regras**
        /// - Somente **Owner** (Requester original) ou **Manager**.
        /// - Ticket deve estar <c>Resolvido</c> ou <c>Fechado</c>.
        /// - Reabre para <c>Em Análise</c>, limpa <c>ClosedAt</c> e reinicia/recalcula **SLA**.
        /// - Registro obrigatório de comentário **Interno** com o motivo.
        /// - Gera **TicketAction** e envia **notificação**.
        ///
        /// **Responses**
        /// - 200: <c>ReopenResponseDto</c>
        /// - 400: Status atual não permite reabrir
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPost("{id:int}/reopen")]
        [SwaggerOperation(Summary = "Reabrir ticket", Description = "Reabre ticket Resolvido/Fechado para Em Análise (Owner ou Manager).")]
        [ProducesResponseType(typeof(ReopenResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReopenResponseDto>> Reopen(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Motivo da reabertura (opcional).")]
            ReopenRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var t = await _db.Tickets.FindAsync(new object?[] { id });
            if (t is null) return NotFound("Ticket não encontrado.");

            if (!Owner(user, t))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente o solicitante do ticket ou um Manager pode reabrí-lo.");

            if (t.Status is not (TicketStatus.Resolvido or TicketStatus.Fechado))
                return BadRequest("Só reabre se Resolvido/Fechado.");

            var reason = dto.Reason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("O motivo da reabertura é obrigatório.");

            var previous = t.Status;

            t.Status = TicketStatus.EmAnalise;
            t.ClosedAt = null;

            var now = DateTime.Now;
            t.SlaStartAt = now;
            t.SlaDueAt = now + Priority.ToSla(t.PriorityLevel);

            await using var tx = await _db.Database.BeginTransactionAsync();

            if (!string.IsNullOrWhiteSpace(reason))
            {
                var comment = new TicketCommentModel
                {
                    TicketId = t.Id,
                    Visibility = CommentVisibility.Internal,
                    AuthorId = userId,
                    Message = $"Chamado reaberto: {reason}",
                    CreatedAt = now
                };
                _db.TicketComments.Add(comment);
            }

            var actionDescription = $"Chamado reaberto por {user.Name}.";
            _db.TicketActions.Add(new TicketActionModel
            {
                TicketId = t.Id,
                Description = actionDescription,
                CreatedAt = DateTime.Now
            });

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync();

            await _notify.NotifyTicketActionAsync(t.Id, actionDescription, HttpContext.RequestAborted);

            await tx.CommitAsync();

            var response = new ReopenResponseDto(
                t.Id,
                previous,
                t.Status,
                DateTime.Now,
                userId,
                reason
            );

            return Ok(response);
        }

        /// <summary>Cancelar ticket (Novo/Em Análise → Cancelado)</summary>
        /// <remarks>
        /// **Caso de uso**: Cancelar um ticket ainda em fase inicial.
        ///
        /// **Regras**
        /// - Somente **Owner** (Requester original) ou **Manager**.
        /// - Só cancela se status atual for <c>Novo</c> ou <c>Em Análise</c>.
        /// - Define <c>ClosedAt</c>, registra comentário **Interno** com motivo,
        ///   gera **TicketAction** e envia **notificação**.
        ///
        /// **Responses**
        /// - 200: <c>CancelResponseDto</c>
        /// - 400: Status atual não permite cancelar
        /// - 401: Usuário inválido/não informado
        /// - 403: Sem permissão
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPost("{id:int}/cancel")]
        [SwaggerOperation(Summary = "Cancelar ticket", Description = "Cancela ticket Novo/Em Análise (Owner ou Manager).")]
        [ProducesResponseType(typeof(CancelResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CancelResponseDto>> Cancel(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int id,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromBody, SwaggerRequestBody("Motivo do cancelamento.")]
            CancelRequestDto dto)
        {
            var user = await GetUserFromHeaderAsync(userId);
            if (user is null) return Unauthorized("Usuário inválido ou não informado.");

            var t = await _db.Tickets.FindAsync(new object?[] { id });
            if (t is null) return NotFound("Ticket não encontrado.");

            if (!Owner(user, t))
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Somente o solicitante do ticket ou um Manager pode cancelá-lo.");

            if (t.Status is not TicketStatus.Novo && t.Status is not TicketStatus.EmAnalise)
                return BadRequest($"Só cancela se Novo/Em Análise.");

            var reason = dto.Reason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("O motivo do cancelamento é obrigatório.");

            await using var tx = await _db.Database.BeginTransactionAsync();

            var previous = t.Status;

            t.Status = TicketStatus.Cancelado;
            t.ClosedAt = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                var comment = new TicketCommentModel
                {
                    TicketId = t.Id,
                    AuthorId = userId,
                    Visibility = CommentVisibility.Internal,
                    Message = $"Chamado cancelado: {reason}",
                    CreatedAt = DateTime.Now
                };
                _db.TicketComments.Add(comment);
            }

            var actionDescription = $"Chamado cancelado por {user.Name}.";
            _db.TicketActions.Add(new TicketActionModel
            {
                TicketId = t.Id,
                Description = actionDescription,
                CreatedAt = DateTime.Now
            });

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync();

            await _notify.NotifyTicketActionAsync(t.Id, actionDescription, HttpContext.RequestAborted);

            await tx.CommitAsync();

            var response = new CancelResponseDto(
                t.Id,
                previous,
                t.Status,
                t.ClosedAt!.Value,
                userId,
                reason
            );

            return Ok(response);
        }
    }
}
