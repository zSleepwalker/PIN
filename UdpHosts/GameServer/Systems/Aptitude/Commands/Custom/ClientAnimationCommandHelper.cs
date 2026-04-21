using System;
using System.Collections.Generic;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Aptitude;

internal static class ClientAnimationCommandHelper
{
    public static IReadOnlyList<CharacterEntity> ResolveCharacterTargets(Context context)
    {
        var results = new List<CharacterEntity>();
        var seen = new HashSet<ulong>();

        foreach (var target in context.Targets)
        {
            if (target is CharacterEntity character && seen.Add(character.EntityId))
            {
                results.Add(character);
            }
        }

        if (results.Count == 0 && context.Self is CharacterEntity self && seen.Add(self.EntityId))
        {
            results.Add(self);
        }

        return results;
    }

    public static bool TryApplyEmoteToken(Context context, string token, ILogger logger, string commandName, uint commandId)
    {
        var characters = ResolveCharacterTargets(context);
        if (characters.Count == 0)
        {
            logger.Information("{Command} {CommandId} skipped because there were no character targets", commandName, commandId);
            return true;
        }

        if (string.Equals(token?.Trim(), "stop", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var character in characters)
            {
                character.SetEmote(new EmoteData { Id = 0, Time = 0 });
            }

            return true;
        }

        var emoteRecord = SDBInterface.ResolveEmoteRecord(token);
        if (emoteRecord == null)
        {
            logger.Information("{Command} {CommandId} could not resolve client emote token {Token}", commandName, commandId, token ?? string.Empty);
            return true;
        }

        var emoteData = new EmoteData
        {
            Id = emoteRecord.Id,
            Time = context.Shard.CurrentTime + 60,
        };

        foreach (var character in characters)
        {
            character.SetEmote(emoteData);
        }

        return true;
    }

    public static int BroadcastAnimationUpdated(Context context, ushort value, byte mode)
    {
        var characters = ResolveCharacterTargets(context);
        if (characters.Count == 0)
        {
            return 0;
        }

        int sent = 0;

        foreach (var character in characters)
        {
            var message = new AnimationUpdated
            {
                Unk1 = value,
                Unk2 = mode,
            };

            var owner = character.IsPlayerControlled ? character.Player : null;

            if (owner is { Status: IPlayer.PlayerStatus.Playing })
            {
                owner.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
                sent++;
            }

            foreach (var remoteClient in context.Shard.Clients.Values)
            {
                if (remoteClient.Status != IPlayer.PlayerStatus.Playing)
                {
                    continue;
                }

                if (ReferenceEquals(remoteClient, owner))
                {
                    continue;
                }

                remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(message, character.EntityId);
                sent++;
            }
        }

        return sent;
    }
}