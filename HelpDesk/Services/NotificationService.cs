using HelpDesk.Data;
using HelpDesk.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HelpDesk.Services
{
    public class NotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly EmailService _email;
        private readonly IServiceProvider _sp;

        public NotificationService(
            ILogger<NotificationService> logger,
            EmailService email,
            IServiceProvider sp)
            => (_logger, _email, _sp) = (logger, email, sp);

        private async Task<List<string>> GetTicketParticipantEmailsAsync(int ticketId, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var t = await db.Tickets
                .AsNoTracking()
                .Where(x => x.Id == ticketId)
                .Select(x => new
                {
                    ReqEmail = x.Requester != null ? x.Requester.Email : null,
                    AssEmail = x.Assignee != null ? x.Assignee.Email : null
                })
                .FirstOrDefaultAsync(ct);

            var all = new List<string?>();
            if (t != null)
            {
                all.Add(t.ReqEmail);
                all.Add(t.AssEmail);
            }

            return all.Where(e => !string.IsNullOrWhiteSpace(e))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList()!;
        }

        private static string HtmlEncode(string s) => System.Net.WebUtility.HtmlEncode(s);

        private async Task<string> BuildTicketPaperCardAsync(
            TicketModel t,
            bool isAlert,
            CancellationToken ct)
        {
            string categoryName = "(categoria removida)";
            if (t.CategoryId.HasValue)
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                categoryName = await db.Categories
                    .AsNoTracking()
                    .Where(c => c.Id == t.CategoryId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(ct) ?? "(categoria removida)";
            }

            var created = t.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            var due = t.SlaDueAt.HasValue ? t.SlaDueAt.Value.ToString("dd/MM/yyyy HH:mm") : "-";
            var desc = string.IsNullOrWhiteSpace(t.Description) ? "-" : HtmlEncode(t.Description);

            var bgColor = isAlert ? "#ffe6e6" : "#f5f5f5";
            var borderColor = isAlert ? "#ff4d4d" : "#ddd";
            var strongColor = isAlert ? "#d00000" : "#000";

            return $@"
            <div style=""background:{bgColor};border:1px solid {borderColor};
                        border-radius:8px;padding:12px;margin:16px 0"">
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Título:</strong> {HtmlEncode(t.Title)}</div>
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Status:</strong> {HtmlEncode(t.Status)}</div>
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Prioridade:</strong> {HtmlEncode(t.PriorityLevel)}</div>
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Categoria:</strong> {HtmlEncode(categoryName)}</div>
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Criado em:</strong> {created}</div>
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Vence em:</strong> {due}</div>
              <div style=""margin:4px 0""><strong style=""color:{strongColor}"">Descrição:</strong></div>
              <div style=""white-space:pre-wrap;line-height:1.35"">{desc}</div>
            </div>";
        }

        private static string HtmlLayout(string title, string body, string? middleBlockHtml = null)
        {
            var sb = new StringBuilder();
            sb.Append($@"
            <div style=""font-family:Arial,Helvetica,sans-serif;font-size:14px"">
              <h2 style=""margin:0 0 12px 0"">{title}</h2>
              <div>{body}</div>
              <hr style=""margin:16px 0;""/>");

            if (!string.IsNullOrWhiteSpace(middleBlockHtml))
                sb.Append(middleBlockHtml);

            sb.Append(@"
              <hr style=""margin:16px 0;""/>
              <div style=""color:#666;font-size:12px;text-align:right"">
                Mensagem automática do HelpDesk - NoReply
              </div>
            </div>");
            return sb.ToString();
        }

        public async Task NotifySlaAlertAsync(TicketModel t, CancellationToken ct = default)
        {
            _logger.LogWarning("SLA alerta (≥85%): Ticket #{Id} vence em {Due}", t.Id, t.SlaDueAt);

            var emails = await GetTicketParticipantEmailsAsync(t.Id, ct);
            if (emails.Count == 0) return;

            var title = $"⚠️ [HelpDesk] Alerta de SLA — Ticket #{t.Id}";
            var body = $@"
            <p>O ticket <strong style=""color:#d00000"">#{t.Id}</strong> 
            (“<strong style=""color:#d00000"">{HtmlEncode(t.Title)}</strong>”) 
            está <strong style=""color:#d00000"">próximo do vencimento de SLA</strong>.</p>
            <p>Por favor, priorize a resolução antes do prazo final.</p>";

            var middle = await BuildTicketPaperCardAsync(t, isAlert: true, ct);
            await _email.SendAsync(
                emails,
                title,
                HtmlLayout(title, body, middle),
                ct
            );
        }

        public async Task NotifyTicketActionAsync(
            int ticketId,
            string description,
            CancellationToken ct = default,
            string? extraEmail = null)
        {
            _logger.LogInformation("Ticket #{Id} ação: {Desc}", ticketId, description);

            var emails = await GetTicketParticipantEmailsAsync(ticketId, ct);
            if (!string.IsNullOrWhiteSpace(extraEmail))
                emails.Add(extraEmail);

            emails = emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (emails.Count == 0) return;

            string subjectCore;
            var descTrim = description?.Trim() ?? string.Empty;

            if (descTrim.StartsWith("Título do chamado alterado", StringComparison.OrdinalIgnoreCase))
                subjectCore = "Título do chamado alterado.";
            else if (descTrim.StartsWith("Descrição do chamado alterada", StringComparison.OrdinalIgnoreCase))
                subjectCore = "Descrição do chamado alterada.";
            else
                subjectCore = descTrim;

            var title = $"[HelpDesk] Ticket #{ticketId} — {subjectCore}";
            var body = $"<p>{HtmlEncode(descTrim)}</p>";

            string? middle = null;
            using (var scope = _sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var t = await db.Tickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == ticketId, ct);

                if (t is not null)
                    middle = await BuildTicketPaperCardAsync(t, isAlert: false, ct);
            }

            await _email.SendAsync(
                emails,
                title,
                HtmlLayout(title, body, middle),
                ct
            );
        }

    }
}
