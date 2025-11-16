using Xunit;
using FluentAssertions;
using HelpDesk.Controllers;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using HelpDesk.Tests.UnitTests.Utilities;

namespace HelpDesk.Tests.UnitTests.Tickets
{
    public class Tickets_Assign_Tests
    {
        [Fact]
        public async Task Assign_Should_Set_Agent_And_Move_To_EmAnalise_When_Novo()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();

            var manager = new UserModel { Name = "M", Email = "m@x", Role = "Manager" };
            var agent = new UserModel { Name = "A", Email = "a@x.com", Role = "Agent" };
            var req = new UserModel { Name = "R", Email = "r@x.com", Role = "Requester" };

            db.AddRange(manager, agent, req);
            await db.SaveChangesAsync();

            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                Status = TicketStatus.Novo,
                RequesterId = req.Id,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };
            db.Tickets.Add(t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);
            sut.WithUserHeader(req.Id);

            // Act
            var result = await sut.Assign(t.Id, manager.Id, new AssignRequestDto(agent.Id));

            // Assert
            (result.Result as OkObjectResult).Should().NotBeNull();
            var updated = await db.Tickets.FindAsync(t.Id);
            updated!.Status.Should().Be(TicketStatus.EmAnalise);
            updated.AssigneeId.Should().Be(agent.Id);
            updated.AssignedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task Assign_Should_Reject_NonAgent()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var manager = new UserModel { Name = "M", Email = "m@x", Role = "Manager" };
            var bad = new UserModel { Name = "U", Email = "", Role = "Requester" };
            var req = new UserModel { Name = "R", Email = "", Role = "Requester" };

            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                Status = TicketStatus.Novo,
                Requester = req,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };

            db.AddRange(manager, bad, req, t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);

            // Act
            var result = await sut.Assign(t.Id, manager.Id, new AssignRequestDto(bad.Id));

            // Assert
            (result.Result as BadRequestObjectResult).Should().NotBeNull();
        }
    }
}
