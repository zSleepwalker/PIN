using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AeroMessages.Common;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.GRPC;
using LoadoutVisualType = AeroMessages.GSS.V66.Character.LoadoutConfig_Visual.LoadoutVisualType;

namespace GameServer.Data;

public class CharacterInventory
{
    private static readonly System.Text.Json.JsonSerializerOptions LoadoutVisualJsonOptions = new()
    {
        IncludeFields = true,
    };

    private sealed class FrameProgressionState
    {
        public uint ChassisId { get; init; }
        public uint Level { get; init; }
        public uint CurrentXp { get; init; }
        public uint LifetimeXp { get; init; }
        public uint EliteLevel { get; init; }
        public uint EliteXp { get; init; }
        public uint ElitePoints { get; init; }
    }

    public bool EnablePartialUpdates = true;

    // Mirror of client lib_Battleframes.lua cert grants per chassis.
    private static readonly Dictionary<uint, uint[]> FrameCertsByChassis = new()
    {
        [76164] = [732],
        [76133] = [733, 732],
        [76132] = [734, 732],

        [75775] = [735],
        [76337] = [736, 735],
        [76338] = [737, 735],

        [75772] = [741],
        [76331] = [742, 741],
        [76332] = [743, 741],
        [82360] = [748, 741],

        [75774] = [738],
        [76335] = [739, 738],
        [76336] = [740, 738],

        [75773] = [744],
        [76333] = [745, 744],
        [76334] = [746, 744],
    };

    // Maps unambiguous AbilitySlotType byte values to their canonical LoadoutSlotType storage values.
    // Derived from CharacterLoadout.AbilityToLoadoutSlotMap, excluding entries whose AbilitySlotType
    // byte value collides with a valid LoadoutSlotType value:
    //   Ability2 (1) collides with LoadoutSlotType.Primary (1)
    //   Ability3 (2) collides with LoadoutSlotType.Secondary (2)
    //   AbilityMedical (6) collides with LoadoutSlotType.AbilityHKM (6)
    // Those three are resolved contextually in NormalizeSlotIndexWithItemContext.
    private static readonly Dictionary<byte, byte> LegacyAbilitySlotToLoadoutSlot = new()
    {
        { (byte)AbilitySlotType.Ability1, (byte)LoadoutSlotType.Ability1 },
        { (byte)AbilitySlotType.AbilityHKM, (byte)LoadoutSlotType.AbilityHKM },
        { (byte)AbilitySlotType.AbilityAux, (byte)LoadoutSlotType.GearAuxWeapon },
        { (byte)AbilitySlotType.AbilityCalldownVehicle, (byte)LoadoutSlotType.Vehicle },
        { (byte)AbilitySlotType.AbilityCalldownGlider, (byte)LoadoutSlotType.Glider },
    };

    private Dictionary<ulong, Item> _items; // By guid
    private Dictionary<uint, Resource> _resources; // By typeid
    private Dictionary<int, Loadout> _loadouts; // By loadoutid
    private Dictionary<uint, FrameProgressionState> _frameProgressions; // By chassis id

    private IShard _shard;
    private INetworkClient _player;
    private CharacterEntity _character;

    public CharacterInventory(IShard shard, INetworkClient player, CharacterEntity character)
    {
        _shard = shard;
        _player = player;
        _character = character;
        _items = new();
        _resources = new();
        _loadouts = new();
        _frameProgressions = new();
        Unlocks = new CharacterUnlocks(shard, player, character);
    }

    public CharacterUnlocks Unlocks { get; private set; }

    public static LoadoutSlotType NormalizeRequestedLoadoutSlot(byte rawSlotIndex)
    {
        // Request path has no item context. Keep direct legacy mappings only.
        return (LoadoutSlotType)LegacyAbilitySlotToLoadoutSlot.GetValueOrDefault(rawSlotIndex, rawSlotIndex);
    }

    public void LoadHardcodedInventory()
    {
        foreach (uint item in HardcodedCharacterData.FallbackInventoryItems)
        {
            CreateItem(item);
        }

        foreach ((uint resource, uint quantity) in HardcodedCharacterData.FallbackInventoryResources)
        {
            AddResource(resource, quantity);
        }

        foreach (var data in HardcodedCharacterData.TempHardcodedLoadouts)
        {
            HardcodedCharacterData.GenerateLoadoutAndItems(this, data);
        }

        foreach ((uint createId, uint chassisId) in HardcodedCharacterData.TempCharCreateLoadouts)
        {
            HardcodedCharacterData.GenerateCharCreateLoadoutAndItems(this, createId, chassisId);
        }
    }

