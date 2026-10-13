using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests
{
    /// <summary>
    /// Boots the real API in memory for integration tests, replacing the SQL Server DbContext
    /// with a fresh EF Core InMemory database per factory instance (so tests need no SQL Server).
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"RaceDayTestDb_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<RaceDayDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            // EnsureCreated applies the seed data (the Organiser/Participant rows in the Roles table).
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();

            return host;
        }

        /// <summary>Creates an HttpClient that keeps cookies between requests, so the login session survives.</summary>
        public HttpClient CreateSessionClient() =>
            CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
    }
}
