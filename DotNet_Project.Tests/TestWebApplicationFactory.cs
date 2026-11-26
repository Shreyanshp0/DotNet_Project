using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

[CollectionDefinition("WebApp")]
public class WebAppCollection : ICollectionFixture<TestWebApplicationFactory>
{
}

public class TestAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new("test-token", "test-cookie", "RequestVerificationToken", "X-CSRF-TOKEN");
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => new("test-token", "test-cookie", "RequestVerificationToken", "X-CSRF-TOKEN");
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
    public void SetCookieTokenAndHeader(HttpContext httpContext) { }
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"test_{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbName}"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<IAntiforgery, TestAntiforgery>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbName))
        {
            try { File.Delete(_dbName); } catch { }
        }
    }
}
