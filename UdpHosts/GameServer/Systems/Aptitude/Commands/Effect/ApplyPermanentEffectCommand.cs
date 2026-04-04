using GameServer.Data.SDB.Records.customdata;
using GameServer.GRPC;

namespace GameServer.Aptitude;

public class ApplyPermanentEffectCommand : Command, ICommand
{
    private ApplyPermanentEffectCommandDef Params;

    public ApplyPermanentEffectCommand(ApplyPermanentEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is Entities.Character.CharacterEntity character)
        {
            var characterId = character.Player.CharacterId;
            var duration = Params.DurationSeconds;

            // Check for XP Boost
            if (Params.ExperienceBoost > 0)
            {
                _ = GRPCService.ApplyCharacterBoostAsync(new GrpcGameServerAPIClient.ApplyCharacterBoostReq
                {
                    CharacterId = characterId,
                    BoostType = "xp_boost",
                    Modifier = Params.ExperienceBoost / 100.0f, // Convert percentage to multiplier
                    DurationSeconds = duration
                });
            }

            // Check for Resource Boost
            if (Params.ResourceBoost > 0)
            {
                _ = GRPCService.ApplyCharacterBoostAsync(new GrpcGameServerAPIClient.ApplyCharacterBoostReq
                {
                    CharacterId = characterId,
                    BoostType = "resource_boost",
                    Modifier = Params.ResourceBoost / 100.0f,
                    DurationSeconds = duration
                });
            }

            // Check for Reputation Boost
            if (Params.ReputationBoost > 0)
            {
                _ = GRPCService.ApplyCharacterBoostAsync(new GrpcGameServerAPIClient.ApplyCharacterBoostReq
                {
                    CharacterId = characterId,
                    BoostType = "reputation_boost",
                    Modifier = Params.ReputationBoost / 100.0f,
                    DurationSeconds = duration
                });
            }

            // Also apply the visual status effect locally
            if (Params.EffectId > 0)
            {
                context.Abilities.DoApplyEffect(Params.EffectId, context.Self, context);
            }
        }

        return true;
    }
}