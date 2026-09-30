using FlashSale.Api.Data;
using FlashSale.Api.Models;
using FlashSale.Api.Security;
using FlashSale.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext dbContext, IJwtService jwtService, ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        bool emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
        {
            return Conflict(new { error = "Email is already registered." });
        }

        string passwordHash = PasswordHasher.HashPassword(request.Password);

        var newUser = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            Role = UserRole.Buyer
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User registered successfully: {Email}", newUser.Email);

        return CreatedAtAction(nameof(Register), new { id = newUser.Id }, new 
        { 
            id = newUser.Id, 
            email = newUser.Email, 
            role = newUser.Role 
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        // 1. Find the user by email
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
        {
            // Return generic error to prevent email enumeration
            return Unauthorized(new { error = "Invalid email or password." });
        }

        // 2. Verify the password
        bool isPasswordValid = PasswordHasher.VerifyPassword(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        // 3. Generate and return the JWT
        string token = _jwtService.GenerateToken(user);

        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        return Ok(new { token });
    }
}

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
