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

// --- CODE-FIRST SEEDING (Promotes or Creates Admin on startup) ---
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
        admin.Role = UserRole.Admin; // Promotes existing registered user to Admin
    }
    db.SaveChanges();
}
// -----------------------------------------------------

app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();