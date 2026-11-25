using System.Collections.Concurrent;

namespace Infrastructure.Services;

public sealed class OAuthPendingSession
{
    public required string State { get; init; }
    public required string Provider { get; init; }
    public string Status { get; set; } = "pending"; // "pending", "completed", "error"
    public string? OneTimeCode { get; set; }
    public string? ProviderUserId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AvatarBase64 { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExpiresAtUtc { get; init; } = DateTime.UtcNow.AddMinutes(5);
}

public class OAuthStateStore
{
    private readonly ConcurrentDictionary<string, OAuthPendingSession> sessionsByState = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, OAuthPendingSession> sessionsByCode = new(StringComparer.Ordinal);

    public OAuthPendingSession CreatePending(string provider, string? requestedState = null)
    {
        PruneExpired();
        string state = string.IsNullOrWhiteSpace(requestedState) ? Guid.NewGuid().ToString("N") : requestedState.Trim();
        OAuthPendingSession session = new()
        {
            State = state,
            Provider = provider,
            Status = "pending",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        sessionsByState[state] = session;
        return session;
    }

    public OAuthPendingSession? GetByState(string state)
    {
        PruneExpired();
        return sessionsByState.TryGetValue(state, out OAuthPendingSession? session) ? session : null;
    }

    public bool TryComplete(
        string state,
        string providerUserId,
        string? email,
        string? displayName,
        string? avatarUrl,
        string? avatarBase64,
        out string oneTimeCode)
    {
        oneTimeCode = string.Empty;
        if (!sessionsByState.TryGetValue(state, out OAuthPendingSession? session) || session.ExpiresAtUtc < DateTime.UtcNow)
        {
            return false;
        }

        oneTimeCode = Guid.NewGuid().ToString("N");
        session.ProviderUserId = providerUserId;
        session.Email = email;
        session.DisplayName = displayName;
        session.AvatarUrl = avatarUrl;
        session.AvatarBase64 = avatarBase64;
        session.OneTimeCode = oneTimeCode;
        session.Status = "completed";
        sessionsByCode[oneTimeCode] = session;
        return true;
    }

    public void MarkError(string state, string message)
    {
        if (sessionsByState.TryGetValue(state, out OAuthPendingSession? session))
        {
            session.Status = "error";
            session.ErrorMessage = message;
        }
    }

    public OAuthPendingSession? RedeemOneTimeCode(string code)
    {
        PruneExpired();
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (!sessionsByCode.TryRemove(code.Trim(), out OAuthPendingSession? session)) return null;
        sessionsByState.TryRemove(session.State, out _);
        return session.ExpiresAtUtc >= DateTime.UtcNow ? session : null;
    }

    private void PruneExpired()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, OAuthPendingSession> kv in sessionsByState)
        {
            if (kv.Value.ExpiresAtUtc < now)
            {
                sessionsByState.TryRemove(kv.Key, out _);
                if (!string.IsNullOrEmpty(kv.Value.OneTimeCode))
                {
                    sessionsByCode.TryRemove(kv.Value.OneTimeCode, out _);
                }
            }
        }
    }
}
