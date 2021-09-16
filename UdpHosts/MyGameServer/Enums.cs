﻿namespace MyGameServer.Enums
{
    namespace Visuals
    {
        public enum PaletteType : byte
        {
            FullBody = 0x0,
            WeaponA = 0x4,
            WeaponB = 0x5,
            UnkType7 = 0x07, // Biscuit uses these
            UnkType8 = 0x08, // Biscuit uses these
            UnkType9 = 0x09  // Biscuit uses these
        }
    }

    public enum ControlPacketType : byte
    {
        CloseConnection = 0,
        MatrixAck = 2,
        ReliableGSSAck = 3,
        TimeSyncRequest = 4,
        TimeSyncResponse = 5,
        MTUProbe = 6
    }

    public enum MatrixPacketType : byte
    {
        Login = 17,
        EnterZoneAck = 18,
        ExitZoneAck = 19,
        KeyframeRequest = 20,
        DEV_ExecuteCommand = 21,
        Referee_ExecuteCommand = 22,
        RequestPause = 23,
        RequestResume = 24,
        ClientStatus = 25,
        ClientPreferences = 26,
        SynchronizationResponse = 27,
        SuperPing = 28,
        StressTestMasterObject = 29,
        ServerProfiler_RequestNames = 30,
        LogInstrumentation = 31,
        RequestSigscan = 32,
        SendEmergencyChat = 33,
        SigscanData = 34,
        WelcomeToTheMatrix = 35,
        Announce = 36,
        EnterZone = 37,
        UpdateZoneTimeSync = 38,
        HotfixLevelChanged = 39,
        ExitZone = 40,
        MatrixStatus = 41,
        MatchQueueResponse = 42,
        MatchQueueUpdate = 43,
        FoundMatchUpdate = 44,
        ChallengeJoinResponse = 45,
        ChallengeInvitation = 46,
        ChallengeInvitationSquadInfoAck = 47,
        ChallengeInvitationCancel = 48,
        ChallengeInvitationResponse = 49,
        ChallengeKicked = 50,
        ChallengeLeave = 51,
        ChallengeRosterUpdate = 52,
        ChallengeReadyCheck = 53,
        ChallengeMatchParametersUpdate = 54,
        ChallengeMatchStarting = 55,
        ForceUnqueue = 56,
        SynchronizationRequest = 57,
        GamePaused = 58,
        SuperPong = 59,
        ServerProfiler_SendNames = 60,
        ServerProfiler_SendFrame = 61,
        ZoneQueueUpdate = 62,
        DebugLagSampleSim = 63,
        DebugLagSampleClient = 64,
        LFGMatchFound = 65,
        LFGLeaderChange = 66,
        ReceiveEmergencyChat = 67,
        UpdateDevZoneInfo = 68
    }

    namespace GSS
    {
        public enum Controllers : byte
        {
            Generic = 0,
            Character = 1,
            Character_BaseController = 2,
            Character_NPCController = 3,
            Character_MissionAndMarkerController = 4,
            Character_CombatController = 5,
            Character_LocalEffectsController = 6,
            Character_SpectatorController = 7,
            Character_ObserverView = 8,
            Character_EquipmentView = 9,
            Character_AIObserverView = 10,
            Character_CombatView = 11,
            Character_MovementView = 12,
            Character_TinyObjectView = 13,
            Character_DynamicProjectileView = 14,
            Melding = 15,
            Melding_ObserverView = 16,
            MeldingBubble = 17,
            MeldingBubble_ObserverView = 18,
            AreaVisualData = 19,
            AreaVisualData_ObserverView = 20,
            AreaVisualData_ParticleEffectsView = 21,
            AreaVisualData_MapMarkerView = 22,
            AreaVisualData_TinyObjectView = 23,
            AreaVisualData_LootObjectView = 24,
            AreaVisualData_ForceShieldView = 25,
            Vehicle = 26,
            Vehicle_BaseController = 27,
            Vehicle_CombatController = 28,
            Vehicle_ObserverView = 29,
            Vehicle_CombatView = 30,
            Vehicle_MovementView = 31,
            Anchor = 32,
            Anchor_AIObserverView = 33,
            Deployable = 34,
            Deployable_ObserverView = 35,
            Deployable_NPCObserverView = 36,
            Deployable_HardpointView = 37,
            Turret = 38,
            Turret_BaseController = 39,
            Turret_ObserverView = 40,

            Outpost = 44,
            Outpost_ObserverView = 45,

            ResourceNode = 47,
            ResourceNode_ObserverView = 48,

            CarryableObject = 50,
            CarryableObject_ObserverView = 51,

            LootStoreExtension_LootObjectView = 53,

            Generic2 = 251
        }

        namespace Generic
        {
            public enum Events : byte
            {
                EncounterToUIMessage = 32,
                VotekickInitiated = 33,
                VotekickResponded = 34,
                ScoreBoardEnable = 35,
                ScoreBoardInit = 36,
                ScoreBoardSetWinner = 37,
                ScoreBoardClear = 38,
                ScoreBoardAddPlayer = 39,
                ScoreBoardRemovePlayer = 40,
                ScoreBoardUpdatePlayerStats = 41,
                ScoreBoardUpdatePlayerStatsFromStat = 42,
                ScoreBoardUpdatePlayerStatus = 43,
                MatchLoadingState = 44,
                MatchEndAck = 45,
                ServerProfiler_SendNames = 46,
                ServerProfiler_SendFrame = 47,
                TempConsoleMessage = 48,
                ReloadStaticData = 49,
                EncDebugChatMessage = 50,
                SendRadioMessage = 51,
                NpcBehaviorInfo = 52,
                NpcMonitoringLog = 53,
                NpcNavigationInfo = 54,
                NpcHostilityDebugInfo = 55,
                NpcPositionalDebugInfo = 56,
                NpcShapeDebugInfo = 57,
                NpcVoxelInfo = 58,
                DebugDrawInfo = 59,
                NpcDevCmdResponse = 60,
                DevRequestObjectPositions = 61,
                DevRequestSpawnTables = 62,
                DevRequestResourceNodeDebug = 63,
                MissionObjectiveUpdated = 64,
                MissionStatusChanged = 65,
                MissionReturnToChanged = 66,
                MissionsAvailable = 67,
                MissionActivationAck = 68,
                BountyStatusChanged = 69,
                BountyAbortAck = 70,
                BountyActivationAck = 71,
                BountyListActiveAck = 72,
                BountyListActiveDetailsAck = 73,
                BountyListAvailableAck = 74,
                BountyClearAck = 75,
                BountyClearPreviousAck = 76,
                BountyListPreviousAck = 77,
                BountyRerollResponse = 78,
                DisplayUiTrackBounty = 79,
                Achievements = 80,
                AchievementUnlocked = 81,
                TotalAchievementPoints = 82,
                MissionCompletionCounts = 83,
                ContentUnlocked = 84,
                ClientUIAction = 85,
                ArcCompletionHistoryUpdate = 86,
                JobLedgerEntriesUpdate = 87,
                TrackRecipe = 88,
                ClearTrackedRecipe = 89,
                SlotTech = 90,
                InteractableStatusChanged = 91,
                SendTipMessage = 92,
                DebugEventSample = 93,
                DebugLagPlayerSample = 94,
                DebugLagSimulationSample = 95,
                DebugLagRaiaSample = 96,
                DebugEncounterVolumes = 97,
                Trail = 98,
                EncounterDebugNotification = 99,
                EncounterUIScopeIn = 100,
                EncounterUIUpdate = 101,
                EncounterUIScopeOut = 102,
                DisplayUiNotification = 103,
                DisplayMoneyBombBanner = 104,
                SetPreloadPosition = 105,
                PlaySoundId = 106,
                PlaySoundIdAtLocation = 107,
                PlayDialogScriptMessage = 108,
                StopDialogScriptMessage = 109,
                PingMap = 110,
                PingMapMarker = 111,
                EncounterPublicInfo = 112,
                RequestActiveEncounters_Response = 113,
                ShoppingListInit = 114,
                SetClientDailyInfo = 115,
                GlobalCounterUpdate = 116,
                GlobalCounterMilestoneInfo = 117,
                ChatMessageList = 118,
                CurrentLoadoutResponse = 119,
                VendorProductsResponse = 120,
                VendorPurchaseResponse = 121,
                ConductorGlobalAnnouncement = 122
            }

            public enum Commands : byte
            {
                UIToEncounterMessage = 17,
                ServerProfiler_RequestNames = 18,
                ScheduleUpdateRequest = 19,
                LocalProximityAbilitySuccess = 20,
                RemoteProximityAbilitySuccess = 21,
                TrailRequest = 22,
                RequestLeaveZone = 23,
                RequestLogout = 24,
                RequestEncounterInfo = 25,
                RequestActiveEncounters = 26,
                VotekickRequest = 27,
                VotekickResponse = 28,
                GlobalCounterRequest = 29,
                CurrentLoadoutRequest = 30,
                VendorProductReques = 31
            }
        }

        namespace Character
        {
            internal enum Events
            {
                PartialUpdate = 1,
                KeyFrame = 4,
                MarketRequestComplete = 83,
                ReceiveWeaponTweaks = 84,
                TookDebugWeaponHitPublic = 85,
                TookDebugWeaponHit = 86,
                DebugWeaponStats = 87,
                RewardInfo = 88,
                ProgressionXpRefresh = 89,
                ReceivedDeferredXP = 90,
                PublicCombatLog = 91,
                PrivateCombatLog = 92,
                AnimationUpdated = 93,
                RaiaNPCDebugging = 94,
                WeaponProjectileFired = 95,
                AbilityProjectileFired = 96,
                ProjectileHitReported = 97,
                Stumble = 98,
                QuickChat = 99,
                ProximityTextChat = 100,
                JumpActioned = 101,
                JumpRolled = 102,
                Respawned = 103,
                CalledForHelp = 104,
                TookHit = 105,
                AlmostHit = 106,
                DealtHit = 107,
                Killed = 108,
                WarnLockTargeted = 109,
                CurrentPoseUpdate = 110,
                ConfirmedPoseUpdate = 111,
                PublicDebugMovementUpdate = 112,
                ForcedMovement = 113,
                ForcedMovementCancelled = 114,
                GrappleClimbPermission = 115,
                AbilityActivated = 116,
                AbilityFailed = 117,
                AbilityCooldowns = 118,
                NPCInteraction = 119,
                OpenMovieDialog = 120,
                PrivateDialog = 121,
                PublicDialog = 122,
                AddOrUpdateInteractives = 123,
                RemoveInteractives = 124,
                InteractionProgressed = 125,
                InteractionCompleted = 126,
                InteractedWithProgressed = 127,
                InteractedWithCompleted = 128,
                InventoryUpdate = 129,
                UnlocksUpdate = 130,
                WorkbenchUpdate = 131,
                SimulateLootPickup = 132,
                DisplayRewards = 133,
                TrackerEvent = 134,
                TrackerPulse = 135,
                PriorityTargetSet = 136,
                ResourceNodeCompletedEvent = 137,
                FoundResourceAreas = 138,
                GeographicalReportResponse = 139,
                ResourceLocationInfosRespons = 140,
                UiNamedVariableUpdate = 141,
                DuelNotification = 142,
                NewUiQuery = 143,
                UiQueryCancelled = 144,
                FetchQueueInfo_Response = 145,
                MatchQueueResponse = 146,
                ChallengeCreateResponse = 147,
                CharacterLoaded = 148,
                VendorTokenMachineResponse = 149,
                SalvageResponse = 150,
                RepairResponse = 151,
                SlotModuleResponse = 152,
                UnslotAllModulesResponse = 153,
                SlotGearResponse = 154,
                SlotVisualResponse = 155,
                SlotVisualMultiResponse = 156,
                TinkeringPlanResponse = 157,
                UnlockContentSuccess = 158,
                PushBehavior = 159,
                PopBehavior = 160,
                SelfReviveResponse = 161,
                ApplyCameraShake = 162,
                ReceivedWebUIMessage = 163,
                ExitingAttachment = 164,
                LootDistributionStartEvt = 165,
                LootDistributionUpdateEvt = 166,
                LootDistributionCompletionEvt = 167,
                ForcedWeaponSwap = 168,
                ChatPartyUpdate = 169,
                BagInventoryUpdate = 170,
                LevelUpEvent = 171,
                FactionReputationUpdate = 172,
                TutorialStateInitializeEvt = 173,
                TutorialStateUpdateEvt = 174,
                DailyLoginRewardsUpdateEvt = 175,
                Fabrication_FetchAllInstances_Response = 176,
                Fabrication_FetchAllRecipes_Response = 177,
                Fabrication_FetchInstance_Response = 178,
                Fabrication_Start_Response = 179,
                Fabrication_ApplyAction_Response = 180,
                Fabrication_GenerateResult_Response = 181,
                Fabrication_Finalize_Response = 182,
                Fabrication_Claim_Response = 183,
                PostStatEvent = 184,
                BountyRerollProductInfoUpdateEvt = 185,
                EliteLevels_InitAllFrames = 186,
                EliteLevels_InitFrame = 187,
                EliteLevels_UpgradesChanged = 188,
                EliteLevels_UnusedPointsChanged = 189,
                EliteLevels_IncreaseXp = 190,
                EliteLevels_IncreaseLevel = 191,
                EliteLevels_RerollCompleted = 192,
                EliteLevels_Initialized_Info = 193,
                FriendsListChanged = 194,
                FriendsListResponse = 195,
                PerkRespecTimerReset = 196
            }

            internal enum Commands
            {
                UiQueryResponse = 59,
                ListItemForSale = 83,
                SendMailToPlayer = 84,
                FillBuyOrder = 85,
                ToggleMarketplace = 86,
                JoinGroupLeader = 87,
                PlayerReady = 88,
                FetchQueueInfo = 89,
                MatchQueue = 90,
                ClearSavedMatchQueue = 91,
                MatchmakerSetPenalties = 92,
                MatchAccept = 93,
                ChallengeCreate = 94,
                ChallengeLeave = 95,
                ChallengeInvitation = 96,
                ChallengeInvitationResponse = 97,
                ChallengeInvitationSquadInfo = 98,
                ChallengeKick = 99,
                ChallengeSetReady = 100,
                ChallengeReadyCheck = 101,
                ChallengeSwapTeam = 102,
                ChallengeSetRoleAndTeam = 103,
                ChallengeSetMatchParameters = 104,
                ChallengeSetPowerPrivilege = 105,
                ChallengeStartMatch = 106,
                LFGCheckin = 107,
                LFGLeave = 108,
                MapOpened = 109,
                BattleframePurchased = 110,
                CollectLoot = 111,
                TempSlotAbilities = 112,
                SinAcquire_Source = 113,
                BroadcastWeaponTweaks = 114,
                MovementInput = 115,
                MovementInputFake = 116,
                FireInputIgnored = 117,
                FireBurst = 118,
                FireEnd = 119,
                FireCancel = 120,
                FireWeaponProjectile = 121,
                ReportProjectileHit = 122,
                SelectFireMode = 123,
                UseScope = 124,
                SelectWeapon = 125,
                ReloadWeapon = 126,
                CancelReload = 127,
                AcquireWeaponTarget = 128,
                LoseWeaponTarget = 129,
                NPCApplyEffect = 130,
                NPCRemoveEffect = 131,
                DockToPlayer = 132,
                ChangeLookAtTarget = 133,
                ActivateAbility = 134,
                NPCInteractWithTarget = 135,
                TargetAbility = 136,
                DeactivateAbility = 137,
                ActivateConsumable = 138,
                SetNoSpreadFlag = 139,
                ExitAttachmentRequest = 140,
                NPCSetInteractionType = 141,
                PerformEmote = 142,
                NotifyDialogScriptComplete = 143,
                PerformQuickChatCommand = 144,
                PerformTextChat = 145,
                PerformDialog = 146,
                SetDialogTag = 147,
                SetEffectsFlag = 148,
                AnimationUpdate = 149,
                SelectLoadout = 150,
                CallForHelp = 151,
                AbortCampaignMission = 152,
                TryResumeTutorialChain = 153,
                DebugMission = 154,
                AssignBounties = 155,
                AbortBounty = 156,
                ActivateBounty = 157,
                ListActiveBounties = 158,
                ListActiveBountyDetails = 159,
                ListAvailableBounties = 160,
                ClearBounties = 161,
                ClearPreviousBounties = 162,
                ListPreviousBounties = 163,
                RequestRerollBounties = 164,
                TrackBounty = 165,
                SetBountyVar = 166,
                RefreshBounties = 167,
                ListAchievements = 168,
                RequestAchievementStatus = 169,
                RequestAllAchievements = 170,
                RequestMissionAvailability = 171,
                RequestNewActivity = 172,
                RequestPushMission = 173,
                LogDirectActivityRequest = 174,
                LogActivityPush = 175,
                LogLongTimeWithoutPush = 176,
                CameraPoseUpdate = 177,
                QueueUnstuck = 178,
                VehicleCalldownRequest = 179,
                DeployableCalldownRequest = 180,
                DeployableHardpointSelection = 181,
                ResourceNodeBeaconCalldownRequest = 182,
                FindNearbyResourceAreas = 183,
                GeographicalReportRequest = 184,
                UpdateChatPartyMembers = 185,
                ClientQueryInteractionStatus = 186,
                ResourceLocationInfosRequest = 187,
                DuelRequest = 188,
                PickupCarryableObjectByProximity = 189,
                DropCarryableObject = 190,
                AiError = 191,
                AiSignal = 192,
                NonDevDebugCommand = 193,
                UpdateShoppingList = 194,
                FindServiceProvider = 195,
                ClientUIEvent = 196,
                SetMovementSimulation = 197,
                RequestRespawn = 198,
                RequestTransfer = 199,
                VendorTokenMachineRequest = 200,
                TimedDailyRewardRequest = 201,
                SalvageRequest = 202,
                RepairRequest = 203,
                SlotModuleRequest = 204,
                UnslotAllModulesRequest = 205,
                SlotGearRequest = 206,
                SlotVisualRequest = 207,
                SlotVisualMultiRequest = 208,
                RequestSelfRevive = 209,
                RequestTeleport = 210,
                RequestFrameLevelReset = 211,
                LeaveEncounterParty = 212,
                JoinSquadLeadersAr = 213,
                LeaveArc = 214,
                JobLedgerOperation = 215,
                SeatChangeRequest = 216,
                RequestTrackerUpdate = 217,
                LootDistributionSetState = 218,
                LootDistributionSetVotes = 219,
                ResetTutorialId = 220,
                DismissTutorialId = 221,
                ClaimDailyRewardItem = 222,
                ClaimDailyRewardStreak = 223,
                BuyBackPreviousDay = 224,
                AcceptRewards = 225,
                FlushRewards = 226,
                RerollEliteLevelsAwardList = 227,
                RevertAllEliteLevelsUpgrade = 228,
                SelectEliteLevelsAward = 229,
                ResetAllEliteLevelsUpgrades_Debug = 230,
                FlushCharacterCache = 231,
                RunTeamManagerCommand = 232,
                NPCCombatUpdate = 233,
                BagInventorySettings = 234,
                SetSteamUserId = 235,
                EquipExperimentalLoadout = 236,
                ExecuteTinkeringPlan = 237,
                VendorPurchaseRequest = 238,
                TutorialEventTriggeredCmd = 239,
                Fabrication_FetchAllInstances = 240,
                Fabrication_FetchAllRecipes = 241,
                Fabrication_FetchInstance = 242,
                Fabrication_Start = 243,
                Fabrication_ApplyAction = 244,
                Fabrication_GenerateResult = 245,
                Fabrication_Finalize = 246,
                Fabrication_Claim = 247,
                FriendsListRequest = 248,
                UpdateFriendStatus = 249,
                ClaimBountyRewards = 250
            }
        }

        namespace AreaVisualData
        {
            internal enum Events
            {
                PartialUpdate = 1,
                KeyFrame = 4,
                LootObjectCollected = 83,
                AudioEmitterSpawned = 84,
                ParticleEffectSpawned = 85
            }
        }

        namespace Vehicle
        {
            internal enum Commands
            {
                MovementInput = 83,
                MovementInputFake = 84,
                SinAcquire_Source = 85,
                ReceiveCollisionDamage = 86,
                ActivateAbility = 87,
                DeactivateAbility = 88,
                SetWaterLevelAndDesc = 89,
                SetEffectsFlag = 90
            }

            internal enum Events
            {
                PartialUpdate = 1,
                KeyFrame = 4,
                AbilityActivated = 83,
                AbilityFailed = 84,
                PublicCombatLog = 85,
                CurrentPoseUpdate = 86,
                TookDebugWeaponHitPublic = 87,
                ForcedMovement = 88,
                ForcedMovementCancelled = 89,
                FlipPunch = 90,
                DebugMovementUpdate = 91
            }
        }

        namespace Deployable
        {
            internal enum Events
            {
                PartialUpdate = 1,
                KeyFrame = 4,
                TookHit = 83,
                AbilityProjectileFired = 84,
                PublicCombatLog = 85
            }
        }

        namespace Turret
        {
            internal enum Events
            {
                PartialUpdate = 1,
                KeyFrame = 4,
                WeaponProjectileFired = 83
            }

            internal enum Commands
            {
                PoseUpdate = 83,
                FireBurst = 84,
                FireEnd = 85,
                FireWeaponProjectile = 86,
                ReportProjectileHit = 87
            }
        }

        namespace LootStoreExtensions
        {
            internal enum Events
            {
                PartialUpdate = 1,
                KeyFrame = 4,
                LootObjectCollected = 83
            }
        }
    }
}