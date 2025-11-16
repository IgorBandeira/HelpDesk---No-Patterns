using FluentAssertions;
using HelpDesk.Controllers;
using HelpDesk.Models;
using HelpDesk.Tests.UnitTests.Utilities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HelpDesk.Tests.UnitTests.Tickets
{
    public class Tickets_Status_Tests
    {
        [Fact]
        public async Task Status_Valid_Transitions_Should_Work_With_Proper_Roles()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var req = new UserModel { Name = "R", Email = "", Role = "Requester" };  
            var ag = new UserModel { Name = "A", Email = "", Role = "Agent" };
            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                Status = TicketStatus.EmAnalise,
                Requester = req,
                Assignee = ag,
                RequesterId = req.Id, 
                AssigneeId = ag.Id,
                PriorityLevel = Priority.Alta,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddHours(24)
            };

            db.AddRange(req, ag, t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);
            sut.WithUserHeader(req.Id);

            // Act
            var toAndamento = await sut.ChangeStatus(t.Id, ag.Id, new ChangeStatusRequestDto (TicketStatus.EmAndamento));
            var toResolvido = await sut.ChangeStatus(t.Id, ag.Id, new ChangeStatusRequestDto (TicketStatus.Resolvido));
            var toFechado = await sut.ChangeStatus(t.Id, req.Id, new ChangeStatusRequestDto (TicketStatus.Fechado));

            // Assert
            (toAndamento.Result as OkObjectResult).Should().NotBeNull();
            (toResolvido.Result as OkObjectResult).Should().NotBeNull();
            (toFechado.Result as OkObjectResult).Should().NotBeNull();
        }

        [Fact]
        public async Task Status_Invalid_Transition_Should_Return_400()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
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
            db.AddRange(req, t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);

            // Act
            var bad = await sut.ChangeStatus(t.Id, req.Id, new ChangeStatusRequestDto (TicketStatus.Fechado));

            // Assert
            (bad.Result as BadRequestObjectResult).Should().NotBeNull();
        }
    }
}
