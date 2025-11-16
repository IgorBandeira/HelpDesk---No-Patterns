using Xunit;
using FluentAssertions;
using HelpDesk.Models;
using HelpDesk.HostedServices;

namespace HelpDesk.Tests.UnitTests.Services
{
    public class SlaBackgroundService_Tests
    {
        [Fact]
        public void ShouldAlert_Returns_True_When_Elapsed_Reaches_85_Percent()
        {
            // Arrange
            var now = DateTime.Now;
            var t = new TicketModel
            {
                Status = TicketStatus.EmAndamento,
                SlaStartAt = now.AddHours(-8.5),
                SlaDueAt = now.AddHours(1.5)
            };

            // Act
            var result = SlaBackgroundService.ShouldAlert(t, now);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ShouldAlert_Returns_False_For_Closed_Or_Canceled_Or_PastDue()
        {
            var now = DateTime.Now;

            var closed = new TicketModel { Status = TicketStatus.Fechado, SlaStartAt = now.AddHours(-1), SlaDueAt = now.AddHours(1) };
            var canceled = new TicketModel { Status = TicketStatus.Cancelado, SlaStartAt = now.AddHours(-1), SlaDueAt = now.AddHours(1) };
            var pastDue = new TicketModel { Status = TicketStatus.EmAnalise, SlaStartAt = now.AddHours(-3), SlaDueAt = now.AddHours(-1) };

            SlaBackgroundService.ShouldAlert(closed, now).Should().BeFalse();
            SlaBackgroundService.ShouldAlert(canceled, now).Should().BeFalse();
            SlaBackgroundService.ShouldAlert(pastDue, now).Should().BeFalse();
        }
    }
}
