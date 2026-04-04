using System;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class RegisterHitTagTypeTriggerCommand : Command, ICommand
{
    private RegisterHitTagTypeTriggerCommandDef Params;

    private sealed class RegisterHitTagTypeTriggerActiveContext : ICommandActiveContext
    {
        public uint HitTagTypeId { get; init; }
        public uint Chain { get; init; }
        public uint AbilityId { get; init; }
    }

    public RegisterHitTagTypeTriggerCommand(RegisterHitTagTypeTriggerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.Actives[this] = new RegisterHitTagTypeTriggerActiveContext
        {
            HitTagTypeId = Params.HitTagTypeId,
            Chain = Params.Chain,
            AbilityId = Params.AbilityId,
        };

        Serilog.Log.Information($"[RegisterHitTagTypeTrigger] Registered command={Params.Id}, hitTagType={Params.HitTagTypeId}, chain={Params.Chain}, ability={Params.AbilityId}");
        return true;
    }
}