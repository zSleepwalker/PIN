using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ResourceNodeBeaconCalldownCommand : Command, ICommand
{
    private ResourceNodeBeaconCalldownCommandDef Params;

    public ResourceNodeBeaconCalldownCommand(ResourceNodeBeaconCalldownCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var caller = context.Self;
        var request = context.Abilities.TryConsumeResourceNodeBeaconCalldownRequest(caller.EntityId);
        if (request != null)
        {
            var encounterMan = context.Shard.EncounterMan;

            // NodeTypeId 0 means the SDB has no per-def override; fall back to the well-known default
            // ("Default, Thumper Sifted Earth - Resource Vein 0", ID 20).
            uint nodeType = Params.NodeTypeId != 0
                ? Params.NodeTypeId
                : SDBInterface.GetDefaultThumperNodeTypeId();
            var position = request.Position;
            encounterMan.CreateThumper(nodeType, position, caller as CharacterEntity, Params);
            return true;
        }
        else
        {
            return false;
        }
    }
}