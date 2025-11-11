using Xunit;
using FluentAssertions;
using HelpDesk.Tests.Utilities;
using HelpDesk.Controllers;
using HelpDesk.Data;
using HelpDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Amazon.S3;

namespace HelpDesk.Tests.Attachments
{
    public class Attachments_Tests
    {
        [Fact]
        public async Task Upload_Should_Block_Closed_And_Forbidden_Extensions()
        {
            // Arrange
            AppDbContext db = TestDbContextFactory.CreateInMemory();
            var s3 = new Mock<IAmazonS3>(MockBehavior.Strict).Object;
            var storage = TestHelpers.NewFileStorageServiceNoop(s3);   
            var ctrl = new AttachmentsController(db, storage);

            var user = new UserModel { Name = "U", Email = "u@x", Role = "Requester" };
            var tClosed = new TicketModel { Title = "T", Description = "D", Status = TicketStatus.Fechado, PriorityLevel = Priority.Baixa, CreatedAt = DateTime.Now, SlaStartAt = DateTime.Now, SlaDueAt = DateTime.Now.AddDays(3) };
            var tOpen = new TicketModel { Title = "T2", Description = "D2", Status = TicketStatus.Novo, PriorityLevel = Priority.Baixa, CreatedAt = DateTime.Now, SlaStartAt = DateTime.Now, SlaDueAt = DateTime.Now.AddDays(3) };

            db.AddRange(user, tClosed, tOpen);
            await db.SaveChangesAsync();

            tClosed.RequesterId = user.Id;
            tOpen.RequesterId = user.Id;
            await db.SaveChangesAsync();


            IFormFile exe = new FormFile(new MemoryStream(new byte[10]), 0, 10, "file", "evil.exe")
            { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };

            IFormFile ok = new FormFile(new MemoryStream(new byte[100]), 0, 100, "file", "ok.txt")
            { Headers = new HeaderDictionary(), ContentType = "text/plain" };

            // Act
            var badClosed = await ctrl.Upload(tClosed.Id, 1, new AttachmentUploadDto { File = ok });
            var badExt = await ctrl.Upload(tOpen.Id, 1, new AttachmentUploadDto { File = exe });

            // Assert
            (badClosed.Result as BadRequestObjectResult).Should().NotBeNull(); 
            (badExt.Result as BadRequestObjectResult).Should().NotBeNull();
        }
    }
}
