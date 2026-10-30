namespace ShoppingPortal.Application;

public enum ContactKind { Email, Mobile }

public sealed record ContactRequest(string Value, ContactKind Kind);
public sealed record VerifyOtpRequest(string Value, ContactKind Kind, string Code);
public sealed record OtpSentResponse(string Message, DateTimeOffset ExpiresAt, string? DevelopmentCode = null);
public sealed record CodeDeliveryResult(bool ExposeCodeForDevelopment, bool ProviderVerifiesCode);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Contact);
public sealed record CategoryDto(string Id, string Name, string Icon, string Color);
public sealed record ProductDto(string Id, string Name, string Description, decimal Price, decimal? OriginalPrice, string Image, string CategoryId, string Badge);

public interface IAuthService
{
    Task<OtpSentResponse> RequestCodeAsync(ContactRequest request, CancellationToken cancellationToken);
    Task<LoginResponse?> VerifyCodeAsync(VerifyOtpRequest request, CancellationToken cancellationToken);
}

public interface IOtpStore
{
    void Save(string key, string code, DateTimeOffset expiresAt);
    bool TryConsume(string key, string code);
}

public interface ICodeDelivery
{
    Task<CodeDeliveryResult> DeliverAsync(ContactKind kind, string destination, string code, CancellationToken cancellationToken);
    // Null means the app should verify the code in its local OTP store.
    Task<bool?> VerifyAsync(ContactKind kind, string destination, string code, CancellationToken cancellationToken);
}

public interface ISessionStore
{
    string Create(string contact);
    string? FindContact(string token);
}

public interface ICatalogService
{
    IReadOnlyList<CategoryDto> GetCategories();
    IReadOnlyList<ProductDto> GetProducts(string? categoryId);
}
