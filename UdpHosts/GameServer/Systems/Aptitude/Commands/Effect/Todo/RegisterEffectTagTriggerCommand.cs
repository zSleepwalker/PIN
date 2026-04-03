using System;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class RegisterEffectTagTriggerCommand : Command, ICommand
{
    private RegisterEffectTagTriggerCommandDef Params;

    private sealed class RegisterEffectTagTriggerActiveContext : ICommandActiveContext
    {
        public uint TagId { get; init; }
        public uint Chain { get; init; }
        public uint AbilityId { get; init; }
    }

    public RegisterEffectTagTriggerCommand(RegisterEffectTagTriggerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.Actives[this] = new RegisterEffectTagTriggerActiveContext
        {
            TagId = Params.TagId,
            Chain = Params.Chain,
            AbilityId = Params.AbilityId,
        };

        Console.WriteLine($"[RegisterEffectTagTrigger] Registered command={Params.Id}, tag={Params.TagId}, chain={Params.Chain}, ability={Params.AbilityId}");
        return true;
    }
}