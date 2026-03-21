namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Creates NPC 566 (Casino Felicia) definition and spawn in Devias if missing.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("D4E5F6A7-B8C9-0A1B-2C3D-4E5F6A7B8C9D")]
public class AddCasinoFeliciaPlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Add Casino Felicia NPC";

    internal const string PlugInDescription =
        "Creates NPC 566 (Casino Felicia) with Casino window and spawns her in Devias.";

    private const short FeliciaNumber = 566;
    private const byte DeviasMapNumber = 2;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddCasinoFelicia;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var felicia = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == FeliciaNumber);
        if (felicia is null)
        {
            felicia = context.CreateNew<MonsterDefinition>();
            felicia.Number = FeliciaNumber;
            felicia.Designation = "Casino Felicia";
            felicia.ObjectKind = NpcObjectKind.PassiveNpc;
            felicia.SetGuid(felicia.Number);
            gameConfiguration.Monsters.Add(felicia);
        }

        felicia.NpcWindow = NpcWindow.Casino;

        this.EnsureDeviasSpawn(context, gameConfiguration, felicia);
        return ValueTask.CompletedTask;
    }

    private void EnsureDeviasSpawn(
        IContext context,
        GameConfiguration gameConfiguration,
        MonsterDefinition felicia)
    {
        var devias = gameConfiguration.Maps.FirstOrDefault(m => m.Number == DeviasMapNumber);
        if (devias is null)
        {
            return;
        }

        if (devias.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == FeliciaNumber))
        {
            return;
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.SetGuid(37);
        spawn.GameMap = devias;
        spawn.MonsterDefinition = felicia;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthEast;
        spawn.X1 = 204;
        spawn.X2 = 204;
        spawn.Y1 = 61;
        spawn.Y2 = 61;
        devias.MonsterSpawns.Add(spawn);
    }
}
