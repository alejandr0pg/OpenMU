namespace MUnique.OpenMU.GameServer.MessageHandler.Login;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

[PlugIn("OAuth Login Handler", "Handles OAuth login requests")]
[Guid("12345678-1234-1234-1234-1234567890AB")]
[BelongsToGroup(LogInOutGroup.GroupKey)]
public class OAuthLoginHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly OAuthLoginAction _loginAction = new();

    public bool IsEncryptionExpected => false;

    public byte Key => 0x04; // SubCode matching Client

    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        var span = packet.Span;
        if (span.Length < 5) return;
        
        byte provider = span[4];
        string token = string.Empty;
        if (span.Length > 5)
        {
            token = Encoding.UTF8.GetString(span.Slice(5));
        }
        
        await _loginAction.LoginAsync(player, provider, token).ConfigureAwait(false);
    }
}
