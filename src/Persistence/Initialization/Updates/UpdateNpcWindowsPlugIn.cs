namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates NPC window types for Casino Felicia (566) and Mirage (385).
/// </summary>
[Display(Name = PlugInName, Description = PlugInDescription)]
[PlugIn]
[Guid("C1D2E3F4-A5B6-C7D8-E9F0-112233445566")]
public class UpdateNpcWindowsPlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Update NPC Windows (Casino, Mirage)";
    internal const string PlugInDescription = "Sets NPC 566 to Casino window and NPC 385 to IllusionTemple window.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.UpdateNpcWindows;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var monster in gameConfiguration.Monsters)
        {
            switch (monster.Number)
            {
                case 566:
                    monster.Designation = "Casino Felicia";
                    monster.NpcWindow = NpcWindow.Casino;
                    break;
                case 385:
                    monster.NpcWindow = NpcWindow.IllusionTemple;
                    break;
            }
        }

        return ValueTask.CompletedTask;
    }
}
