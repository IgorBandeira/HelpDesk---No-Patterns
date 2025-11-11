using FluentAssertions;
using HelpDesk.Controllers;
using HelpDesk.Models;
using HelpDesk.Tests.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HelpDesk.Tests.Tickets
{
    public class Tickets_Create_Tests
    {
        [Fact]
        public async Task Create_AsRequester_Should_Create_Ticket_With_SLA_And_Action()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var requester = new UserModel { Name = "Req", Email = "r@x.com", Role = "Requester" };
            var cat = new CategoryModel { Name = "Infra" };
            db.Users.Add(requester);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);

            var dto = new CreateTicketDto(
                "Servidor fora",
                "Parou tudo",
                Priority.Alta,
                cat.Id
            );

            // Act
            var result = await sut.Create(requester.Id, dto);

            // Assert
            var created = (result.Result as CreatedAtActionResult)!.Value as TicketResponseDto;
            created.Should().NotBeNull();
            created!.Title.Should().Be("Servidor fora");
            created.Priority.Should().Be(Priority.Alta);
            created.SlaStartAt.Should().NotBe(default);
            created.SlaDueAt.Should().BeAfter(created.SlaStartAt);


            var action = await db.TicketActions.FirstOrDefaultAsync();
            action.Should().NotBeNull();
            action!.Description.Should().Contain("Chamado criado");
        }

        [Fact]
        public async Task Create_Should_Reject_Invalid_Priority_And_Missing_Fields()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var requester = new UserModel { Name = "Req", Email = "r@x.com", Role = "Requester" };
            db.Users.Add(requester);
            await db.SaveChangesAsync();

            var sut = new TicketsController(db, TestHelpers.NewNotificationServiceNoop());

            // Act
            var bad1 = await sut.Create(requester.Id, new CreateTicketDto("", "x", Priority.Baixa, 1)); 
            var bad2 = await sut.Create(requester.Id, new CreateTicketDto("ok", "", Priority.Baixa, 1));  
            var bad3 = await sut.Create(requester.Id, new CreateTicketDto("ok", "ok", "Inválida", 1));

            // Assert
            bad1.Result.Should().BeOfType<BadRequestObjectResult>();
            bad2.Result.Should().BeOfType<BadRequestObjectResult>();
            bad3.Result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}
