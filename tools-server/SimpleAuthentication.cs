using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace tools_server;

public class SimpleAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemaName = "Simple";

    public SimpleAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }


    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Context.Request.Query.TryGetValue("access_token", out var uuid))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, uuid),
            new Claim(ClaimTypes.Name, uuid),
        }, SchemaName);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
