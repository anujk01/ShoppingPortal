using System.Collections.Concurrent;
using ShoppingPortal.Application;

namespace ShoppingPortal.Infrastructure;

public sealed class InMemoryOtpStore : IOtpStore
{
    private readonly ConcurrentDictionary<string, (string Code, DateTimeOffset ExpiresAt)> _codes = new();
    public void Save(string key, string code, DateTimeOffset expiresAt) => _codes[key] = (code, expiresAt);
    public bool TryConsume(string key, string code)
    {
        if (!_codes.TryGetValue(key, out var saved) || saved.ExpiresAt <= DateTimeOffset.UtcNow || saved.Code != code) return false;
        return _codes.TryRemove(key, out _);
    }
}

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, (string Contact, DateTimeOffset ExpiresAt)> _sessions = new();
    public string Create(string contact)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _sessions[token] = (contact, DateTimeOffset.UtcNow.AddHours(12));
        return token;
    }
    public string? FindContact(string token) => _sessions.TryGetValue(token, out var session) && session.ExpiresAt > DateTimeOffset.UtcNow ? session.Contact : null;
}

