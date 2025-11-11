using HelpDesk.Data;
using HelpDesk.Models;
using HelpDesk.Services;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.HostedServices
{
    public class SlaBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<SlaBackgroundService> _logger;
        public SlaBackgroundService(IServiceProvider sp, ILogger<SlaBackgroundService> logger)
            => (_sp, _logger) = (sp, logger);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();

                    var now = DateTime.Now;
                    var tickets = await db.Tickets
                        .Where(t => t.SlaDueAt != null
                                 && t.SlaDueAt > now
                                 && t.Status != TicketStatus.Fechado
                                 && t.Status != TicketStatus.Cancelado)
                        .ToListAsync(stoppingToken);

                    foreach (var t in tickets)
                    {
                        if (ShouldAlert(t, now))
                            await notify.NotifySlaAlertAsync(t, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no SLA monitor");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        public static bool ShouldAlert(TicketModel t, DateTime now)
        {
            if (t.SlaDueAt is null) return false;
            if (t.Status is TicketStatus.Fechado or TicketStatus.Cancelado) return false;

            var start = t.SlaStartAt;
            var due = t.SlaDueAt.Value;
            if (due <= now) return false;

            var total = (due - start).TotalMinutes;
            if (total <= 0) return false;

            var elapsed = (now - start).TotalMinutes;
            return total > 0 && elapsed / total >= 0.85;
        }
    }
}
