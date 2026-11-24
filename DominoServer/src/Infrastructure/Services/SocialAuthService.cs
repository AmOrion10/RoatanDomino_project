using System.Net.Http.Json;
using System.Text.Json;

using Application.Exceptions.Connection;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class SocialAuthService(
    IUserRepository userRepository,
    IClientManager clientManager,
    ITokenManager tokenManager,
    IChatManager chatManager,
    ILogger<SocialAuthService> logger)
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<User> AuthenticateProfileAsync(SocialAuthDto dto, CancellationToken cancellationToken = default)
    {
        string provider = string.Equals(dto.Provider, "Facebook", StringComparison.OrdinalIgnoreCase)
            ? "Facebook"
            : (string.Equals(dto.Provider, "Google", StringComparison.OrdinalIgnoreCase) ? "Google" : string.Empty);

        if (string.IsNullOrEmpty(provider))
        {
            throw new WrongPasswordException("Unsupported social sign-in provider. Please use Google or Facebook.");
        }

        string? providerUserId = dto.ProviderUserId;
        string? email = dto.Email;
        string? displayName = dto.DisplayName;

        string? googleClientId = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
        string? facebookAppId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");

        if (provider == "Google" && !string.IsNullOrWhiteSpace(googleClientId))
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
            {
                throw new WrongPasswordException("Google sign-in requires a valid ID token.");
            }

            (providerUserId, email, displayName) = await VerifyGoogleTokenAsync(dto.IdToken, googleClientId, cancellationToken);
        }
        else if (provider == "Facebook" && !string.IsNullOrWhiteSpace(facebookAppId))
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
            {
                throw new WrongPasswordException("Facebook sign-in requires a valid access token.");
            }

            (providerUserId, email, displayName) = await VerifyFacebookTokenAsync(dto.IdToken, facebookAppId, cancellationToken);
        }
        else
        {
            logger.LogInformation("Social auth ({provider}) running in local/tunnel profile-link mode (OAuth client ID env var not set).", provider);
        }

        string effectiveProviderId = !string.IsNullOrWhiteSpace(providerUserId)
            ? providerUserId.Trim()
            : (!string.IsNullOrWhiteSpace(email) ? email.Trim() : Guid.NewGuid().ToString("N")[..12]);

        return await userRepository.FindOrCreateSocialUser(provider, effectiveProviderId, email, displayName);
    }

    public async Task<Tuple<User, Guid>> SignInClientAsync(SocialAuthDto dto, IClient client, CancellationToken cancellationToken = default)
    {
        if (client.IsLoggedIn)
        {
            throw new ClientAlreadyLoggedInException("You are already signed in.");
        }

        User user = await AuthenticateProfileAsync(dto, cancellationToken);

        foreach (IClient other in clientManager.GetAllClients())
        {
            if (other.Id != client.Id && other.IsLoggedIn && other.User is not null && other.User.Id == user.Id)
            {
                throw new ClientAlreadyLoggedInException("This account is already signed in on another device.");
            }
        }

        Guid token = tokenManager.GenerateTokenForClient(client);
        client.User = user;
        client.IsLoggedIn = true;
        chatManager.ClientJoinChat(client, chatManager.GlobalChatId);
        return new Tuple<User, Guid>(user, token);
    }

    private static async Task<(string Sub, string? Email, string? Name)> VerifyGoogleTokenAsync(string idToken, string expectedClientId, CancellationToken ct)
    {
        try
        {
            string url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
            using HttpResponseMessage resp = await SharedHttpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                throw new WrongPasswordException("Google sign-in token could not be verified.");
            }

            using JsonDocument doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                ?? throw new WrongPasswordException("Empty response from Google token verification.");

            JsonElement root = doc.RootElement;
            string aud = root.TryGetProperty("aud", out JsonElement audEl) ? audEl.GetString() ?? string.Empty : string.Empty;
            if (!string.Equals(aud, expectedClientId, StringComparison.Ordinal))
            {
                throw new WrongPasswordException("Google sign-in token audience mismatch.");
            }

            string sub = root.TryGetProperty("sub", out JsonElement subEl) ? subEl.GetString() ?? string.Empty : string.Empty;
            string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
            string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(sub))
            {
                throw new WrongPasswordException("Google sign-in token is missing subject identifier.");
            }

            return (sub, email, name);
        }
        catch (WrongPasswordException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new WrongPasswordException("Unable to reach Google sign-in verification service.");
        }
    }

    private static async Task<(string Id, string? Email, string? Name)> VerifyFacebookTokenAsync(string accessToken, string expectedAppId, CancellationToken ct)
    {
        try
        {
            string? appSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
            if (!string.IsNullOrWhiteSpace(appSecret))
            {
                string debugUrl = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString($"{expectedAppId}|{appSecret}")}";
                using HttpResponseMessage debugResp = await SharedHttpClient.GetAsync(debugUrl, ct);
                if (!debugResp.IsSuccessStatusCode)
                {
                    throw new WrongPasswordException("Facebook sign-in token could not be verified.");
                }
                using JsonDocument debugDoc = await debugResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                    ?? throw new WrongPasswordException("Empty response from Facebook token verification.");
                if (!debugDoc.RootElement.TryGetProperty("data", out JsonElement dataEl) ||
                    !dataEl.TryGetProperty("is_valid", out JsonElement validEl) ||
                    !validEl.GetBoolean())
                {
                    throw new WrongPasswordException("Facebook sign-in token is not valid.");
                }
            }

            string meUrl = $"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}";
            using HttpResponseMessage meResp = await SharedHttpClient.GetAsync(meUrl, ct);
            if (!meResp.IsSuccessStatusCode)
            {
                throw new WrongPasswordException("Facebook profile could not be loaded.");
            }

            using JsonDocument meDoc = await meResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                ?? throw new WrongPasswordException("Empty response from Facebook profile lookup.");
            JsonElement root = meDoc.RootElement;
            string id = root.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
            string? name = root.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new WrongPasswordException("Facebook profile is missing user ID.");
            }

            return (id, email, name);
        }
        catch (WrongPasswordException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new WrongPasswordException("Unable to reach Facebook sign-in verification service.");
        }
    }
}
