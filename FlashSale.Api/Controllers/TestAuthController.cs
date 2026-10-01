using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlashSale.Api.Controllers;

[ApiController]
[Route("test-auth")]
public class TestAuthController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new { message = "This endpoint is public." });
    }

    [HttpGet("buyer-only")]
    [Authorize(Roles = "Buyer")]
    public IActionResult BuyerOnlyEndpoint()
    {
        return Ok(new { message = "Success! You are authorized as a Buyer." });
    }

    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnlyEndpoint()
    {
        return Ok(new { message = "Success! You are authorized as an Admin." });
    }
}