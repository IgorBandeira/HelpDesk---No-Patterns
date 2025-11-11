using MailKit.Net.Smtp;
using MailKit.Security;
using HelpDesk.Options;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HelpDesk.Services
{
    public class EmailService
    {
        private readonly SmtpOptions _opt;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpOptions> opt, ILogger<EmailService> logger)
            => (_opt, _logger) = (opt.Value, logger);

        public async Task SendAsync(IEnumerable<string> to, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (_opt.DisableDelivery)
            {
                _logger.LogInformation("[EmailService] DisableDelivery ativo — e-mail suprimido. Assunto: {Subject}", subject);
                return;
            }

            var rcpts = to?.Where(x => !string.IsNullOrWhiteSpace(x))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToList() ?? new List<string>();

            if (rcpts.Count == 0)
            {
                _logger.LogWarning("Sem destinatários para {Subject}", subject);
                return;
            }

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_opt.FromName ?? string.Empty, _opt.FromEmail ?? string.Empty));
            foreach (var r in rcpts)
                msg.To.Add(MailboxAddress.Parse(r));
            msg.Subject = subject;

            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_opt.Host, _opt.Port, SecureSocketOptions.StartTlsWhenAvailable, ct);

                if (!string.IsNullOrWhiteSpace(_opt.User))
                    await client.AuthenticateAsync(_opt.User, _opt.Password, ct);

                await client.SendAsync(msg, ct);
                _logger.LogInformation("E-mail enviado para {Count} destinatários: {Destinatarios}",
                    rcpts.Count, string.Join(", ", rcpts));
            }
            finally
            {
                try { await client.DisconnectAsync(true, ct); } catch {}
            }
        }
    }
}