    public void LoadDatabaseInventory(GrpcGameServerAPIClient.CharacterInventoryResponse inventoryData)
    {
        _items.Clear();
        _resources.Clear();
        _loadouts.Clear();
        _frameProgressions.Clear();

        foreach (var item in inventoryData.Items)
        {
            var dbItem = new Item
            {
                SdbId = item.SdbId,
                GUID = item.Guid,
                SubInventory = GetInventoryTypeByItemTypeId(item.SdbId),
                Durability = 1000,
                DynamicFlags = 0,
                TimestampEpoch = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Modules = Array.Empty<uint>(),
                Unk1 = 0,
                Unk3 = 0,
                Unk4 = 0,
                Unk5 = 0,
                Unk6 = Array.Empty<ItemUnkData>(),
                Unk7 = 0,
            };
            _items.Add(item.Guid, dbItem);
        }

        foreach (var resource in inventoryData.Resources)
        {
            var dbResource = new Resource
            {
                SdbId = resource.SdbId,
                Quantity = resource.Quantity,
                SubInventory = GetInventoryTypeByItemTypeId(resource.SdbId),
                TextKey = string.Empty,
                Unk2 = 0
            };
            _resources.Add(resource.SdbId, dbResource);
        }

        foreach (var loadoutData in inventoryData.Loadouts)
        {
            bool loadoutWasRepaired = false;

            _frameProgressions[(uint)loadoutData.ChassisSdbId] = new FrameProgressionState
            {
                ChassisId = (uint)loadoutData.ChassisSdbId,
                Level = ClampToUInt(loadoutData.Level, 1),
                CurrentXp = ClampToUInt(loadoutData.CurrentXp),
                LifetimeXp = ClampToUInt(loadoutData.LifetimeXp),
                EliteLevel = ClampToUInt(loadoutData.EliteLevel),
                EliteXp = ClampToUInt(loadoutData.EliteXp),
                ElitePoints = ClampToUInt(loadoutData.ElitePoints),
            };

            // Parse the loadout from the database format
            var loadout = new Loadout
            {
                FrameLoadoutId = loadoutData.LoadoutId,
                ChassisID = (uint)loadoutData.ChassisSdbId,
                LoadoutName = $"Loadout {loadoutData.LoadoutId}",
                LoadoutType = "battleframe",
                LoadoutConfigs = CreateDefaultLoadoutConfigs(),
            };

            if (!string.IsNullOrEmpty(loadoutData.Visuals))
            {
                try
                {
                    var visuals = System.Text.Json.JsonSerializer.Deserialize<LoadoutConfig_Visual[]>(loadoutData.Visuals, LoadoutVisualJsonOptions);
                    if (visuals != null)
                    {
                        loadout.LoadoutConfigs[0].Visuals = visuals;
                    }
                }
                catch (Exception ex)
                {
                    _shard.Logger.Error(ex, "Failed to parse loadout visuals for {charId}", _character.EntityId);
                }
            }

            if (!string.IsNullOrEmpty(loadoutData.SlottedItems))
            {
                try
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<Dictionary<byte, ulong>>(loadoutData.SlottedItems);
                    if (items != null)
                    {
                        var resolvedItems = new List<LoadoutConfig_Item>();
                        foreach (var item in items)
                        {
                            if (TryResolveStoredLoadoutItemId(item.Value, out var resolvedGuid))
                            {
                                resolvedItems.Add(new LoadoutConfig_Item { SlotIndex = item.Key, ItemGUID = resolvedGuid });
                                if (resolvedGuid != item.Value)
                                {
                                    loadoutWasRepaired = true;
                                }
                            }
                            else
                            {
                                _shard.Logger.Warning("Skipping unresolved DB loadout item reference {storedValue} in slot {slot} for {charId}", item.Value, item.Key, _character.EntityId);
                            }
                        }

                        loadout.LoadoutConfigs[0].Items = NormalizeLegacyLoadoutSlots(resolvedItems.ToArray(), out bool slotIdsUpdated);
                        if (slotIdsUpdated)
                        {
                            loadoutWasRepaired = true;
                        }

                        // Auto-equip Vehicle and Glider if they are in the bag but not yet slotted.
                        // This migrates existing characters that were created before loadout slot
                        // persistence was added for these two item sub-types.
                        const ushort VehicleSubtype = 83;
                        const ushort GliderSubtype = 3709;
                        const byte VehicleUiCategory = 6;
                        const byte GliderUiCategory = 9;
                        bool hasVehicleSlot = resolvedItems.Any(r => r.SlotIndex == (byte)LoadoutSlotType.Vehicle);
                        bool hasGliderSlot = resolvedItems.Any(r => r.SlotIndex == (byte)LoadoutSlotType.Glider);
                        if (!hasVehicleSlot || !hasGliderSlot)
                        {
                            var equipList = new List<LoadoutConfig_Item>(resolvedItems);
                            foreach (var (guid, itm) in _items)
                            {
                                var rootInfo = SDBInterface.GetRootItem(itm.SdbId);
                                var abilityModule = SDBInterface.GetAbilityModule(itm.SdbId);
                                if (rootInfo == null)
                                {
                                    continue;
                                }

                                bool matchesVehicle = rootInfo.ItemSubtype == VehicleSubtype
                                    || (abilityModule != null && abilityModule.UiCategory == VehicleUiCategory);
                                bool matchesGlider = rootInfo.ItemSubtype == GliderSubtype
                                    || (abilityModule != null && abilityModule.UiCategory == GliderUiCategory);

                                if (!hasVehicleSlot && matchesVehicle)
                                {
                                    equipList.Add(new LoadoutConfig_Item { SlotIndex = (byte)LoadoutSlotType.Vehicle, ItemGUID = guid });
                                    var equipped = itm;
                                    equipped.DynamicFlags = (byte)(equipped.DynamicFlags | (byte)ItemDynamicFlags.IsEquipped);
                                    _items[guid] = equipped;
                                    hasVehicleSlot = true;
                                    loadoutWasRepaired = true;
                                    _shard.Logger.Information("Auto-equipped vehicle item {sdbId} (guid {guid}) into Vehicle slot for {charId}", itm.SdbId, guid, _character.EntityId);
                                }
                                else if (!hasGliderSlot && matchesGlider)
                                {
                                    equipList.Add(new LoadoutConfig_Item { SlotIndex = (byte)LoadoutSlotType.Glider, ItemGUID = guid });
                                    var equipped = itm;
                                    equipped.DynamicFlags = (byte)(equipped.DynamicFlags | (byte)ItemDynamicFlags.IsEquipped);
                                    _items[guid] = equipped;
                                    hasGliderSlot = true;
                                    loadoutWasRepaired = true;
                                    _shard.Logger.Information("Auto-equipped glider item {sdbId} (guid {guid}) into Glider slot for {charId}", itm.SdbId, guid, _character.EntityId);
                                }

                                if (hasVehicleSlot && hasGliderSlot)
                                {
                                    break;
                                }
                            }

                            loadout.LoadoutConfigs[0].Items = equipList.ToArray();
                        }

                        // Mark all slotted items as equipped for client inventory/state sync.
                        // DB inventory rows do not carry DynamicFlags, so we derive it here from slot membership.
                        foreach (var slotted in loadout.LoadoutConfigs[0].Items)
                        {
                            if (_items.TryGetValue(slotted.ItemGUID, out var equippedItem))
                            {
                                equippedItem.DynamicFlags = (byte)(equippedItem.DynamicFlags | (byte)ItemDynamicFlags.IsEquipped);
                                _items[slotted.ItemGUID] = equippedItem;
                            }
                        }

                        // Keep loadout visuals aligned with utility equipment so UI panels that
                        // consume visual slots can show equipped vehicle/glider correctly.
                        SyncUtilityVisualsFromLoadoutSlots(loadout);
                    }
                }
                catch (Exception ex)
                {
                    _shard.Logger.Error(ex, "Failed to parse loadout items for {charId}", _character.EntityId);
                }
            }

            AddLoadout(loadout);

            // Self-heal stale loadout rows so subsequent logins stop emitting recovery warnings.
            if (loadoutWasRepaired)
            {
                PersistLoadoutToDatabase(loadout);
            }
        }

