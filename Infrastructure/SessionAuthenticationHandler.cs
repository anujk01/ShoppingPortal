using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShoppingPortal.Application;

namespace ShoppingPortal.Infrastructure;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionStore sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var contact = sessions.FindContact(header[7..].Trim());
        if (contact is null) return Task.FromResult(AuthenticateResult.Fail("Invalid or expired session."));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, contact)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
