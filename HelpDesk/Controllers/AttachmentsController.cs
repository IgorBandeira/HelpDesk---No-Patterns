using HelpDesk.Data;
using HelpDesk.Models;
using HelpDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace HelpDesk.Controllers
{
    /// <summary>
    /// 📎 Anexos de Ticket
    /// </summary>
    /// <remarks>
    /// Rota/entidade responsável por **gerenciar arquivos anexados** a um ticket.
    ///
    /// **Para que serve**
    /// - **Upload** de arquivos (máx. 10MB; extensões bloqueadas: .exe, .bat, .sh)
    /// - **Listagem** dos anexos de um ticket
    /// - **Consulta** de um anexo específico
    /// - **Exclusão** de anexo (somente pelo **autor**)
    ///
    /// **Regras gerais**
    /// - Ticket **não** pode estar **Fechado** ou **Cancelado** para upload/exclusão
    /// - Header **userId** é obrigatório nas operações que mudam estado (upload/exclusão)
    /// </remarks>
    [ApiController]
    [Route("api/tickets/{ticketId:int}/attachments")]
    [Tags("Attachments")]
    public class AttachmentsController : ControllerBase
    {
        private static readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".bat", ".sh" };

        private readonly AppDbContext _db;
        private readonly FileStorageService _storage;

        public AttachmentsController(AppDbContext db, FileStorageService storage)
            => (_db, _storage) = (db, storage);

        /// <summary>Upload de anexo para um ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Anexar um arquivo ao ticket informado.
        ///
        /// **Validações**
        /// - Tamanho máximo: **10MB**
        /// - Extensões bloqueadas: **.exe**, **.bat**, **.sh**
        /// - Ticket não pode estar **Fechado** ou **Cancelado**
        /// - Header obrigatório: **userId**
        ///
        /// **Entrada (multipart/form-data)**
        /// - Campo <c>file</c> em <c>AttachmentUploadDto</c>
        ///
        /// **Responses**
        /// - 201: Anexo criado com sucesso (retorna metadados do anexo)
        /// - 400: Arquivo inválido, extensão proibida, ticket inativo, usuário inválido
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Upload de anexo", Description = "Anexa um arquivo ao ticket (máx. 10MB; .exe/.bat/.sh bloqueados).")]
        [ProducesResponseType(typeof(AttachmentResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AttachmentResponseDto>> Upload(
            [SwaggerParameter("ID do ticket ao qual o anexo pertencerá.", Required = true)]
            int ticketId,
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [FromForm, SwaggerParameter("Dados do upload (multipart/form-data) com o campo 'file'.", Required = true)]
            AttachmentUploadDto uploadDto
        )
        {
            var file = uploadDto.File;
            if (file is null || file.Length == 0) return BadRequest("Arquivo inválido.");
            if (file.Length > 10 * 1024 * 1024) return BadRequest("Máx 10MB.");

            var ext = Path.GetExtension(file.FileName);
            if (!string.IsNullOrEmpty(ext) && _blocked.Contains(ext))
                return BadRequest("Extensão proibida.");

            var ticket = await _db.Tickets.FindAsync(new object?[] { ticketId });
            if (ticket is null) return NotFound("Ticket não encontrado.");

            if (ticket.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível anexar arquivos em tickets não ativos.");

            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return BadRequest("Usuário inválido ou não informado.");

            var key = $"tickets/{ticketId}/{file.FileName}";
            var (_, url) = await _storage.SaveAsync(file, key);

            var now = DateTime.Now;
            var att = new AttachmentModel
            {
                TicketId = ticketId,
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                SizeBytes = file.Length,
                StorageKey = key,
                PublicUrl = url,
                UploadedById = userId,
                UploadedAt = now
            };

            _db.Attachments.Add(att);
            await _db.SaveChangesAsync();

            var dto = new AttachmentResponseDto(
                att.Id, att.TicketId, att.FileName, att.ContentType, att.SizeBytes,
                att.StorageKey, att.PublicUrl, att.UploadedAt,
                att.UploadedById
            );

            return Created($"/api/tickets/{ticketId}/attachments/{att.Id}", dto);
        }

        /// <summary>Listar anexos de um ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Obter a lista de anexos de um ticket de forma decrescente.
        ///
        /// **Responses**
        /// - 200: Lista de anexos
        /// - 404: Ticket não encontrado
        /// </remarks>
        [HttpGet]
        [SwaggerOperation(Summary = "Listar anexos do ticket", Description = "Retorna os anexos associados ao ticket.")]
        [ProducesResponseType(typeof(IEnumerable<AttachmentListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<AttachmentListItemDto>>> ListByTicket(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId)
        {
            var exists = await _db.Tickets.AsNoTracking().AnyAsync(t => t.Id == ticketId);
            if (!exists) return NotFound("Ticket não encontrado.");

            var items = await _db.Attachments
                .AsNoTracking()
                .Where(a => a.TicketId == ticketId)
                .OrderByDescending(a => a.Id)
                .Select(a => new AttachmentListItemDto(
                    a.Id,
                    a.TicketId,
                    a.FileName,
                    a.ContentType,
                    a.SizeBytes,
                    a.StorageKey,
                    a.PublicUrl,
                    a.UploadedAt,
                    new UserMiniDto(
                        a.UploadedById ?? 0,
                        (a.UploadedBy != null && !string.IsNullOrEmpty(a.UploadedBy.Name))
                            ? a.UploadedBy.Name
                            : "(autor removido)"
                    )
                ))
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>Obter um anexo específico do ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Consultar os metadados de um anexo (nome, tipo, tamanho, URL pública, autor).
        ///
        /// **Responses**
        /// - 200: Metadados do anexo
        /// - 404: Anexo não encontrado
        /// </remarks>
        [HttpGet("{attachmentId:int}")]
        [SwaggerOperation(Summary = "Detalhar anexo", Description = "Retorna os metadados de um anexo específico do ticket.")]
        [ProducesResponseType(typeof(AttachmentListItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AttachmentListItemDto>> GetById(
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId,
            [SwaggerParameter("ID do anexo.", Required = true)]
            int attachmentId)
        {
            var a = await _db.Attachments
                .AsNoTracking()
                .Where(x => x.TicketId == ticketId && x.Id == attachmentId)
                .Select(x => new AttachmentListItemDto(
                    x.Id,
                    x.TicketId,
                    x.FileName,
                    x.ContentType,
                    x.SizeBytes,
                    x.StorageKey,
                    x.PublicUrl,
                    x.UploadedAt,
                    new UserMiniDto(
                        x.UploadedById ?? 0,
                        (x.UploadedBy != null && !string.IsNullOrEmpty(x.UploadedBy.Name))
                            ? x.UploadedBy.Name
                            : "(autor removido)"
                    )
                ))
                .FirstOrDefaultAsync();

            return a is null ? NotFound("Anexo não encontrado.") : Ok(a);
        }

        /// <summary>Excluir um anexo do ticket</summary>
        /// <remarks>
        /// **Caso de uso**: Remover um anexo **do próprio autor**.
        ///
        /// **Regras**
        /// - Ticket não pode estar **Fechado** ou **Cancelado**
        /// - Somente o **autor** do anexo pode excluir
        /// - Header obrigatório: **userId**
        ///
        /// **Responses**
        /// - 200: Anexo deletado com sucesso
        /// - 400: Ticket inativo
        /// - 403: Tentativa de excluir anexo de outro autor
        /// - 404: Ticket ou anexo não encontrado
        /// </remarks>
        [HttpDelete("{attachmentId:int}")]
        [SwaggerOperation(Summary = "Excluir anexo", Description = "Exclui um anexo do ticket (somente o autor).")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromHeader, SwaggerParameter("ID do usuário (header obrigatório).", Required = true)]
            int userId,
            [SwaggerParameter("ID do ticket.", Required = true)]
            int ticketId,
            [SwaggerParameter("ID do anexo.", Required = true)]
            int attachmentId)
        {
            var ticket = await _db.Tickets
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket is null)
                return NotFound("Ticket não encontrado.");

            if (ticket.Status is TicketStatus.Fechado or TicketStatus.Cancelado)
                return BadRequest("Não é possível excluir anexos em tickets não ativos.");

            var att = await _db.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId);

            if (att is null) return NotFound("Anexo não encontrado.");

            if (att.UploadedById != userId)
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Não é possível excluir anexos de outras pessoas!");

            try
            {
                if (!string.IsNullOrWhiteSpace(att.StorageKey))
                    await _storage.DeleteAsync(att.StorageKey);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[S3 Delete Error] Falha ao excluir arquivo '{att.StorageKey}': {ex.Message}");
            }

            _db.Attachments.Remove(att);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Anexo deletado com sucesso!" });
        }
    }
}
