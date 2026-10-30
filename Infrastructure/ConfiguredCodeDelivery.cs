using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using ShoppingPortal.Application;

namespace ShoppingPortal.Infrastructure;

public sealed class OtpDeliveryOptions
{
    public string EmailProvider { get; set; } = "Development";
    public string SmsProvider { get; set; } = "Development";
    public ResendOptions Resend { get; set; } = new();
    public TwilioOptions Twilio { get; set; } = new();
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = "";
    public string FromEmail { get; set; } = "";
}

public sealed class TwilioOptions
{
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string FromNumber { get; set; } = "";
}

public sealed class ConfiguredCodeDelivery(
    IHttpClientFactory httpClientFactory,
    IOptions<OtpDeliveryOptions> configuredOptions,
    ILogger<ConfiguredCodeDelivery> logger,
    IHostEnvironment environment) : ICodeDelivery
{
    private readonly OtpDeliveryOptions _options = configuredOptions.Value;

    public async Task<CodeDeliveryResult> DeliverAsync(ContactKind kind, string destination, string code, CancellationToken cancellationToken)
    {
        var provider = kind == ContactKind.Email ? _options.EmailProvider : _options.SmsProvider;
        if (provider.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("The Development OTP provider is only available in the Development environment.");
            logger.LogInformation("Development OTP for {ContactKind} {Destination}: {Code}", kind, destination, code);
            return new CodeDeliveryResult(true, false);
        }

        try
        {
            if (kind == ContactKind.Email && provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
                await SendWithResendAsync(destination, code, cancellationToken);
            else if (kind == ContactKind.Mobile && provider.Equals("Twilio", StringComparison.OrdinalIgnoreCase))
                await SendWithTwilioAsync(destination, code, cancellationToken);
            else
                throw new InvalidOperationException($"Unsupported {kind} OTP provider '{provider}'.");

            return new CodeDeliveryResult(false, false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "{Provider} could not deliver an OTP to a {ContactKind} recipient.", provider, kind);
            throw new InvalidOperationException("The verification code could not be delivered. Check the configured provider and try again.");
        }
    }

    public Task<bool?> VerifyAsync(ContactKind kind, string destination, string code, CancellationToken cancellationToken) => Task.FromResult<bool?>(null);

    private async Task SendWithResendAsync(string destination, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Resend.ApiKey) || string.IsNullOrWhiteSpace(_options.Resend.FromEmail))
            throw new InvalidOperationException("Resend is selected but its API key or sender email is missing.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = _options.Resend.FromEmail,
            to = new[] { destination },
            subject = "Your Foundry sign-in code",
            text = $"Your Foundry sign-in code is {code}. It expires in 5 minutes.",
            html = $"<p>Your Foundry sign-in code is <strong>{code}</strong>.</p><p>It expires in 5 minutes.</p>"
        });
        await EnsureProviderSuccessAsync(await httpClientFactory.CreateClient().SendAsync(request, cancellationToken), "Resend", cancellationToken);
    }

    private async Task SendWithTwilioAsync(string destination, string code, CancellationToken cancellationToken)
    {
        var twilio = _options.Twilio;
        ValidateTwilioSettings(twilio);
        var account = Uri.EscapeDataString(twilio.AccountSid);
        using var request = CreateTwilioRequest(twilio, $"https://api.twilio.com/2010-04-01/Accounts/{account}/Messages.json");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = destination,
            ["From"] = twilio.FromNumber,
            ["Body"] = $"Your Foundry sign-in code is {code}. It expires in 5 minutes."
        });
        await EnsureProviderSuccessAsync(await httpClientFactory.CreateClient().SendAsync(request, cancellationToken), "Twilio", cancellationToken);
    }

    private static void ValidateTwilioSettings(TwilioOptions twilio)
    {
        if (string.IsNullOrWhiteSpace(twilio.AccountSid) || string.IsNullOrWhiteSpace(twilio.AuthToken))
            throw new InvalidOperationException("Twilio is selected but its account SID or auth token is missing.");
        if (string.IsNullOrWhiteSpace(twilio.FromNumber))
            throw new InvalidOperationException("Configure a Twilio sender number.");
    }

    private static HttpRequestMessage CreateTwilioRequest(TwilioOptions twilio, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{twilio.AccountSid}:{twilio.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private static async Task EnsureProviderSuccessAsync(HttpResponseMessage response, string provider, CancellationToken cancellationToken)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                throw new HttpRequestException($"{provider} returned HTTP {status}.", null, response.StatusCode);
            }
        }
    }
}
