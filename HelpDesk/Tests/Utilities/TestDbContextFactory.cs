using HelpDesk.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HelpDesk.Tests.Utilities
{
    public static class TestDbContextFactory
    {
        public static AppDbContext CreateInMemory(string name = "HelpDeskTests")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"{name}_{Guid.NewGuid()}")
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .EnableSensitiveDataLogging()
                .Options;

            var ctx = new AppDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }
}