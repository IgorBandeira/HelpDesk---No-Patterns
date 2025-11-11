using Amazon.S3;
using HelpDesk.Data;
using HelpDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using HDOpts = HelpDesk.Options;
using MSOpts = Microsoft.Extensions.Options;

namespace HelpDesk.Tests.Utilities
{
    public static class TestHelpers
    {
        public static AppDbContext NewInMemoryDb(string? name = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(name ?? $"HelpDesk_{Guid.NewGuid()}")
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .EnableSensitiveDataLogging()
                .Options;

            var ctx = new AppDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        public static FileStorageService NewFileStorageServiceNoop(IAmazonS3 s3)
        {
            var s3Opts = MSOpts.Options.Create(new HDOpts.S3Options
            {
                Bucket = "test-bucket",
                PublicBaseUrl = "https://files.example"
            });
            return new FileStorageService(s3, s3Opts);
        }

        public static NotificationService NewNotificationServiceNoop()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddSingleton<IOptions<HDOpts.SmtpOptions>>(
                MSOpts.Options.Create(new HDOpts.SmtpOptions
                {
                    Host = "localhost",
                    Port = 2525,
                    User = "user",
                    Password = "pass",
                    FromEmail = "noreply@test",
                    FromName = "HelpDesk Test",
                    DisableDelivery = true
                })
            );

            services.AddSingleton<EmailService>(sp =>
                new EmailService(sp.GetRequiredService<IOptions<HDOpts.SmtpOptions>>(),
                    NullLogger<EmailService>.Instance));

            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase($"Notify_{Guid.NewGuid()}"));

            var sp = services.BuildServiceProvider();
            var email = sp.GetRequiredService<EmailService>();
            return new NotificationService(NullLogger<NotificationService>.Instance, email, sp);
        }

        public static void WithUserHeader(this ControllerBase ctrl, int userId)
        {
            ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            ctrl.ControllerContext.HttpContext.Request.Headers["userId"] = userId.ToString();
        }
    }
}
