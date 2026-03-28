using AeroMessages.GSS.V66.Character.Command;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Enums.GSS.Character;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers.Character;

[ControllerID(Enums.GSS.Controllers.Character_MissionAndMarkerController)]
public class MissionAndMarkerController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger;
    }

    // ── Achievements ──────────────────────────────────────────────────────────
    [MessageID((byte)Commands.RequestAllAchievements)]
    public void RequestAllAchievements(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // No backend achievement system yet. Send an empty UnlocksUpdate so the
        // client's achievement UI doesn't stall waiting for a reply.
        var response = new UnlocksUpdate
        {
            ClearExistingData = 1,
            Groups = System.Array.Empty<UnlockGroup>(),
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
    }

    [MessageID((byte)Commands.RequestAchievementStatus)]
    public void RequestAchievementStatus(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<RequestAchievementStatus>();
        if (request == null) return;
        _logger.Verbose("RequestAchievementStatus AchievementId={AchievementId} from entity {EntityId:x8}", request.AchievementId, entityId);
        // No achievement backend yet – client will have to rely on cached data.
    }

    [MessageID((byte)Commands.ListAchievements)]
    public void ListAchievements(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // No backend achievement system yet. Send empty unlock list.
        var response = new UnlocksUpdate
        {
            ClearExistingData = 1,
            Groups = System.Array.Empty<UnlockGroup>(),
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
    }

    // ── Missions ──────────────────────────────────────────────────────────────
    [MessageID((byte)Commands.RequestMissionAvailability)]
    public void RequestMissionAvailability(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<RequestMissionAvailability>();
        if (request == null) return;
        _logger.Verbose("RequestMissionAvailability Unk1={Unk1} Unk2={Unk2} from entity {EntityId:x8}", request.Unk1, request.Unk2, entityId);
        // No mission system yet.
    }

    [MessageID((byte)Commands.RequestNewActivity)]
    public void RequestNewActivity(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("RequestNewActivity from entity {EntityId:x8}", entityId);
        // No activity / mission push system yet.
    }

    [MessageID((byte)Commands.RequestPushMission)]
    public void RequestPushMission(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("RequestPushMission from entity {EntityId:x8}", entityId);
        // No mission push system yet.
    }

    [MessageID((byte)Commands.AbortCampaignMission)]
    public void AbortCampaignMission(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<AbortCampaignMission>();
        if (request == null) return;
        _logger.Verbose("AbortCampaignMission MissionId={MissionId} from entity {EntityId:x8}", request.MissionId, entityId);
        // No mission system yet.
    }

    [MessageID((byte)Commands.DebugMission)]
    public void DebugMission(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<DebugMission>();
        if (request == null) return;
        _logger.Verbose("DebugMission Unk1={Unk1} Unk2={Unk2} from entity {EntityId:x8}", request.Unk1, request.Unk2, entityId);
    }

    // ── Activity logging ──────────────────────────────────────────────────────
    [MessageID((byte)Commands.LogDirectActivityRequest)]
    public void LogDirectActivityRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("LogDirectActivityRequest from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.LogActivityPush)]
    public void LogActivityPush(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("LogActivityPush from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.LogLongTimeWithoutPush)]
    public void LogLongTimeWithoutPush(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("LogLongTimeWithoutPush from entity {EntityId:x8}", entityId);
    }

    // ── Bounties ──────────────────────────────────────────────────────────────
    [MessageID((byte)Commands.AssignBounties)]
    public void AssignBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<AssignBounties>();
        if (request == null) return;
        _logger.Verbose("AssignBounties Unk1={Unk1} from entity {EntityId:x8}", request.Unk1, entityId);
        // No bounty system yet.
    }

    [MessageID((byte)Commands.AbortBounty)]
    public void AbortBounty(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<AbortBounty>();
        if (request == null) return;
        _logger.Verbose("AbortBounty BountyId={BountyId} from entity {EntityId:x8}", request.Unk, entityId);
    }

    [MessageID((byte)Commands.ActivateBounty)]
    public void ActivateBounty(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ActivateBounty from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ListActiveBounties)]
    public void ListActiveBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ListActiveBounties from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ListActiveBountyDetails)]
    public void ListActiveBountyDetails(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ListActiveBountyDetails from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ListAvailableBounties)]
    public void ListAvailableBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ListAvailableBounties from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ClearBounties)]
    public void ClearBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ClearBounties from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ClearPreviousBounties)]
    public void ClearPreviousBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ClearPreviousBounties from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ListPreviousBounties)]
    public void ListPreviousBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ListPreviousBounties from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.RequestRerollBounties)]
    public void RequestRerollBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("RequestRerollBounties from entity {EntityId:x8}", entityId);
        // No bounty system yet; send empty reroll info so the client's UI doesn't stall.
        var response = new BountyRerollProductInfoUpdateEvt
        {
            Data = System.Array.Empty<BountyRerollProductInfoData>(),
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
    }

    [MessageID((byte)Commands.TrackBounty)]
    public void TrackBounty(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<TrackBounty>();
        if (request == null) return;
        _logger.Verbose("TrackBounty BountyId={BountyId} from entity {EntityId:x8}", request.Unk1, entityId);
    }

    [MessageID((byte)Commands.SetBountyVar)]
    public void SetBountyVar(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("SetBountyVar from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.RefreshBounties)]
    public void RefreshBounties(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("RefreshBounties from entity {EntityId:x8}", entityId);
    }

    [MessageID((byte)Commands.ClaimBountyRewards)]
    public void ClaimBountyRewards(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("ClaimBountyRewards from entity {EntityId:x8}", entityId);
        // No bounty reward system yet.
    }

    // ── Tutorial ──────────────────────────────────────────────────────────────
    [MessageID((byte)Commands.TryResumeTutorialChain)]
    public void TryResumeTutorialChain(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        _logger.Verbose("TryResumeTutorialChain from entity {EntityId:x8}", entityId);
        // Send an empty init so the client knows there is no active tutorial chain.
        var response = new TutorialStateInitializeEvt
        {
            Data = System.Array.Empty<TutorialState2x4>(),
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, player.CharacterEntity.EntityId);
    }

    [MessageID((byte)Commands.ResetTutorialId)]
    public void ResetTutorialId(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<ResetTutorialId>();
        if (request == null) return;
        _logger.Verbose("ResetTutorialId TutorialId={TutorialId} from entity {EntityId:x8}", request.Unk1, entityId);
    }

    [MessageID((byte)Commands.DismissTutorialId)]
    public void DismissTutorialId(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<DismissTutorialId>();
        if (request == null) return;
        _logger.Verbose("DismissTutorialId TutorialId={TutorialId} from entity {EntityId:x8}", request.Unk1, entityId);
    }

    [MessageID((byte)Commands.TutorialEventTriggeredCmd)]
    public void TutorialEventTriggeredCmd(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var request = packet.Unpack<TutorialEventTriggeredCmd>();
        if (request == null) return;
        _logger.Verbose("TutorialEventTriggeredCmd Unk1={Unk1} Unk2={Unk2} Unk3={Unk3} from entity {EntityId:x8}", request.Unk1, request.Unk2, request.Unk3, entityId);
    }
}