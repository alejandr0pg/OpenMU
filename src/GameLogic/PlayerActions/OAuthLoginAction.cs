// <copyright file="OAuthLoginAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Action to handle OAuth login from supported providers.
/// </summary>
public class OAuthLoginAction
{
    private static readonly HttpClient SharedHttpClient = new();
    private static int _templateCounter;

    /// <summary>
    /// Attempts OAuth login with the specified provider and token.
    /// </summary>
    public async ValueTask LoginAsync(Player player, byte provider, string token)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        player.Logger.LogInformation("OAuth Login Attempt: Provider={provider}", provider);

        try
        {
            var (email, loginPrefix) = await this.ResolveEmailAsync(player, provider, token).ConfigureAwait(false);
            if (email is null)
            {
                return;
            }

            var loginName = $"{loginPrefix}{email}";
            var account = await this.GetOrCreateAccountAsync(player, loginName, email).ConfigureAwait(false);
            if (account is null)
            {
                return;
            }

            await this.CompleteLoginAsync(player, account, loginName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "OAuth Login Exception");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
        }
    }

    private async ValueTask<(string? Email, string Prefix)> ResolveEmailAsync(Player player, byte provider, string token)
    {
        string? email;
        string prefix;

        switch (provider)
        {
            case 0:
                email = await this.ValidateGoogleTokenAsync(player, token).ConfigureAwait(false);
                prefix = "google:";
                break;
            case 1:
                email = await this.ValidateFacebookTokenAsync(player, token).ConfigureAwait(false);
                prefix = "facebook:";
                break;
            case 2:
                email = await this.ValidateAppleTokenAsync(player, token).ConfigureAwait(false);
                prefix = "apple:";
                break;
            default:
                player.Logger.LogWarning("Unknown OAuth provider: {provider}", provider);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                return (null, string.Empty);
        }

        if (string.IsNullOrEmpty(email))
        {
            player.Logger.LogWarning("OAuth validation failed: Email not found.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)).ConfigureAwait(false);
            return (null, string.Empty);
        }

        return (email, prefix);
    }

    private async ValueTask<Account?> GetOrCreateAccountAsync(Player player, string loginName, string email)
    {
        var account = await player.PersistenceContext.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
        if (account is not null)
        {
            return account;
        }

        try
        {
            var newAccount = player.PersistenceContext.CreateNew<Account>();
            newAccount.LoginName = loginName;
            newAccount.EMail = email;
            newAccount.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());
            newAccount.State = AccountState.Normal;
            newAccount.RegistrationDate = DateTime.UtcNow;
            newAccount.SecurityCode = string.Empty;

            if (!await player.PersistenceContext.SaveChangesAsync().ConfigureAwait(false))
            {
                player.Logger.LogError("Failed to save new OAuth account.");
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                return null;
            }

            player.Logger.LogInformation("Created new OAuth account: {loginName}", loginName);
            return newAccount;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Error creating OAuth account.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
    }

    private async ValueTask CompleteLoginAsync(Player player, Account account, string loginName)
    {
        if (account.State == AccountState.Banned)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountBlocked)).ConfigureAwait(false);
            return;
        }

        if (account.State == AccountState.TemporarilyBanned)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.TemporaryBlocked)).ConfigureAwait(false);
            return;
        }

        await using var context = await player.PlayerState.TryBeginAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        if (!context.Allowed
            || player.GameContext is not IGameServerContext gameServerContext
            || (!account.IsTemplate && !await gameServerContext.LoginServer.TryLoginAsync(loginName, gameServerContext.Id).ConfigureAwait(false)))
        {
            context.Allowed = false;
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);

            if (player.GameContext is IGameServerContext ctx)
            {
                await ctx.EventPublisher.PlayerAlreadyLoggedInAsync(ctx.Id, loginName).ConfigureAwait(false);
            }

            return;
        }

        player.Account = account;

        if (player.IsTemplatePlayer)
        {
            foreach (var character in account.Characters)
            {
                character.Name = $"_{Interlocked.Increment(ref _templateCounter)}";
            }
        }

        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.Ok)).ConfigureAwait(false);
    }

    private async Task<string?> ValidateGoogleTokenAsync(Player player, string code)
    {
        var clientId = Environment.GetEnvironmentVariable("OAUTH_GOOGLE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("OAUTH_GOOGLE_CLIENT_SECRET");
        var redirectUri = Environment.GetEnvironmentVariable("OAUTH_REDIRECT_URI") ?? "http://localhost:54321/";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            player.Logger.LogError("Google OAuth not configured: set OAUTH_GOOGLE_CLIENT_ID and OAUTH_GOOGLE_CLIENT_SECRET env vars.");
            return null;
        }

        try
        {
            var tokenResponse = await SharedHttpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", redirectUri },
                    { "grant_type", "authorization_code" },
                })).ConfigureAwait(false);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var tokenDoc = JsonDocument.Parse(tokenContent);
            if (!tokenDoc.RootElement.TryGetProperty("id_token", out var idTokenProp))
            {
                return null;
            }

            var response = await SharedHttpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={idTokenProp.GetString()}").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);

            // Validate audience matches our client ID
            if (doc.RootElement.TryGetProperty("aud", out var audProp) && audProp.GetString() != clientId)
            {
                player.Logger.LogWarning("Google token audience mismatch.");
                return null;
            }

            return doc.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Google OAuth token validation failed.");
            return null;
        }
    }

    private async Task<string?> ValidateFacebookTokenAsync(Player player, string code)
    {
        var clientId = Environment.GetEnvironmentVariable("OAUTH_FACEBOOK_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("OAUTH_FACEBOOK_CLIENT_SECRET");

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            player.Logger.LogError("Facebook OAuth not configured: set OAUTH_FACEBOOK_CLIENT_ID and OAUTH_FACEBOOK_CLIENT_SECRET env vars.");
            return null;
        }

        try
        {
            var redirectUri = Environment.GetEnvironmentVariable("OAUTH_REDIRECT_URI") ?? "http://localhost:54321/";
            var tokenResponse = await SharedHttpClient.PostAsync(
                "https://graph.facebook.com/v12.0/oauth/access_token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "redirect_uri", redirectUri },
                    { "client_secret", clientSecret },
                    { "code", code },
                })).ConfigureAwait(false);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var tokenDoc = JsonDocument.Parse(tokenContent);
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
            {
                return null;
            }

            var response = await SharedHttpClient.GetAsync(
                $"https://graph.facebook.com/me?fields=email&access_token={accessTokenProp.GetString()}").ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Facebook OAuth token validation failed.");
            return null;
        }
    }

    private async Task<string?> ValidateAppleTokenAsync(Player player, string token)
    {
        var clientId = Environment.GetEnvironmentVariable("OAUTH_APPLE_CLIENT_ID");
        if (string.IsNullOrEmpty(clientId))
        {
            player.Logger.LogError("Apple Sign In not configured: set OAUTH_APPLE_CLIENT_ID env var.");
            return null;
        }

        try
        {
            return await AppleJwtValidator.ValidateAndGetEmailAsync(token, clientId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Apple JWT validation failed.");
            return null;
        }
    }
}
