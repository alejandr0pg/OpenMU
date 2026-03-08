// <copyright file="GuildCreateAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Guild;

using MUnique.OpenMU.GameLogic.Views.Guild;

/// <summary>
/// Action to create a guild.
/// </summary>
public class GuildCreateAction
{
    private const int MinimumLevel = 100;
    private const int CreationCost = 1_000_000;

    /// <summary>
    /// Creates the guild.
    /// </summary>
    /// <param name="creator">The creator.</param>
    /// <param name="guildName">Name of the guild.</param>
    /// <param name="guildEmblem">The guild emblem.</param>
    public async ValueTask CreateGuildAsync(Player creator, string guildName, byte[] guildEmblem)
    {
        using var loggerScope = creator.Logger.BeginScope(this.GetType());
        if (creator.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            creator.Logger.LogError($"Account {creator.Account?.LoginName} not in the right state, but {creator.PlayerState.CurrentState}.");
            return;
        }

        if (creator.GuildStatus is not null)
        {
            creator.Logger.LogWarning("Player {0} tried to create guild while already in one.", creator.SelectedCharacter?.Name);
            return;
        }

        if (creator.Level < MinimumLevel)
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
            return;
        }

        var guildServer = (creator.GameContext as IGameServerContext)?.GuildServer;
        if (guildServer is null)
        {
            creator.Logger.LogError($"No guild server available");
            return;
        }

        if (await guildServer.GuildExistsAsync(guildName).ConfigureAwait(false))
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
            return;
        }

        if (!creator.TryRemoveMoney(CreationCost))
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
            return;
        }

        if (await guildServer.CreateGuildAsync(guildName, creator.SelectedCharacter!.Name, creator.SelectedCharacter.Id, guildEmblem, ((IGameServerContext)creator.GameContext).Id).ConfigureAwait(false))
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.None)).ConfigureAwait(false);
            creator.Logger.LogInformation("Guild created: [{0}], Master: [{1}]", guildName, creator.SelectedCharacter.Name);
        }
        else
        {
            creator.TryAddMoney(CreationCost);
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
        }
    }
}