using FluentAssertions;
using HelpDesk.Controllers;
using HelpDesk.Data;
using HelpDesk.Models;
using HelpDesk.Tests.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using Xunit;

namespace HelpDesk.Tests.Tickets
{
    public class Tickets_ReopenCancel_Tests
    {
        [Fact]
        public async Task Reopen_Should_Require_Reason_And_Create_Internal_Comment()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var req = new UserModel { Name = "Req", Email = "", Role = "Requester" };
            var t = new TicketModel
            {
                Title = "X",
                Description = "Y",
                Status = TicketStatus.Fechado,
                PriorityLevel = Priority.Media,
                Requester = req,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddHours(48)
            };
            db.Users.Add(req);
            db.Tickets.Add(t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);
            sut.WithUserHeader(req.Id);

            // Act
            var bad = await sut.Reopen(t.Id, req.Id, new ReopenRequestDto ( " " ));
            (bad.Result as BadRequestObjectResult).Should().NotBeNull();

            var ok = await sut.Reopen(t.Id, req.Id, new ReopenRequestDto ("motivo"));
            (ok.Result as OkObjectResult).Should().NotBeNull();

            // Assert
            var comment = await db.TicketComments.FirstOrDefaultAsync();
            comment.Should().NotBeNull();
            comment!.Visibility.Should().Be(CommentVisibility.Internal);
            comment.Message.Should().Contain("reaberto");
        }

        [Fact]
        public async Task Cancel_Should_Require_Reason_And_Log_Action()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var req = new UserModel { Name = "Req", Email = "", Role = "Requester" };
            var t = new TicketModel
            {
                Title = "X",
                Description = "Y",
                Status = TicketStatus.Novo,
                PriorityLevel = Priority.Media,
                Requester = req,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddHours(48)
            };
            db.Users.Add(req);
            db.Tickets.Add(t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);
            sut.WithUserHeader(req.Id);

            // Act
            var bad = await sut.Cancel(t.Id, req.Id, new CancelRequestDto (" "));
            (bad.Result as BadRequestObjectResult).Should().NotBeNull();

            var ok = await sut.Cancel(t.Id, req.Id, new CancelRequestDto ("motivo"));
            (ok.Result as OkObjectResult).Should().NotBeNull();

            // Assert
            var action = await db.TicketActions.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
            action.Should().NotBeNull();
            action!.Description.Should().Contain("cancelado");
        }
    }
}
