using Xunit;
using FluentAssertions;
using HelpDesk.Tests.Utilities;
using HelpDesk.Controllers;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Tests.Users
{
    public class Users_Tests
    {
        [Fact]
        public async Task Create_Should_Validate_Email_And_Role()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var manager = new UserModel { Name = "M", Email = "m@x", Role = "Manager" };
            db.Users.Add(manager);
            await db.SaveChangesAsync();
            var sut = new UsersController(db);
            
            // Act
            var bad = await sut.Create(manager.Id, new CreateUserDto("X", "bad", "Requester"));
            var ok = await sut.Create(manager.Id, new CreateUserDto("X", "x@x.com", "Requester"));

            // Assert
            (bad.Result as BadRequestObjectResult).Should().NotBeNull();
            (ok.Result as CreatedAtActionResult).Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_Should_Block_When_User_Has_Active_Tickets()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var manager = new UserModel { Name = "M", Email = "m@x", Role = "Manager" };
            var req = new UserModel { Name = "R", Email = "r@x", Role = "Requester" };
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
            db.AddRange(manager, req, t);
            await db.SaveChangesAsync();
            var sut = new UsersController(db);
            // Act
            var result = await sut.Delete(req.Id, manager.Id);
            // Assert
            (result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }
    }
}
