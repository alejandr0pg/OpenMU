// <copyright file="LoginAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using System.Threading;
using MUnique.OpenMU.GameLogic.Views.Login;

/// <summary>
/// Action to log in a player to the game.
/// </summary>
public class LoginAction
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);
    private static int _templateCounter;

    /// <summary>
    /// Logins the specified player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public async ValueTask LoginAsync(Player player, string username, string password)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());

        if (player.FailedLoginAttempts >= MaxFailedAttempts
            && DateTime.UtcNow < player.LoginLockoutUntil)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.TemporaryBlocked)).ConfigureAwait(false);
            return;
        }

        Account? account;
        try
        {
            account = await player.PersistenceContext.GetAccountByLoginNameAsync(username, password).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Login Failed.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return;
        }

        if (account is null)
        {
            player.FailedLoginAttempts++;
            if (player.FailedLoginAttempts >= MaxFailedAttempts)
            {
                player.LoginLockoutUntil = DateTime.UtcNow + LockoutDuration;
            }

            player.Logger.LogInformation("Login failed for connection {ConnectionId}.", player.GetHashCode());
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)).ConfigureAwait(false);
        }
        else if (account.State == AccountState.Banned)
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
                    && (account.IsTemplate || await gameServerContext.LoginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false)))
                {
                    player.Account = account;
                    player.FailedLoginAttempts = 0;
                    player.Logger.LogDebug("Login successful, username: [{0}]", username);

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
                        await gameServerContext2.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext2.Id, username).ConfigureAwait(false);
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
}