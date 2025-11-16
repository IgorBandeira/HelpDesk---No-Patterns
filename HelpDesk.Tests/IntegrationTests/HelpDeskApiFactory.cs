using Amazon.S3;
using Amazon.S3.Model;
using HelpDesk.Data;
using HelpDesk.Options;
using HelpDesk.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Data.Common;
using System.Net;
using System.Text;


namespace HelpDesk.Tests.IntegrationTests
{
    public class HelpDeskApiFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly DbConnection _connection;

        public HelpDeskApiFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // 1) Troca o DbContext original pelo SQLite em memória compartilhada
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (dbContextDescriptor != null)
                    services.Remove(dbContextDescriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                services.RemoveAll(typeof(IAmazonS3));

                var s3Mock = new Mock<IAmazonS3>(MockBehavior.Loose);

                s3Mock
                    .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PutObjectResponse
                    {
                        HttpStatusCode = HttpStatusCode.OK
                    });

                services.AddSingleton<IAmazonS3>(s3Mock.Object);

                services.PostConfigure<SmtpOptions>(o => o.DisableDelivery = true);

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.EnsureCreated();
                SeedDatabase(db);
            });
        }

        public new void Dispose()
        {
            _connection.Dispose();
            base.Dispose();
        }

        private static void SeedDatabase(AppDbContext db)
        {
            if (!db.Users.Any())
            {
                db.Users.Add(new Models.UserModel
                {
                    Id = 1,
                    Name = "Requester",
                    Email = "requester@helpdesk.com",
                    Role = "Requester"
                });
                db.Users.Add(new Models.UserModel
                {
                    Id = 2,
                    Name = "Agent",
                    Email = "Agent@helpdesk.com",
                    Role = "Agent"
                });
                db.Users.Add(new Models.UserModel
                {
                    Id = 3,
                    Name = "Manager",
                    Email = "manager@helpdesk.com",
                    Role = "Manager"
                });
            }

            if (!db.Categories.Any())
            {
                db.Categories.Add(new Models.CategoryModel
                {
                    Id = 1,
                    Name = "Infraestrutura",
                    ParentId = null
                });
            }
            db.SaveChanges();
        }
    }
}