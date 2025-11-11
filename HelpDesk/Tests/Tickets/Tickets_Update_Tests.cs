using FluentAssertions;
using HelpDesk.Controllers;
using HelpDesk.Models;
using HelpDesk.Tests.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HelpDesk.Tests.Tickets
{
    public class Tickets_Update_Tests
    {
        [Fact]
        public async Task Patch_Should_Recalculate_SLA_On_Priority_Change_And_Log_Action()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var req = new UserModel { Name = "Req", Email = "", Role = "Requester" };
            var cat = new CategoryModel { Name = "Cat" };
            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                PriorityLevel = Priority.Baixa,
                Status = TicketStatus.Novo,
                Requester = req,
                Category = cat,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddHours(72)
            };
            db.AddRange(req, cat, t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);
            sut.WithUserHeader(req.Id);

            // Act
            var result = await sut.Update(t.Id, req.Id, new UpdateTicketDto { Priority = Priority.Critica });

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();

            var reloaded = await db.Tickets.FirstAsync(x => x.Id == t.Id);
            reloaded.PriorityLevel.Should().Be(Priority.Critica);

            reloaded.SlaStartAt.Should().NotBe(default);
            reloaded.SlaDueAt.Should().BeAfter(reloaded.SlaStartAt);

            var action = await db.TicketActions.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
            action.Should().NotBeNull();
            action!.Description.Should().Contain("Prioridade");
        }

        [Fact]
        public async Task Patch_Should_Return_400_When_No_Changes()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var req = new UserModel { Name = "Req", Email = "", Role = "Requester" };
            var cat = new CategoryModel { Name = "Cat" };
            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                PriorityLevel = Priority.Baixa,
                Status = TicketStatus.Novo,
                Requester = req,
                Category = cat,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };
            db.AddRange(req, cat, t);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);
            sut.WithUserHeader(req.Id);

            // Act
            var result = await sut.Update(t.Id, req.Id, new UpdateTicketDto());

            // Assert
            (result.Result as BadRequestObjectResult).Should().NotBeNull();
        }
    }
}
