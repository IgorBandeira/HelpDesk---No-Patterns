using FluentAssertions;
using HelpDesk.Tests.Utilities;  
using HelpDesk.Controllers;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HelpDesk.Tests.Tickets
{
    public class Tickets_List_Tests
    {
        [Fact]
        public async Task List_Should_Exclude_Canceled_By_Default_And_Apply_Basic_Filters()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();

            var req = new UserModel { Name = "R", Email = "r@x", Role = "Requester" };
            var cat = new CategoryModel { Name = "Infra" };

            var open = new TicketModel
            {
                Title = "VPN fora",
                Description = "...",
                Status = TicketStatus.Novo,
                Requester = req,
                Category = cat,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };

            var canceled = new TicketModel
            {
                Title = "Impressora",
                Description = "...",
                Status = TicketStatus.Cancelado,
                Requester = req,
                Category = cat,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };

            db.AddRange(req, cat, open, canceled);
            await db.SaveChangesAsync();

            var notify = TestHelpers.NewNotificationServiceNoop();
            var sut = new TicketsController(db, notify);

            // Act
            var result = await sut.List(
                status: null,
                priority: null,
                title: "VPN",
                createdFrom: null,
                createdTo: null,
                requesterId: null,
                assigneeId: null,
                categoryId: null,
                slaDueFrom: null,
                slaDueTo: null,
                overdueOnly: null,
                page: 1,
                pageSize: 20
            );

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();

            var list = ok!.Value as IEnumerable<TicketListItemDto>;
            list.Should().NotBeNull();
            list!.Should().ContainSingle();                
            list.First().Title.Should().Contain("VPN");    
        }
    }
}
