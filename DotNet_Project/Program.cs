using DotNet_Project.Data;
using DotNet_Project.Filters;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=mindcare.db";

var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider")
    ?? (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ? "Sqlite" : "MySql");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)));
    }
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "MindCare.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddAuthorization();
builder.Services.AddScoped<MustChangePasswordFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<MustChangePasswordFilter>();
});
builder.Services.AddHttpClient();

// Register application domain services
builder.Services.AddScoped<IGeminiAiService, GeminiAiService>();
builder.Services.AddScoped<IAIReportService, AIReportService>();
builder.Services.AddScoped<IMoodService, MoodService>();
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<IJournalSentimentService, JournalSentimentService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IParentActivityService, ParentActivityService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

// Initialize database schema and roles on startup
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (db.Database.IsSqlite())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "MySQL migration failed or skipped. If using MySQL, ensure the server is running and run 'dotnet ef database update'.");
        }
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await DbSeeder.SeedAsync(db, userManager, roleManager);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "MindCare database/roles initialization encountered an issue: {Message}", ex.Message);
}

app.Run();

void LoadDotEnv()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
        Path.Combine(AppContext.BaseDirectory, ".env")
    };

    foreach (var path in candidates)
    {
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                var idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    var key = trimmed[..idx].Trim();
                    var value = trimmed[(idx + 1)..].Trim().Trim('"', '\'');
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                    if (key.Equals("GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("Gemini__ApiKey", value);
                    }
                    if (key.Equals("GEMINI_MODEL", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("Gemini__Model", value);
                    }
                    if (key.Equals("DATABASE_PROVIDER", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("DatabaseProvider", value);
                    }
                    if (key.Equals("DEFAULT_CONNECTION", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", value);
                    }
                }
            }
            break;
        }
    }
}

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
