using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ShoppingPortal.Application;

public sealed class AuthService(IOtpStore otpStore, ICodeDelivery delivery, ISessionStore sessions) : IAuthService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    public async Task<OtpSentResponse> RequestCodeAsync(ContactRequest request, CancellationToken cancellationToken)
    {
        var contact = NormalizeAndValidate(request.Value, request.Kind);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
        var deliveryResult = await delivery.DeliverAsync(request.Kind, contact, code, cancellationToken);
        if (!deliveryResult.ProviderVerifiesCode)
            otpStore.Save(Key(contact, request.Kind), code, expiresAt);
        return new OtpSentResponse("A verification code was sent.", expiresAt,
            deliveryResult.ExposeCodeForDevelopment ? code : null);
    }

    public async Task<LoginResponse?> VerifyCodeAsync(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contact = NormalizeAndValidate(request.Value, request.Kind);
        if (request.Code.Length != 6)
            return null;
        var providerResult = await delivery.VerifyAsync(request.Kind, contact, request.Code, cancellationToken);
        if (providerResult is false || (providerResult is null && !otpStore.TryConsume(Key(contact, request.Kind), request.Code)))
            return null;
        var token = sessions.Create(contact);
        return new LoginResponse(token, DateTimeOffset.UtcNow.AddHours(12), contact);
    }

    private static string Key(string contact, ContactKind kind) => $"{kind}:{contact.ToLowerInvariant()}";

    private static string NormalizeAndValidate(string value, ContactKind kind)
    {
        var normalized = value.Trim();
        var valid = kind switch
        {
            ContactKind.Email => Regex.IsMatch(normalized, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"),
            ContactKind.Mobile => Regex.IsMatch(normalized, @"^\+?[1-9]\d{7,14}$"),
            _ => false
        };
        if (!valid) throw new ArgumentException(kind == ContactKind.Email ? "Enter a valid email address." : "Enter a phone number with country code, such as +14155552671.");
        return kind == ContactKind.Email ? normalized.ToLowerInvariant() : normalized;
    }
}