        Unlocks.LoadPersistedUnlocks(inventoryData.Unlocks);
        Unlocks.RebuildAutoUnlocks(_loadouts.Values, _items.Values.Select(item => item.SdbId), _character.Level);
    }

    public bool ConsumeItem(uint sdbId, uint quantity)
    {
        // Find items in inventory with this SDB ID
        var itemsToConsume = _items.Values.Where(i => i.SdbId == sdbId).Take((int)quantity).ToList();
        if (itemsToConsume.Count < quantity)
        {
            return false;
        }

        foreach (var item in itemsToConsume)
        {
            _items.Remove(item.GUID);

            // Send update to client (TODO: implement SendItemRemove if needed, or send full inventory)
        }

        // Inform the backend database asynchronously to consume the items so it persists state
        _ = GRPCService.ConsumeCharacterItemAsync(new GrpcGameServerAPIClient.ConsumeItemReq
        {
            CharacterId = ((NetworkPlayer)_player).CharacterId + 0xFE,
            SdbId = sdbId,
            Quantity = quantity
        });

        // For now, send full inventory to be safe, though a partial remove would be better
        SendFullInventory();

        return true;
    }

    public async Task RefreshFromDatabase()
    {
        var charId = (long)((NetworkPlayer)_player).CharacterId + 0xFE;
        var inventoryData = await GRPCService.GetCharacterInventoryAsync(charId);

        LoadDatabaseInventory(inventoryData);
        SendFullInventory();
        SendCertificateUnlocksUpdate();
        SendBattleframeProgressionUpdate();
    }

    public void SendBattleframeProgressionUpdate(uint currentFrameChassisId = 0)
    {
        if (_frameProgressions.Count == 0)
        {
            return;
        }

        uint resolvedCurrentFrameId = currentFrameChassisId;
        if (resolvedCurrentFrameId == 0)
        {
            resolvedCurrentFrameId = _character.CurrentLoadout?.ChassisID ?? 0;
        }

        if (resolvedCurrentFrameId == 0 || !_frameProgressions.ContainsKey(resolvedCurrentFrameId))
        {
            resolvedCurrentFrameId = _frameProgressions.Keys.FirstOrDefault();
        }

        var eliteInfo = new EliteLevels_Initialized_Info
        {
            LevelsPerRare = 1,
            AwardFrameMinLevel = 1,
            RevertCost = 0,
            RerollCost = 0,
        };

        var eliteFrames = _frameProgressions.Values
            .OrderBy(frame => frame.ChassisId)
            .Select(frame => new EliteFrameInfoAll
            {
                ChassisId_1 = frame.ChassisId,
                ChassisId_2 = frame.ChassisId,
                EliteRank = frame.EliteLevel,
                EliteXP = frame.EliteXp,
                ElitePoints = frame.ElitePoints,
                AvailableUpgrades = Array.Empty<EliteAvailableUpgradeInfo>(),
                PreviousUpgrades = Array.Empty<ElitePreviousUpgradeInfo>(),
            })
            .ToArray();

        var progressionFrames = _frameProgressions.Values
            .OrderBy(frame => frame.ChassisId)
            .Select(frame => new ProgressionFrameInfo
            {
                ChassisID = frame.ChassisId,
                XpValue1 = frame.CurrentXp,
                XpValue2 = frame.LifetimeXp,
                CurrentLevel = frame.Level,
                Unk = frame.EliteLevel,
            })
            .ToArray();

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(eliteInfo, _character.EntityId);
        _player.NetChannels[ChannelType.ReliableGss].SendMessage(new EliteLevels_InitAllFrames
        {
            CurrentFrame_Id = resolvedCurrentFrameId,
            Frames = eliteFrames,
        }, _character.EntityId);
        _player.NetChannels[ChannelType.ReliableGss].SendMessage(new ProgressionXPRefresh
        {
            Frames = progressionFrames,
        }, _character.EntityId);
    }

    public int GetLoadoutIdForChassis(uint chassisId)
    {
        foreach (var (loadoutId, loadout) in _loadouts)
        {
            if (loadout.ChassisID == chassisId)
            {
                return loadoutId;
            }
        }

        return 0;
    }

    private static uint ClampToUInt(long value, uint defaultValue = 0)
    {
        if (value < 0)
        {
            return defaultValue;
        }

        return value > uint.MaxValue ? uint.MaxValue : (uint)value;
    }

    public int GetAnyLoadoutId()
    {
        if (_loadouts.Count == 0)
        {
            return 0;
        }

        return _loadouts.Keys.Min();
    }

    /// <summary>
    /// Get a loadout from the inventory by loadoutId
    /// </summary>
    /// <param name="loadoutId">The id of the loadout to get</param>
    /// <returns>The loadout data as LoadoutReferenceData or null if the loadoutId was invalid</returns>
    public LoadoutReferenceData GetLoadoutReferenceData(int loadoutId)
    {
        if (!_loadouts.ContainsKey(loadoutId))
        {
            return null;
        }

        var loadout = _loadouts[loadoutId];

        var refData = new LoadoutReferenceData()
        {
            LoadoutId = loadoutId,
            ChassisId = loadout.ChassisID,
        };

        NormalizeLoadoutForSerialization(ref loadout);

        var pveConfig = loadout.LoadoutConfigs[0];
        var pvpConfig = loadout.LoadoutConfigs[1];
        refData.Visuals = (pveConfig.Visuals ?? Array.Empty<LoadoutConfig_Visual>()).ToArray();
        foreach (var itemRef in pveConfig.Items)
        {
            if (_items.TryGetValue(itemRef.ItemGUID, out var item))
            {
                refData.SlottedItemsPvE[(LoadoutSlotType)itemRef.SlotIndex] = item.SdbId;
            }
        }

        foreach (var itemRef in pvpConfig.Items)
        {
            if (_items.TryGetValue(itemRef.ItemGUID, out var item))
            {
                refData.SlottedItemsPvP[(LoadoutSlotType)itemRef.SlotIndex] = item.SdbId;
            }
        }

        return refData;
    }

    public ulong CreateItem(uint sdbId)
    {
        ulong guid = _shard.GetNextGuid((byte)GuidService.AdditionalTypes.Item);
        Item item = new Item()
        {
            SdbId = sdbId,
            GUID = guid,
            SubInventory = GetInventoryTypeByItemTypeId(sdbId),
            Durability = 1000,
            DynamicFlags = 0,
            TimestampEpoch = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Modules = Array.Empty<uint>(),
            Unk1 = 0,
            Unk3 = 0,
            Unk4 = 0,
            Unk5 = 0,
            Unk6 = Array.Empty<ItemUnkData>(),
            Unk7 = 0,
        };

        _items.Add(guid, item);
        SendItemUpdate(guid);
        return guid;
    }

    public void AddResource(uint sdbId, uint quantity)
    {
        if (!_resources.ContainsKey(sdbId))
        {
            Resource resource = new Resource()
            {
                Quantity = 0,
                SdbId = sdbId,
                SubInventory = GetInventoryTypeByItemTypeId(sdbId),
                TextKey = string.Empty,
                Unk2 = 0,
            };

            _resources.Add(sdbId, resource);
        }

        var res = _resources[sdbId];
        res.Quantity += quantity;
        _resources[sdbId] = res;
        SendResourceUpdate(sdbId);
    }

    public bool ConsumeResource(uint sdbId, uint cost)
    {
        if (!_resources.ContainsKey(sdbId))
        {
            return false;
        }

        var res = _resources[sdbId];
        if (res.Quantity < cost)
        {
            return false;
        }
        else
        {
            res.Quantity -= cost;

            if (res.Quantity > 0)
            {
                _resources[sdbId] = res;
            }
            else
            {
                _resources.Remove(sdbId);
            }

            SendResourceUpdate(sdbId);

            // Inform the backend database asynchronously to consume the resource so it persists state
            _ = GRPCService.ConsumeCharacterResourceAsync(new GrpcGameServerAPIClient.ConsumeResourceReq
            {
                CharacterId = ((NetworkPlayer)_player).CharacterId + 0xFE,
                SdbId = sdbId,
                Quantity = cost
            });

            return true;
        }
    }

    public uint GetResourceQuantity(uint sdbId)
    {
        return _resources.TryGetValue(sdbId, out var value) ? value.Quantity : 0;
    }

    public void AddLoadout(Loadout loadout)
    {
        NormalizeLoadoutForSerialization(ref loadout);
        _loadouts.Add(loadout.FrameLoadoutId, loadout);
    }

    public void SendCertificateUnlocksUpdate()
    {
        if (_player is NetworkPlayer networkPlayer && networkPlayer.Status != IPlayer.PlayerStatus.Playing)
        {
            // Avoid sending unlock events before the backend unlock manager initializes.
            return;
        }

        Unlocks.SendUnlocksUpdate();
    }

    public bool TryGetLoadout(int loadoutId, out Loadout loadout)
    {
        if (!_loadouts.TryGetValue(loadoutId, out loadout))
        {
            return false;
        }

        NormalizeLoadoutForSerialization(ref loadout);
        return true;
    }

    public void SendFullInventory()
    {
        if (_items.Count > ((255 * 3) - 1))
        {
            throw new NotImplementedException("Too many items in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        if (_resources.Count > 254)
        {
            throw new NotImplementedException("Too many resources in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        if (_loadouts.Count > 254)
        {
            throw new NotImplementedException("Too many loadouts in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        foreach (var loadoutId in _loadouts.Keys.ToArray())
        {
            NormalizeAndStoreLoadout(loadoutId);
        }

        var update = new InventoryUpdate()
        {
            ClearExistingData = 1,
            ItemsPart1Length = 0,
            ItemsPart1 = Array.Empty<Item>(),
            ItemsPart2Length = 0,
            ItemsPart2 = Array.Empty<Item>(),
            ItemsPart3Length = 0,
            ItemsPart3 = Array.Empty<Item>(),
            Resources = _resources.Values.ToArray(),
            Loadouts = _loadouts.Values.ToArray(),
            Unk = 1,
            SecondItems = Array.Empty<Item>(),
            SecondResources = Array.Empty<Resource>()
        };

        if (_items.Count >= 255)
        {
            var tmp = _items.Values.ToArray();
            update.ItemsPart1Length = 255;
            update.ItemsPart1Full = tmp[0..255];
            if (_items.Count >= 510)
            {
                update.ItemsPart2Length = 255;
                update.ItemsPart2Full = tmp[255..510];

                update.ItemsPart3Length = (byte)tmp[510..^0].Length;
                update.ItemsPart3 = tmp[510..^0];
            }
            else
            {
                update.ItemsPart2Length = (byte)tmp[255..^0].Length;
                update.ItemsPart2 = tmp[255..^0];
            }
        }
        else
        {
            update.ItemsPart1Length = (byte)_items.Count;
            update.ItemsPart1 = _items.Values.ToArray();
        }

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendItemUpdate(ulong guid)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var item = _items[guid];
        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = 1,
            ItemsPart1 =
            [
                item
            ],
            ItemsPart2Length = 0,
            ItemsPart2 = Array.Empty<Item>(),
            ItemsPart3Length = 0,
            ItemsPart3 = Array.Empty<Item>(),
            Resources = Array.Empty<Resource>(),
            Loadouts = Array.Empty<Loadout>(),
            Unk = 1,
            SecondItems = Array.Empty<Item>(),
            SecondResources = Array.Empty<Resource>()
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendResourceUpdate(uint sdbId)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var resource = _resources[sdbId];
        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = 0,
            ItemsPart1 = Array.Empty<Item>(),
            ItemsPart2Length = 0,
            ItemsPart2 = Array.Empty<Item>(),
            ItemsPart3Length = 0,
            ItemsPart3 = Array.Empty<Item>(),
            Resources =
            [
                resource
            ],
            Loadouts = Array.Empty<Loadout>(),
            Unk = 1,
            SecondItems = Array.Empty<Item>(),
            SecondResources = Array.Empty<Resource>()
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendEquipmentChanges(ulong oldItemGuid, ulong newItemGuid)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        foreach (var loadoutId in _loadouts.Keys.ToArray())
        {
            NormalizeAndStoreLoadout(loadoutId);
        }

        var itemChanges = new Item[]
        {
        };

        if (oldItemGuid != 0)
        {
            var oldItem = _items[oldItemGuid];
            itemChanges = itemChanges.Append(oldItem).ToArray();
        }

        if (newItemGuid != 0)
        {
            var newItem = _items[newItemGuid];
            itemChanges = itemChanges.Append(newItem).ToArray();
        }

        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = (byte)itemChanges.Length,
            ItemsPart1 = itemChanges,
            ItemsPart2Length = 0,
            ItemsPart2 = Array.Empty<Item>(),
            ItemsPart3Length = 0,
            ItemsPart3 = Array.Empty<Item>(),
            Resources = Array.Empty<Resource>(),
            Loadouts = _loadouts.Values.ToArray(),
            Unk = 1,
            SecondItems = Array.Empty<Item>(),
            SecondResources = Array.Empty<Resource>()
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void EquipItemByGUID(int loadoutId, LoadoutSlotType slot, ulong guid)
    {
        slot = NormalizeRequestedSlotForItem(slot, guid);

        ulong changedOldItemGUID = 0;
        ulong changedNewItemGUID = guid;

        NormalizeAndStoreLoadout(loadoutId);

        // Unequip old Item (if any)
        if (_loadouts[loadoutId].LoadoutConfigs[0].Items.Any((e) => e.SlotIndex == (byte)slot))
        {
            // Set Item to unequipped
            var oldItemGUID = _loadouts[loadoutId].LoadoutConfigs[0].Items.First((e) => e.SlotIndex == (byte)slot).ItemGUID;
            changedOldItemGUID = oldItemGUID;
            var oldItem = _items[oldItemGUID];
            oldItem.DynamicFlags = (byte)(oldItem.DynamicFlags ^ (byte)ItemDynamicFlags.IsEquipped);
            _items[oldItemGUID] = oldItem;

            // Update CurrentLoadout
            _character.CurrentLoadout.SlottedItems[slot] = 0;

            // Update LoadoutConfigs
            _loadouts[loadoutId].LoadoutConfigs[0].Items = _loadouts[loadoutId].LoadoutConfigs[0].Items
                .Where(e => e.SlotIndex != (byte)slot).ToArray();
        }

        // Equip new item (if any)
        if (guid != 0)
        {
            // VALIDATION: For ability slots, verify the item can be equipped on the current frame
            if ((slot == LoadoutSlotType.Ability1 || slot == LoadoutSlotType.Ability2 ||
                slot == LoadoutSlotType.Ability3 || slot == LoadoutSlotType.AbilityHKM) && guid != 0)
            {
                var tempItem = _items[guid];
                uint currentFrameChassisId = _character.CurrentLoadout?.ChassisID ?? 0;

                if (!Unlocks.CanEquipAbilityOnFrame(tempItem.SdbId, currentFrameChassisId))
                {
                    _shard?.Logger?.Warning(
                        "[INVENTORY] EquipItemByGUID: REJECTED - Ability {itemId} cannot be equipped on frame {frameId}",
                        tempItem.SdbId, currentFrameChassisId);
                    return;
                }
            }
        }

        {
            // Update Item to Equipped
            var item = _items[guid];
            item.DynamicFlags = (byte)(item.DynamicFlags | (byte)ItemDynamicFlags.IsEquipped);
            _items[guid] = item;

            // Update CurrentLoadout
            _character.CurrentLoadout.SlottedItems[slot] = item.SdbId;

            // Update LoadoutConfig
            _loadouts[loadoutId].LoadoutConfigs[0].Items = _loadouts[loadoutId].LoadoutConfigs[0].Items.Append(new LoadoutConfig_Item() { ItemGUID = guid, SlotIndex = (byte)slot }).ToArray();
        }

        // Update StaticInfo when visuals are changed
        var equippedSdbId = (guid != 0) ? _items[guid].SdbId : 0;
        switch (slot)
        {
            case LoadoutSlotType.Glider:
                _character.SetStaticInfo(_character.StaticInfo with { LoadoutGlider = equippedSdbId });
                break;
            case LoadoutSlotType.Vehicle:
                _character.SetStaticInfo(_character.StaticInfo with { LoadoutVehicle = equippedSdbId });
                break;
        }

        SendEquipmentChanges(changedOldItemGUID, changedNewItemGUID);

        // Persist the updated loadout slots to the database via gRPC.
        if (_loadouts.TryGetValue(loadoutId, out var updatedLoadout))
        {
            NormalizeLoadoutForSerialization(ref updatedLoadout);

            var slottedItemsDict = updatedLoadout.LoadoutConfigs[0].Items
                .ToDictionary(x => x.SlotIndex, x => x.ItemGUID);
            var slottedItemsJson = System.Text.Json.JsonSerializer.Serialize(slottedItemsDict);
            var visualsJson = SerializeVisualsForPersistence(updatedLoadout.LoadoutConfigs[0].Visuals ?? Array.Empty<LoadoutConfig_Visual>());
            ulong charGuid = ((NetworkPlayer)_player).CharacterId + 0xFE;
            _ = GRPCService.SaveCharacterLoadoutAsync(charGuid, loadoutId, (int)updatedLoadout.ChassisID, visualsJson, slottedItemsJson);
        }
    }

    public void EquipVisualBySdbId(int loadoutId, LoadoutVisualType visual, LoadoutSlotType slot, uint sdb_id)
    {
        NormalizeAndStoreLoadout(loadoutId);

        // Unequip old item (if any)
        if (_loadouts[loadoutId].LoadoutConfigs[0].Visuals.Any(i => i.VisualType == visual))
        {
            // Update Visuals
            _loadouts[loadoutId].LoadoutConfigs[0].Visuals = _loadouts[loadoutId].LoadoutConfigs[0].Visuals
                .Where(e => e.VisualType != visual).ToArray();
        }

        // Equip new item (if any)
        if (sdb_id != 0)
        {
            // Update Visuals
            _loadouts[loadoutId].LoadoutConfigs[0].Visuals = _loadouts[loadoutId].LoadoutConfigs[0].Visuals.Append(new LoadoutConfig_Visual() { ItemSdbId = sdb_id, VisualType = visual, Data1 = 0, Data2 = 0, Transform = Array.Empty<float>() }).ToArray();
            var item = _items.First(e => e.Value.SdbId == sdb_id).Value;
        }

        var equippedGUID = (sdb_id != 0) ? _items.First(e => e.Value.SdbId == sdb_id).Value.GUID : 0;
        EquipItemByGUID(loadoutId, slot, equippedGUID);
    }

    public bool TrySetLoadoutVisuals(int loadoutId, uint configId, LoadoutConfig_Visual[] visuals, out LoadoutConfig_Visual[] mergedVisuals)
    {
        mergedVisuals = Array.Empty<LoadoutConfig_Visual>();

        ulong charGuid = ((NetworkPlayer)_player).CharacterId + 0xFE;
        Serilog.Log.Information(
            "PAINT_DEBUG TrySetLoadoutVisuals START: char={CharGuid}, loadout={LoadoutId}, config={ConfigId}, incomingCount={Count}, incoming={Visuals}",
            charGuid, loadoutId, configId,
            visuals?.Length ?? 0,
            SerializeVisualsForPersistence(visuals ?? Array.Empty<LoadoutConfig_Visual>()));

        if (!_loadouts.TryGetValue(loadoutId, out var loadout))
        {
            Serilog.Log.Warning("PAINT_DEBUG TrySetLoadoutVisuals: loadout {LoadoutId} not found for char={CharGuid}", loadoutId, charGuid);
            return false;
        }

        NormalizeLoadoutForSerialization(ref loadout);
        if (loadout.LoadoutConfigs == null || loadout.LoadoutConfigs.Length == 0)
        {
            Serilog.Log.Warning("PAINT_DEBUG TrySetLoadoutVisuals: no LoadoutConfigs for char={CharGuid}, loadout={LoadoutId}", charGuid, loadoutId);
            return false;
        }

        int configIndex = configId < loadout.LoadoutConfigs.Length ? (int)configId : 0;
        loadout.LoadoutConfigs[configIndex].Visuals = MergeVisualChanges(
            loadout.LoadoutConfigs[configIndex].Visuals,
            visuals);
        _loadouts[loadoutId] = loadout;
        mergedVisuals = loadout.LoadoutConfigs[configIndex].Visuals;

        var slottedItemsDict = loadout.LoadoutConfigs[0].Items.ToDictionary(x => x.SlotIndex, x => x.ItemGUID);
        var slottedItemsJson = System.Text.Json.JsonSerializer.Serialize(slottedItemsDict);
        var visualsJson = SerializeVisualsForPersistence(mergedVisuals);

        Serilog.Log.Information(
            "PAINT_DEBUG TrySetLoadoutVisuals AFTER MERGE: char={CharGuid}, loadout={LoadoutId}, mergedCount={Count}, merged={Merged}, visualsJson={VisualsJson}",
            charGuid, loadoutId,
            mergedVisuals.Length,
            SerializeVisualsForPersistence(mergedVisuals),
            visualsJson);

        _ = GRPCService.SaveCharacterLoadoutAsync(charGuid, loadoutId, (int)loadout.ChassisID, visualsJson, slottedItemsJson);
        return true;
    }

    private static LoadoutConfig_Visual[] MergeVisualChanges(LoadoutConfig_Visual[] existingVisuals, LoadoutConfig_Visual[] requestedVisuals)
    {
        var merged = (existingVisuals ?? Array.Empty<LoadoutConfig_Visual>())
            .Where(v => !IsEmptyPlaceholderVisual(v))
            .Select(CloneVisual)
            .ToList();

        foreach (var requestVisual in requestedVisuals ?? Array.Empty<LoadoutConfig_Visual>())
        {
            var normalized = NormalizeRequestedVisual(requestVisual);
            var existingIndex = merged.FindIndex(v => v.VisualType == normalized.VisualType && v.Data1 == normalized.Data1);

            // ItemSdbId=0 for a concrete visual slot is an explicit clear request.
            if (IsClearVisualRequest(normalized))
            {
                if (existingIndex >= 0)
                {
                    merged.RemoveAt(existingIndex);
                }

                continue;
            }

            if (IsEmptyPlaceholderVisual(normalized))
            {
                continue;
            }

            if (existingIndex >= 0)
            {
                merged[existingIndex] = normalized;
            }
            else
            {
                merged.Add(normalized);
            }
        }

        return merged
            .Where(v => !IsEmptyPlaceholderVisual(v))
            .ToArray();
    }

    private static string SerializeVisualsForPersistence(IEnumerable<LoadoutConfig_Visual> visuals)
    {
        var payload = (visuals ?? Array.Empty<LoadoutConfig_Visual>())
            .Select(v => new
            {
                v.ItemSdbId,
                v.VisualType,
                v.Data1,
                v.Data2,
                v.Transform,
            });

        return System.Text.Json.JsonSerializer.Serialize(payload);
    }

    private static LoadoutConfig_Visual NormalizeRequestedVisual(LoadoutConfig_Visual visual)
    {
        var normalized = CloneVisual(visual);

        // Client sends Data1 one step too high for indexed visual selectors.
        if (normalized.Data1 > 0)
        {
            normalized.Data1 -= 1;
        }

        return normalized;
    }

    private static LoadoutConfig_Visual CloneVisual(LoadoutConfig_Visual visual)
    {
        return new LoadoutConfig_Visual
        {
            ItemSdbId = visual.ItemSdbId,
            VisualType = visual.VisualType,
            Data1 = visual.Data1,
            Data2 = visual.Data2,
            Transform = visual.Transform?.ToArray() ?? Array.Empty<float>(),
        };
    }

    private static bool IsEmptyPlaceholderVisual(LoadoutConfig_Visual visual)
    {
        return visual.ItemSdbId == 0
               && (byte)visual.VisualType == 0
               && visual.Data1 == 0
               && visual.Data2 == 0
               && (visual.Transform == null || visual.Transform.Length == 0);
    }

    private static bool IsClearVisualRequest(LoadoutConfig_Visual visual)
    {
        if (visual.ItemSdbId != 0)
        {
            return false;
        }

        return visual.VisualType is LoadoutConfig_Visual.LoadoutVisualType.Palette
            or LoadoutConfig_Visual.LoadoutVisualType.Pattern
            or LoadoutConfig_Visual.LoadoutVisualType.Decal
            or LoadoutConfig_Visual.LoadoutVisualType.Glider
            or LoadoutConfig_Visual.LoadoutVisualType.Vehicle;
    }

    private byte GetInventoryTypeByItemTypeId(uint sdbId)
    {
        var itemInfo = SDBInterface.GetRootItem(sdbId);
        if (itemInfo != null)
        {
            return GetInventoryTypeByItemType(itemInfo.Type);
        }
        else
        {
            return (byte)InventoryType.Bag;
        }
    }

    private static LoadoutConfig[] CreateDefaultLoadoutConfigs()
    {
        return
        [
            new LoadoutConfig()
            {
                ConfigID = 0,
                ConfigName = "pve",
                Items = Array.Empty<LoadoutConfig_Item>(),
                Visuals = Array.Empty<LoadoutConfig_Visual>(),
                Perks = Array.Empty<uint>(),
                Unk1 = 0,
                PerkBandwidth = 0,
                PerkRespecLockRemainingSeconds = 0,
                HaveExtraData = 0,
            },
            new LoadoutConfig()
            {
                ConfigID = 1,
                ConfigName = "pvp",
                Items = Array.Empty<LoadoutConfig_Item>(),
                Visuals = Array.Empty<LoadoutConfig_Visual>(),
                Perks = Array.Empty<uint>(),
                Unk1 = 0,
                PerkBandwidth = 0,
                PerkRespecLockRemainingSeconds = 0,
                HaveExtraData = 0,
            },
        ];
    }

    private void NormalizeAndStoreLoadout(int loadoutId)
    {
        if (!_loadouts.TryGetValue(loadoutId, out var loadout))
        {
            return;
        }

        NormalizeLoadoutForSerialization(ref loadout);
        _loadouts[loadoutId] = loadout;
    }

    private void NormalizeLoadoutForSerialization(ref Loadout loadout)
    {
        if (loadout.LoadoutConfigs == null || loadout.LoadoutConfigs.Length < 2)
        {
            loadout.LoadoutConfigs = CreateDefaultLoadoutConfigs();
        }

        loadout.LoadoutName ??= $"Loadout {loadout.FrameLoadoutId}";
        loadout.LoadoutType ??= "battleframe";

        loadout.LoadoutConfigs[0].Items ??= Array.Empty<LoadoutConfig_Item>();
        loadout.LoadoutConfigs[1].Items ??= Array.Empty<LoadoutConfig_Item>();
        loadout.LoadoutConfigs[0].Visuals ??= Array.Empty<LoadoutConfig_Visual>();
        loadout.LoadoutConfigs[1].Visuals ??= Array.Empty<LoadoutConfig_Visual>();
        loadout.LoadoutConfigs[0].Perks ??= Array.Empty<uint>();
        loadout.LoadoutConfigs[1].Perks ??= Array.Empty<uint>();
        loadout.LoadoutConfigs[0].ConfigName ??= "pve";
        loadout.LoadoutConfigs[1].ConfigName ??= "pvp";

        for (int configIndex = 0; configIndex < loadout.LoadoutConfigs.Length; configIndex++)
        {
            var config = loadout.LoadoutConfigs[configIndex];
            config.Items ??= Array.Empty<LoadoutConfig_Item>();
            config.Visuals ??= Array.Empty<LoadoutConfig_Visual>();
            config.Perks ??= Array.Empty<uint>();
            config.ConfigName ??= configIndex == 0 ? "pve" : "pvp";
            config.HaveExtraData = 1;

            config.Items = NormalizeLegacyLoadoutSlots(config.Items, out _);

            config.ExtraData = BuildLoadoutExtraData(loadout, config);

            for (int visualIndex = 0; visualIndex < config.Visuals.Length; visualIndex++)
            {
                config.Visuals[visualIndex].Transform ??= Array.Empty<float>();
            }

            loadout.LoadoutConfigs[configIndex] = config;
        }
    }

    private LoadoutConfig_Extra BuildLoadoutExtraData(Loadout loadout, LoadoutConfig config)
    {
        uint vehicleSdbId = ResolveSlottedItemSdbId(config, (byte)LoadoutSlotType.Vehicle);
        uint gliderSdbId = ResolveSlottedItemSdbId(config, (byte)LoadoutSlotType.Glider);

        return new LoadoutConfig_Extra
        {
            Unk1_1 = 0,
            Unk1_2 = 0,
            Unk1_3 = 0,
            Unk1_4 = 0,
            Unk1_5 = 0,
            Unk1_6 = 0,
            Unk2 = Array.Empty<uint>(),
            UnkVisualsBlock = CreateEmptyVisualsBlock(),
            VehicleId = vehicleSdbId,
            GliderId = gliderSdbId,
            Unk3 = Array.Empty<LoadoutConfig_Extra_UnkThing>(),
            OverrideWeaponsPaletteId = 0,
            Unk4 = Array.Empty<uint>(),
            Unk5_1 = 0,
            Unk5_2 = 0,
            Unk5_3 = 0,
            Chassis = CreateChassisSlottedItem(loadout, config),
            Weapons = CreateWeaponSlottedItems(config),
            VisualOverrides = Array.Empty<VisualOverridesData>(),
            Backpack = CreateResolvedSlottedItem(config, LoadoutSlotType.Backpack, 255),
            Unk6 = 0,
            UnkPerkRespecRemainingSecRelated = config.PerkRespecLockRemainingSeconds,
            ArchetypeLevel = 1,
            Unk7 = 0,
        };
    }

    private SlottedItem CreateChassisSlottedItem(Loadout loadout, LoadoutConfig config)
    {
        var chassisVisuals = CharacterLoadout.BuildChassisVisuals(loadout.ChassisID, config.Visuals);

        return new SlottedItem
        {
            SdbId = loadout.ChassisID,
            SlotIndex = 255,
            Flags = 0,
            Unk2 = 0,
            Modules = CreateChassisModules(config),
            Visuals = chassisVisuals,
        };
    }

    private SlottedItem[] CreateWeaponSlottedItems(LoadoutConfig config)
    {
        return
        [
            CreateResolvedSlottedItem(config, LoadoutSlotType.Primary, 255),
            CreateResolvedSlottedItem(config, LoadoutSlotType.Secondary, 255),
        ];
    }

    private SlottedItem CreateResolvedSlottedItem(LoadoutConfig config, LoadoutSlotType slot, byte serializedSlotIndex)
    {
        uint sdbId = ResolveSlottedItemSdbId(config, (byte)slot);
        return new SlottedItem
        {
            SdbId = sdbId,
            SlotIndex = serializedSlotIndex,
            Flags = 0,
            Unk2 = 0,
            Modules = Array.Empty<SlottedModule>(),
            Visuals = CreateEmptyVisualsBlock(),
        };
    }

    private SlottedModule[] CreateChassisModules(LoadoutConfig config)
    {
        return config.Items
            .Where(item => CharacterLoadout.LoadoutChassisSlots.Contains((LoadoutSlotType)item.SlotIndex))
            .Select(item => new SlottedModule
            {
                SdbId = _items.TryGetValue(item.ItemGUID, out var equippedItem) ? equippedItem.SdbId : 0,
                SlotIndex = 255,
                Flags = 0,
                Unk2 = 0,
            })
            .Where(module => module.SdbId != 0)
            .ToArray();
    }

    private static VisualsBlock CreateEmptyVisualsBlock()
    {
        return new VisualsBlock
        {
            Decals = Array.Empty<VisualsDecalsBlock>(),
            Gradients = Array.Empty<uint>(),
            Colors = Array.Empty<uint>(),
            Palettes = Array.Empty<VisualsPaletteBlock>(),
            Patterns = Array.Empty<VisualsPatternBlock>(),
            OrnamentGroupIds = Array.Empty<uint>(),
            CziMapAssetIds = Array.Empty<uint>(),
            MorphWeights = Array.Empty<HalfFloat>(),
            Overlays = Array.Empty<VisualsOverlayBlock>(),
        };
    }

    private void SyncUtilityVisualsFromLoadoutSlots(Loadout loadout)
    {
        NormalizeLoadoutForSerialization(ref loadout);

        if (loadout.LoadoutConfigs.Length == 0)
        {
            return;
        }

        var pveConfig = loadout.LoadoutConfigs[0];
        var visuals = (pveConfig.Visuals ?? Array.Empty<LoadoutConfig_Visual>()).ToList();

        uint vehicleSdbId = ResolveSlottedItemSdbId(pveConfig, (byte)LoadoutSlotType.Vehicle);
        uint gliderSdbId = ResolveSlottedItemSdbId(pveConfig, (byte)LoadoutSlotType.Glider);

        UpsertUtilityVisual(visuals, LoadoutVisualType.Vehicle, vehicleSdbId);
        UpsertUtilityVisual(visuals, LoadoutVisualType.Glider, gliderSdbId);

        pveConfig.Visuals = visuals.ToArray();
        loadout.LoadoutConfigs[0] = pveConfig;
    }

    private uint ResolveSlottedItemSdbId(LoadoutConfig config, byte slotIndex)
    {
        var match = config.Items.FirstOrDefault(i => i.SlotIndex == slotIndex);
        if (match.ItemGUID == 0)
        {
            return 0;
        }

        if (_items.TryGetValue(match.ItemGUID, out var item))
        {
            return item.SdbId;
        }

        return 0;
    }

    private static void UpsertUtilityVisual(List<LoadoutConfig_Visual> visuals, LoadoutVisualType visualType, uint itemSdbId)
    {
        int index = visuals.FindIndex(v => v.VisualType == visualType);
        if (itemSdbId == 0)
        {
            if (index >= 0)
            {
                visuals.RemoveAt(index);
            }

            return;
        }

        var visual = new LoadoutConfig_Visual
        {
            ItemSdbId = itemSdbId,
            VisualType = visualType,
            Data1 = 0,
            Data2 = 0,
            Transform = Array.Empty<float>(),
        };

        if (index >= 0)
        {
            visuals[index] = visual;
        }
        else
        {
            visuals.Add(visual);
        }
    }

    private bool TryResolveStoredLoadoutItemId(ulong storedValue, out ulong resolvedGuid)
    {
        if (_items.ContainsKey(storedValue))
        {
            resolvedGuid = storedValue;
            return true;
        }

        if (storedValue <= uint.MaxValue)
        {
            uint sdbId = (uint)storedValue;
            var matchedItem = _items.Values.FirstOrDefault(item => item.SdbId == sdbId);
            if (matchedItem.GUID != 0)
            {
                resolvedGuid = matchedItem.GUID;
                return true;
            }
        }

        resolvedGuid = 0;
        return false;
    }

    private LoadoutConfig_Item[] NormalizeLegacyLoadoutSlots(LoadoutConfig_Item[] items, out bool changed)
    {
        changed = false;
        if (items == null || items.Length == 0)
        {
            return items ?? Array.Empty<LoadoutConfig_Item>();
        }

        bool hasZeroBasedAbilityMarker = items.Any(item => item.SlotIndex == 0 && IsAbilityModuleGuid(item.ItemGUID));

        for (int i = 0; i < items.Length; i++)
        {
            var entry = items[i];
            byte normalizedSlot = NormalizeSlotIndexWithItemContext(entry.SlotIndex, entry.ItemGUID, hasZeroBasedAbilityMarker);
            if (normalizedSlot == entry.SlotIndex)
            {
                continue;
            }

            entry.SlotIndex = normalizedSlot;
            items[i] = entry;
            changed = true;
        }

        if (!changed)
        {
            return items;
        }

        return items
            .GroupBy(item => item.SlotIndex)
            .Select(group => group.Last())
            .ToArray();
    }

    private LoadoutSlotType NormalizeRequestedSlotForItem(LoadoutSlotType requestedSlot, ulong itemGuid)
    {
        byte normalized = NormalizeSlotIndexWithItemContext((byte)requestedSlot, itemGuid, hasZeroBasedAbilityMarker: false);
        return (LoadoutSlotType)normalized;
    }

    private byte NormalizeSlotIndexWithItemContext(byte rawSlotIndex, ulong itemGuid, bool hasZeroBasedAbilityMarker)
    {
        if (IsAbilityModuleGuid(itemGuid))
        {
            // Legacy saves can encode ability slot indices instead of dbitems::LoadoutSlot ids.
            // Resolve ambiguous 1/2/3 by detecting whether this loadout follows zero-based ability indexing.
            switch (rawSlotIndex)
            {
                case 0:
                    return (byte)LoadoutSlotType.Ability1;
                case 1:
                    return hasZeroBasedAbilityMarker ? (byte)LoadoutSlotType.Ability2 : (byte)LoadoutSlotType.Ability1;
                case 2:
                    return hasZeroBasedAbilityMarker ? (byte)LoadoutSlotType.Ability3 : (byte)LoadoutSlotType.Ability2;
                case 3:
                    return hasZeroBasedAbilityMarker ? (byte)LoadoutSlotType.AbilityHKM : (byte)LoadoutSlotType.Ability3;
                case 4:
                    return (byte)LoadoutSlotType.AbilityHKM;
                case (byte)AbilitySlotType.AbilityMedical: // 6 — collides with LoadoutSlotType.AbilityHKM
                    return hasZeroBasedAbilityMarker ? (byte)LoadoutSlotType.GearMedicalSystem : (byte)LoadoutSlotType.AbilityHKM;
            }
        }

        return LegacyAbilitySlotToLoadoutSlot.GetValueOrDefault(rawSlotIndex, rawSlotIndex);
    }

    private bool IsAbilityModuleGuid(ulong guid)
    {
        if (guid == 0 || !_items.TryGetValue(guid, out var item))
        {
            return false;
        }

        var rootItem = SDBInterface.GetRootItem(item.SdbId);
        return rootItem != null && rootItem.Type == (byte)ItemType.AbilityModule;
    }

    private byte GetInventoryTypeByItemType(byte itemType)
    {
        var result = InventoryType.Bag;
        switch ((ItemType)itemType)
        {
            case ItemType.Backpack:
                // Legacy/unknown item type appears in some old inventories; treat as bag silently.
                result = InventoryType.Bag;
                break;
            case ItemType.TinkerTools:
                result = InventoryType.Bag;
                break;
            case ItemType.ItemModule:
                result = InventoryType.Bag;
                break;
            case ItemType.PaletteModule:
                result = InventoryType.Bag;
                break;
            case ItemType.CraftingStation:
                result = InventoryType.Bag;
                break;
            case ItemType.ResourceItem:
                result = InventoryType.Bag;
                break;
            case ItemType.LockBoxKey:
                result = InventoryType.Bag;
                break;
            case ItemType.Basic: // NOTE: There are some basic type items and resources that go into cache
                result = InventoryType.Bag;
                break;
            case ItemType.Consumable:
                result = InventoryType.Cache;
                break;
            case ItemType.AbilityModule:
                result = InventoryType.Gear;
                break;
            case ItemType.FrameModule:
                result = InventoryType.Gear;
                break;
            case ItemType.Weapon:
                result = InventoryType.Gear;
                break;
            case ItemType.Chassis:
                result = InventoryType.Gear;
                break;
            default:
                Serilog.Log.Information($"Unknown InventoryType for ItemType {(ItemType)itemType}, defaulting to {result}");
                break;
        }

        return (byte)result;
    }

    private void PersistLoadoutToDatabase(Loadout loadout)
    {
        NormalizeLoadoutForSerialization(ref loadout);

        var slottedItemsDict = loadout.LoadoutConfigs[0].Items.ToDictionary(x => x.SlotIndex, x => x.ItemGUID);
        var slottedItemsJson = System.Text.Json.JsonSerializer.Serialize(slottedItemsDict);
        var visualsJson = System.Text.Json.JsonSerializer.Serialize(loadout.LoadoutConfigs[0].Visuals ?? Array.Empty<LoadoutConfig_Visual>(), LoadoutVisualJsonOptions);
        ulong charGuid = ((NetworkPlayer)_player).CharacterId + 0xFE;

        _ = Task.Run(async () =>
        {
            try
            {
                await GRPCService.SaveCharacterLoadoutAsync(charGuid, loadout.FrameLoadoutId, (int)loadout.ChassisID, visualsJson, slottedItemsJson);
            }
            catch (Exception ex)
            {
                _shard.Logger.Warning(ex, "Failed to persist repaired loadout {loadoutId} for {charId}", loadout.FrameLoadoutId, _character.EntityId);
            }
        });
    }
}