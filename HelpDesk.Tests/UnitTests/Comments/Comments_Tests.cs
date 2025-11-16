using Xunit;
using FluentAssertions;
using HelpDesk.Controllers;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using HelpDesk.Tests.UnitTests.Utilities;
using Microsoft.AspNetCore.Http;

namespace HelpDesk.Tests.UnitTests.Comments
{
    public class Comments_Tests
    {
        [Fact]
        public async Task Add_Internal_Comment_Should_Require_Participation()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var requester = new UserModel { Name = "Req", Email = "r@x.com", Role = "Requester" };
            var outsider = new UserModel { Name = "Out", Email = "o@x.com", Role = "Requester" };
            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                Status = TicketStatus.Novo,
                Requester = requester,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };
            db.AddRange(requester, outsider, t);
            await db.SaveChangesAsync();
            var sut = new CommentsController(db);

            // Act
            var allowed = await sut.Add(t.Id, requester.Id, new AddCommentDto("interno", CommentVisibility.Internal));
            var forbidden = await sut.Add(t.Id, outsider.Id, new AddCommentDto("interno", CommentVisibility.Internal));

            // Assert
            (allowed.Result as CreatedAtActionResult).Should().NotBeNull();
            (forbidden.Result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task List_Should_Hide_Internal_Comments_From_NonParticipants()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var requester = new UserModel { Name = "Req", Email = "r@x.com", Role = "Requester" };
            var outsider = new UserModel { Name = "Out", Email = "o@x.com", Role = "Requester" };
            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                Status = TicketStatus.Novo,
                Requester = requester,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };
            db.AddRange(requester, outsider, t);
            await db.SaveChangesAsync();
            var sut = new CommentsController(db);

            await sut.Add(t.Id, requester.Id, new AddCommentDto("interno", CommentVisibility.Internal));
            await sut.Add(t.Id, requester.Id, new AddCommentDto("publico", CommentVisibility.Public));

            // Act
            var outsiderList = await sut.List(t.Id, outsider.Id);
            var items = (outsiderList.Result as OkObjectResult)!.Value as IEnumerable<CommentDetailsDto>;

            // Assert
            items!.Should().ContainSingle();
            items.First().Visibility.Should().Be(CommentVisibility.Public);
        }

        [Fact]
        public async Task Put_And_Delete_Should_Be_Allowed_Only_For_Author()
        {
            // Arrange
            var db = TestDbContextFactory.CreateInMemory();
            var author = new UserModel { Name = "U", Email = "u@x.com", Role = "Requester" };
            var other = new UserModel { Name = "O", Email = "o@x.com", Role = "Requester" };
            var t = new TicketModel
            {
                Title = "T",
                Description = "D",
                Status = TicketStatus.Novo,
                Requester = author,
                PriorityLevel = Priority.Baixa,
                CreatedAt = DateTime.Now,
                SlaStartAt = DateTime.Now,
                SlaDueAt = DateTime.Now.AddDays(3)
            };
            db.AddRange(author, other, t);
            await db.SaveChangesAsync();
            var sut = new CommentsController(db);

            var created = await sut.Add(t.Id, author.Id, new AddCommentDto("x", CommentVisibility.Public));
            var cid = ((CommentResponse)((CreatedAtActionResult)created.Result!).Value!).Id;

            // Act
            var putOther = await sut.ReplaceMessage(t.Id, cid, other.Id, new UpdateCommentMessageDto("hack"));
            var putAuthor = await sut.ReplaceMessage(t.Id, cid, author.Id, new UpdateCommentMessageDto("novo"));
            var delOther = await sut.Delete(other.Id, t.Id, cid);
            var delAuthor = await sut.Delete(author.Id, t.Id, cid);

            // Assert
            (putOther.Result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            (putAuthor.Result as OkObjectResult).Should().NotBeNull();
            (delOther as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            (delAuthor as OkObjectResult).Should().NotBeNull();
        }
    }
}
