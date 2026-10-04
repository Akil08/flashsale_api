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
using FlashSale.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// --- HANGFIRE CONFIGURATION ---
var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(postgresConnectionString, new PostgreSqlStorageOptions
    {
        SchemaName = "hangfire", // Keeps Hangfire tables organized
        QueuePollInterval = TimeSpan.FromSeconds(15) // Efficient polling
    }));

builder.Services.AddHangfireServer();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddScoped<SaleStatusUpdateService>();

// --- BULLETPROOF REDIS CONFIGURATION ---
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? throw new InvalidOperationException("Redis connection string is not configured.");

// 1. Parse the connection string into a ConfigurationOptions object
var redisConfig = ConfigurationOptions.Parse(redisConnectionString);

// 2. CRITICAL: Prevent the app from crashing on startup if Redis is temporarily unreachable
redisConfig.AbortOnConnectFail = false;
redisConfig.ConnectRetry = 5;

// 3. Ensure TLS/SSL is enabled (Upstash requires this)
redisConfig.Ssl = true;

// 4. Register the Singleton using the CONFIG OBJECT, not the raw string
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisConfig));

builder.Services.AddScoped<IRedisService, RedisService>();
// ---------------------------------------

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
    var admin = db.Users.FirstOrDefault(u => u.Email == "admin@test.com");
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
    db.SaveChanges();
}
// --------------------------


// Register the recurring job AFTER app is built
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    // Schedule the job to run every 1 minute
    recurringJobManager.AddOrUpdate<SaleStatusUpdateService>(
        "update-sale-statuses",
        service => service.UpdateSaleStatusesAsync(),
        Cron.Minutely);
}


app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();


// Add Hangfire Dashboard (Optional, but great for debugging)
// Note: In a real production app, you would secure this dashboard with authentication!
app.UseHangfireDashboard("/hangfire");

app.MapControllers();



app.Run();

public partial class Program { }