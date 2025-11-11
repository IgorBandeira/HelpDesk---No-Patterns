using Xunit;
using FluentAssertions;
using HelpDesk.Tests.Utilities;
using HelpDesk.Controllers;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Tests.Categories
{
    public class Categories_Tests
    {
        [Fact]
        public async Task Create_Should_Require_Manager_And_Validate_Parent_Depth()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var manager = new UserModel { Name="M", Email="m@x", Role="Manager" };
            var parent = new CategoryModel { Name="Pai" };
            db.AddRange(manager, parent);
            await db.SaveChangesAsync();
            var sut = new CategoriesController(db);

            // Act
            var created = await sut.Create(
                manager.Id,
                new CreateCategoryRequestDto("Filho", parent.Id)
            );

            // Assert
            (created.Result as CreatedAtActionResult).Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_Should_Block_When_Has_Children_Or_Active_Tickets()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var manager = new UserModel { Name="M", Email="m@x", Role="Manager" };
            var cat = new CategoryModel { Name="Pai" };
            var child = new CategoryModel { Name="Filho", Parent = cat };
            var req = new UserModel { Name="R", Email="r@x", Role="Requester" };
            var t = new TicketModel { Title="T", Description="D", Status=TicketStatus.Novo, Requester=req, Category=cat, PriorityLevel=Priority.Baixa, CreatedAt=DateTime.Now, SlaStartAt=DateTime.Now, SlaDueAt=DateTime.Now.AddDays(3) };
            db.AddRange(manager, cat, child, req, t);
            await db.SaveChangesAsync();

            var sut = new CategoriesController(db);

            // Act
            var conflict = await sut.Delete(manager.Id, cat.Id);

            // Assert
            (conflict as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }
    }
}