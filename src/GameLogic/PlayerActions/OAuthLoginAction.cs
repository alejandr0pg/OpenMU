namespace MUnique.OpenMU.GameLogic.PlayerActions;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for Dictionary
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Persistence;

public class OAuthLoginAction
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static int _templateCounter;

    public async ValueTask LoginAsync(Player player, byte provider, string token)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        player.Logger.LogInformation($"OAuth Login Attempt: Provider={provider}");

        string? email = null;
        string loginPrefix = "";

        try
        {
            switch (provider)
            {
                case 0: // Google
                    email = await ValidateGoogleToken(token);
                    loginPrefix = "google:";
                    break;
                case 1: // Facebook
                    email = await ValidateFacebookToken(token);
                    loginPrefix = "facebook:";
                    break;
                case 2: // Apple
                    email = ValidateAppleToken(token); // JWT decode only for now
                    loginPrefix = "apple:";
                    break;
                default:
                    player.Logger.LogWarning("Unknown OAuth provider: {0}", provider);
                    await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError));
                    return;
            }

            if (string.IsNullOrEmpty(email))
            {
                player.Logger.LogWarning("OAuth validation failed: Email not found.");
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)); // Generic error
                return;
            }

            string loginName = $"{loginPrefix}{email}";
            
            // Check if account exists
            var account = await player.PersistenceContext.GetAccountByLoginNameAsync(loginName).ConfigureAwait(false);
            if (account == null)
            {
                // Register new account
                player.Logger.LogInformation("Creating new OAuth account for: {0}", loginName);
                try
                {
                    // We need a writable context to create account. Player.PersistenceContext might be it.
                    // But we usually need to save changes.
                    
                    var newAccount = player.PersistenceContext.CreateNew<Account>();
                    newAccount.LoginName = loginName;
                    newAccount.EMail = email;
                    newAccount.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()); // Random password
                    newAccount.State = AccountState.Normal;
                    newAccount.RegistrationDate = DateTime.UtcNow;
                    newAccount.SecurityCode = "123456"; // Default
                    
                    if (!await player.PersistenceContext.SaveChangesAsync().ConfigureAwait(false))
                    {
                        player.Logger.LogError("Failed to save new account.");
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError));
                        return;
                    }
                    
                    account = newAccount;
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Error creating account.");
                    await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError));
                    return;
                }
            }

            // Proceed with login logic (copied from LoginAction)
            if (account.State == AccountState.Banned)
            {
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountBlocked)).ConfigureAwait(false);
            }
            else if (account.State == AccountState.TemporarilyBanned)
            {
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.TemporaryBlocked)).ConfigureAwait(false);
            }
            else
            {
                 try
                {
                    await using var context = await player.PlayerState.TryBeginAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
                    if (context.Allowed
                        && player.GameContext is IGameServerContext gameServerContext
                        && (account.IsTemplate || await gameServerContext.LoginServer.TryLoginAsync(loginName, gameServerContext.Id).ConfigureAwait(false)))
                    {
                        player.Account = account;
                        player.Logger.LogDebug("Login successful, username: [{0}]", loginName);

                        if (player.IsTemplatePlayer)
                        {
                            foreach (var character in account.Characters)
                            {
                                var counter = Interlocked.Increment(ref _templateCounter);
                                character.Name = $"_{counter}";
                            }
                        }

                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.Ok)).ConfigureAwait(false);
                    }
                    else
                    {
                        context.Allowed = false;
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);

                        if (player.GameContext is IGameServerContext gameServerContext2)
                        {
                            await gameServerContext2.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext2.Id, loginName).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Unexpected error during login through login server");
                    await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                }
            }

        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "OAuth Login Exception");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError));
        }
    }

    private async Task<string?> ValidateGoogleToken(string code)
    {
        try
        {
            // Exchange Authorization Code for Access Token
            var tokenResponse = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", "YOUR_GOOGLE_CLIENT_ID" }, // Should match client
                { "client_secret", "YOUR_GOOGLE_CLIENT_SECRET" },
                { "redirect_uri", "http://localhost:54321/" },
                { "grant_type", "authorization_code" }
            }));

            if (!tokenResponse.IsSuccessStatusCode) return null;

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenContent);
            if (!tokenDoc.RootElement.TryGetProperty("id_token", out var idTokenProp)) return null;
            
            var idToken = idTokenProp.GetString();

            // Verify ID Token (similar to before, but now we have the real ID Token)
            var response = await _httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString();
            }
        }
        catch { }
        return null;
    }

    private async Task<string?> ValidateFacebookToken(string code)
    {
        try
        {
            // Exchange Code for Access Token
            var tokenResponse = await _httpClient.GetAsync($"https://graph.facebook.com/v12.0/oauth/access_token?client_id=YOUR_FACEBOOK_CLIENT_ID&redirect_uri=http://localhost:54321/&client_secret=YOUR_FACEBOOK_CLIENT_SECRET&code={code}");
            
            if (!tokenResponse.IsSuccessStatusCode) return null;

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenContent);
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenProp)) return null;
            
            var accessToken = accessTokenProp.GetString();

            // Get User Data
            var response = await _httpClient.GetAsync($"https://graph.facebook.com/me?fields=email&access_token={accessToken}");
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString();
            }
        }
        catch { }
        return null;
    }

    private string? ValidateAppleToken(string token)
    {
        try
        {
            // Simple JWT payload decoding (no signature verification)
            // Token is header.payload.signature
            var parts = token.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1];
            // Fix base64 padding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString();
            }
        }
        catch { }
        return null;
    }
}
