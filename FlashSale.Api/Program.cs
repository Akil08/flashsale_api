// using System.Text;
// using FlashSale.Api.Data;
// using FlashSale.Api.Middleware;
// using FlashSale.Api.Services;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;

// var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddControllers();

// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// builder.Services.AddScoped<IJwtService, JwtService>();

// // --- JWT Authentication Configuration ---
// var secretKey = builder.Configuration["Jwt:SecretKey"] 
//     ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//     options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
// })
// .AddJwtBearer(options =>
// {
//     options.TokenValidationParameters = new TokenValidationParameters
//     {
//         ValidateIssuer = true,
//         ValidIssuer = "FlashSaleApi",
//         ValidateAudience = true,
//         ValidAudience = "FlashSaleClients",
//         ValidateLifetime = true,
//         ValidateIssuerSigningKey = true,
//         IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
//     };
// });
// // ----------------------------------------

// var app = builder.Build();

// // Authentication MUST come before Authorization
// app.UseAuthentication();
// app.UseMiddleware<RequestLoggingMiddleware>();
// app.UseAuthorization();

// app.MapControllers();

// app.Run();



using System.Text;
using FlashSale.Api.Data;
using FlashSale.Api.Middleware;
using FlashSale.Api.Models;
using FlashSale.Api.Security;
using FlashSale.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IJwtService, JwtService>();

// --- REGISTER HOSTED SERVICE HERE (BEFORE builder.Build) ---
// is this serive singlaton, or scoped, or transient ?
// The OutboxWorker is registered as a hosted service, which means it is a singleton by default.
// Hosted services are long-running background tasks that are managed by the ASP.NET Core runtime, 
// and they are typically instantiated once and run for the lifetime of the application.
builder.Services.AddHostedService<OutboxWorker>();
// -----------------------------------------------------------

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

app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();