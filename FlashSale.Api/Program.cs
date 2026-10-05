using System.Text;
using FlashSale.Api.Data;
using FlashSale.Api.Middleware;
using FlashSale.Api.Models;
using FlashSale.Api.Security;
using FlashSale.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddHostedService<OutboxWorker>();

// --- REDIS CONFIGURATION ---
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? throw new InvalidOperationException("Redis connection string is not configured.");

var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
redisConfig.AbortOnConnectFail = false;
redisConfig.ConnectRetry = 5;
redisConfig.Ssl = true;

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConfig));

builder.Services.AddScoped<IRedisService, RedisService>();
// ---------------------------

// --- HANGFIRE CONFIGURATION ---
// SKIP Hangfire entirely during integration tests
if (!builder.Environment.IsEnvironment("Testing"))
{
    var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("DefaultConnection is not configured.");

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(postgresConnectionString, new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            QueuePollInterval = TimeSpan.FromSeconds(15)
        }));

    builder.Services.AddHangfireServer();
}
// ------------------------------

var secretKey = builder.Configuration["Jwt:SecretKey"] 
    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "FlashSaleApi",
        ValidateAudience = true,
        ValidAudience = "FlashSaleClients",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

var app = builder.Build();

// --- CODE-FIRST SEEDING ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Only seed if the database is reachable (prevents test startup crashes)
    if (await db.Database.CanConnectAsync())
    {
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@test.com");
        if (admin == null)
        {
            admin = new User
            {
                Email = "admin@test.com",
                PasswordHash = PasswordHasher.HashPassword("AdminPass123!"),
                Role = UserRole.Admin
            };
            db.Users.Add(admin);
        }
        else
        {
            admin.Role = UserRole.Admin;
        }
        await db.SaveChangesAsync();
    }
}
// --------------------------

app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();

// --- HANGFIRE DASHBOARD & JOBS ---
// SKIP Dashboard and Job registration during integration tests
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHangfireDashboard("/hangfire");
    
    var recurringJobManager = app.Services.GetService<IRecurringJobManager>();
    if (recurringJobManager != null)
    {
        recurringJobManager.AddOrUpdate<SaleStatusUpdateService>(
            "update-sale-statuses",
            // belwo line show erorr, what is the fix ? jsut give the fix code
            service => service.UpdateSaleStatusesAsync(), // Note: adjust method name if yours is UpdateSaleStatusesAsync
            // service => service.UpdateSaleStatusesAsync(), // Note: adjust method name if yours is UpdateSaleStatusesAsync
            // service => service.UpdateStatusesAsync(), // Note: adjust method name if yours is UpdateSaleStatusesAsync
            Cron.Minutely);
    }
}
// --------------------------

app.MapControllers();

app.Run();

// Required for WebApplicationFactory to access the Program class in tests
public partial class Program { }