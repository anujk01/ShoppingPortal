using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using ShoppingPortal.Application;

namespace ShoppingPortal.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("request-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> RequestOtp(ContactRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await auth.RequestCodeAsync(request, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(503, new { message = ex.Message }); }
    }

    [HttpPost("verify-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await auth.VerifyCodeAsync(request, cancellationToken);
            return result is null ? Unauthorized(new { message = "The code is invalid or expired." }) : Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(503, new { message = ex.Message }); }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new { contact = User.Identity?.Name });
}
