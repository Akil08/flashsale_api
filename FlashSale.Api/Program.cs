using FlashSale.Api.Data;
using FlashSale.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using FlashSale.Api.Security;
using FlashSale.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<IJwtService, JwtService>();

var app = builder.Build();

// Register our custom middleware BEFORE authorization
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();