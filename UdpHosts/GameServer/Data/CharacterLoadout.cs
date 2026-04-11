using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character;
using GameServer.Data.SDB;

namespace GameServer.Data;

public enum LoadoutSlotType : byte
{
    Primary = 1,
    Secondary = 2,
    AbilityHKM = 6,
    Ability1 = 7,
    Ability2 = 8,
    Ability3 = 9,
    Backpack = 11,

    GearTorso = 116,
    GearAuxWeapon = 122,
    GearMedicalSystem = 123,
    GearHead = 124,
    GearArms = 126,
    GearLegs = 127,
    GearReactor = 128,
    GearOS = 129,
    GearGadget1 = 130,
    GearGadget2 = 137,
    Vehicle = 157,
    Glider = 158,
}

public enum AbilitySlotType
{
    Ability1 = 0,
    Ability2 = 1,
    Ability3 = 2,
    AbilityHKM = 3,
    AbilityInteract = 4,
    AbilityAux = 5,
    AbilityMedical = 6,
    AbilitySIN = 13,
    AbilityCalldownVehicle = 16,
    AbilityCalldownGlider = 17
}

public class CharacterLoadout
{
    public static readonly LoadoutSlotType[] LoadoutAbilitySlots =
    {
        LoadoutSlotType.Ability1,
        LoadoutSlotType.Ability2,
        LoadoutSlotType.Ability3,
        LoadoutSlotType.AbilityHKM,
        LoadoutSlotType.GearAuxWeapon,
        LoadoutSlotType.GearMedicalSystem,
        LoadoutSlotType.Vehicle,
        LoadoutSlotType.Glider
    };

    public static readonly LoadoutSlotType[] LoadoutChassisSlots =
    {
        LoadoutSlotType.GearTorso,
        LoadoutSlotType.GearAuxWeapon,
        LoadoutSlotType.GearMedicalSystem,
        LoadoutSlotType.GearHead,
        LoadoutSlotType.GearArms,
        LoadoutSlotType.GearLegs,
        LoadoutSlotType.GearReactor,
        LoadoutSlotType.GearOS,
        LoadoutSlotType.GearGadget1,
        LoadoutSlotType.GearGadget2
    };

    public static readonly LoadoutSlotType[] LoadoutWeaponSlots =
    {
        LoadoutSlotType.Primary,
        LoadoutSlotType.Secondary,
    };

    public static readonly Dictionary<LoadoutSlotType, AbilitySlotType> LoadoutToAbilitySlotMap = new Dictionary<LoadoutSlotType, AbilitySlotType>()
    {
        { LoadoutSlotType.Ability1, AbilitySlotType.Ability1 },
        { LoadoutSlotType.Ability2, AbilitySlotType.Ability2 },
        { LoadoutSlotType.Ability3, AbilitySlotType.Ability3 },
        { LoadoutSlotType.AbilityHKM, AbilitySlotType.AbilityHKM },
        { LoadoutSlotType.GearAuxWeapon, AbilitySlotType.AbilityAux },
        { LoadoutSlotType.GearMedicalSystem, AbilitySlotType.AbilityMedical },
        { LoadoutSlotType.Vehicle, AbilitySlotType.AbilityCalldownVehicle },
        { LoadoutSlotType.Glider, AbilitySlotType.AbilityCalldownGlider },
    };
    public static readonly Dictionary<AbilitySlotType, LoadoutSlotType> AbilityToLoadoutSlotMap = new Dictionary<AbilitySlotType, LoadoutSlotType>()
    {
        { AbilitySlotType.Ability1, LoadoutSlotType.Ability1 },
        { AbilitySlotType.Ability2, LoadoutSlotType.Ability2 },
        { AbilitySlotType.Ability3, LoadoutSlotType.Ability3 },
        { AbilitySlotType.AbilityHKM, LoadoutSlotType.AbilityHKM },
        { AbilitySlotType.AbilityAux, LoadoutSlotType.GearAuxWeapon },
        { AbilitySlotType.AbilityMedical, LoadoutSlotType.GearMedicalSystem },
        { AbilitySlotType.AbilityCalldownVehicle, LoadoutSlotType.Vehicle },
        { AbilitySlotType.AbilityCalldownGlider, LoadoutSlotType.Glider }
    };

    public Dictionary<LoadoutSlotType, uint> SlottedItems = new Dictionary<LoadoutSlotType, uint>();

    // Module and Character Scalars is an assumption that has not been completely confirmed. I'm not sure what the exact rule for separation between the two categories is.
    public Dictionary<ushort, float> ItemAttributes = new Dictionary<ushort, float>();
    public Dictionary<ushort, float> ItemModuleScalars = new Dictionary<ushort, float>();
    public Dictionary<ushort, float> ItemCharacterScalars = new Dictionary<ushort, float>();

    private static readonly Dictionary<ushort, float> _fallbackAttributes = new()
    {
        // We fill these in if we somehow don't have them to ensure a playable experience (0 run speed = zzz)
        { 5, 75 }, // Jet Energy Recharge
        { 6, 100 }, // Health
        { 7, 3.75f }, // Health Regen
        { 12, 10 }, // Run Speed
        { 35, 500 }, // Jet Energy
        { 37, 1.75f }, // Jump Height
        { 1121, 150 }, // Jet Spring Cost
        { 1451, 100 }, // Power Rating
        { 1377, 130 }, // Sprint Speed
    };

    private static readonly Dictionary<ushort, float> _fallbackModuleScalars = new()
    {
    };

    private static readonly Dictionary<ushort, float> _fallbackCharacterScalars = new()
    {
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterLoadout"/> class.
    /// </summary>
    public CharacterLoadout()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterLoadout"/> class.
    /// </summary>
    /// <param name="refData">Data to setup the loadout with</param>
    public CharacterLoadout(LoadoutReferenceData refData)
    {
        InitFromLoadoutReferenceData(refData);
    }

    public int LoadoutID { get; set; }
    public uint VehicleID { get; set; }
    public uint GliderID { get; set; }
    public uint ChassisID { get; set; }
    public uint BackpackID { get; set; }
    public byte Level { get; set; } = 1;
    public uint ChassisChangeTime { get; set; } = 0;

    public ChassisWarpaintResult ChassisWarpaint { get; set; }
    public LoadoutConfig_Visual[] LoadoutVisuals { get; private set; } = Array.Empty<LoadoutConfig_Visual>();

    public VisualsBlock GetChassisVisuals()
    {
        if (LoadoutVisuals.Length > 0)
        {
            return BuildChassisVisualsFromLoadoutVisuals(LoadoutVisuals, ChassisWarpaint);
        }

        return new VisualsBlock
        {
            Decals = Array.Empty<VisualsDecalsBlock>(),
            Gradients = ChassisWarpaint.Gradients,
            Colors = ChassisWarpaint.Colors,
            Palettes = ChassisWarpaint.Palettes,
            Patterns = Array.Empty<VisualsPatternBlock>(),
            OrnamentGroupIds = Array.Empty<uint>(),
            CziMapAssetIds = Array.Empty<uint>(),
            MorphWeights = Array.Empty<HalfFloat>(),
            Overlays = Array.Empty<VisualsOverlayBlock>()
        };
    }

    public static VisualsBlock BuildChassisVisuals(uint chassisId, LoadoutConfig_Visual[] visuals)
    {
        var baseWarpaint = SDBUtils.GetChassisWarpaint(chassisId, 0, 0, 0, 0);
        return BuildChassisVisualsFromLoadoutVisuals(visuals ?? Array.Empty<LoadoutConfig_Visual>(), baseWarpaint);
    }

    public void SetLoadoutVisuals(LoadoutConfig_Visual[] visuals)
    {
        LoadoutVisuals = visuals?.ToArray() ?? Array.Empty<LoadoutConfig_Visual>();
        ChassisWarpaint = BuildChassisWarpaintFromLoadoutVisuals(LoadoutVisuals, ChassisWarpaint);
    }

    public uint GetAbilityModuleIdBySlotIndex(byte slotIndex)
    {
        var slotType = (AbilitySlotType)slotIndex;
        var loadoutSlot = AbilityToLoadoutSlotMap.GetValueOrDefault(slotType);
        if (loadoutSlot == 0)
        {
            return 0;
        }

        return SlottedItems.GetValueOrDefault(loadoutSlot);
    }

    public SlottedModule[] GetBackpackModules()
    {
        return SlottedItems
        .Where(slotted => LoadoutAbilitySlots.Contains(slotted.Key))
        .Select((slotted) =>
        {
            return new SlottedModule
            {
                SdbId = slotted.Value,
                SlotIndex = (byte)LoadoutToAbilitySlotMap[slotted.Key],
                Flags = 0,
                Unk2 = 0,
            };
        })
        .ToArray();
    }

    public SlottedModule[] GetChassisModules()
    {
        return SlottedItems
        .Where(slotted => LoadoutChassisSlots.Contains(slotted.Key))
        .Select((slotted) =>
        {
            return new SlottedModule
            {
                SdbId = slotted.Value,
                SlotIndex = 0xff,
                Flags = 0,
                Unk2 = 0,
            };
        })
        .ToArray();
    }

    public StatsData[] GetItemAttributes()
    {
        return ItemAttributes
        .Select((pair) =>
        {
            return new StatsData()
            {
                Id = pair.Key,
                Value = pair.Value
            };
        })
        .ToArray();
    }

    public StatsData[] GetPrimaryWeaponAttributes()
    {
        var result = new Dictionary<ushort, float>();
        var itemId = SlottedItems.GetValueOrDefault(LoadoutSlotType.Primary);
        if (itemId != 0)
        {
            var itemAttributes = SDBInterface.GetItemAttributeRange(itemId);
            foreach (var range in itemAttributes.Values)
            {
                if (result.ContainsKey(range.AttributeId))
                {
                    result[range.AttributeId] += range.Base;
                }
                else
                {
                    result.Add(range.AttributeId, range.Base);
                }
            }
        }

        return result
        .Select((pair) =>
        {
            return new StatsData()
            {
                Id = pair.Key,
                Value = pair.Value
            };
        })
        .ToArray();
    }

    public StatsData[] GetSecondaryWeaponAttributes()
    {
        var result = new Dictionary<ushort, float>();
        var itemId = SlottedItems.GetValueOrDefault(LoadoutSlotType.Secondary);
        if (itemId != 0)
        {
            var itemAttributes = SDBInterface.GetItemAttributeRange(itemId);
            foreach (var range in itemAttributes.Values)
            {
                if (result.ContainsKey(range.AttributeId))
                {
                    result[range.AttributeId] += range.Base;
                }
                else
                {
                    result.Add(range.AttributeId, range.Base);
                }
            }
        }

        return result
        .Select((pair) =>
        {
            return new StatsData()
            {
                Id = pair.Key,
                Value = pair.Value
            };
        })
        .ToArray();
    }

    public StatsData[] GetItemModuleScalars()
    {
        return ItemModuleScalars
        .Select((pair) =>
        {
            return new StatsData()
            {
                Id = pair.Key,
                Value = pair.Value
            };
        })
        .ToArray();
    }

    public StatsData[] GetItemCharacterScalars()
    {
        return ItemCharacterScalars
        .Select((pair) =>
        {
            return new StatsData()
            {
                Id = pair.Key,
                Value = pair.Value
            };
        })
        .ToArray();
    }

    private void InitFromLoadoutReferenceData(LoadoutReferenceData refData)
    {
        VehicleID = 0;
        GliderID = 0;
        LoadoutID = refData.LoadoutId;
        ChassisID = refData.ChassisId;
        BackpackID = 0;
        ChassisWarpaint = SDBUtils.GetChassisWarpaint(ChassisID, 0, 0, 0, 0);
        SetLoadoutVisuals(refData.Visuals);

        // Assume PvE
        foreach (var (slot, type) in refData.SlottedItemsPvE)
        {
            SlottedItems.Add(slot, type);
        }

        // Utility equipment should come from persisted loadout slot state.
        BackpackID = SlottedItems.GetValueOrDefault(LoadoutSlotType.Backpack, 0u);
        VehicleID = SlottedItems.GetValueOrDefault(LoadoutSlotType.Vehicle, 0u);
        GliderID = SlottedItems.GetValueOrDefault(LoadoutSlotType.Glider, 0u);

        // Ensure Vehicle and Glider are always present in SlottedItems so GetBackpackModules() includes them.
        // Items may sit in CharacterItems (inventory) rather than CharacterLoadoutItems (loadout slots),
        // in which case they won't be in SlottedItemsPvE but VehicleID/GliderID will still be set.
        if (!SlottedItems.ContainsKey(LoadoutSlotType.Vehicle) && VehicleID != 0)
        {
            SlottedItems[LoadoutSlotType.Vehicle] = VehicleID;
        }

        if (!SlottedItems.ContainsKey(LoadoutSlotType.Glider) && GliderID != 0)
        {
            SlottedItems[LoadoutSlotType.Glider] = GliderID;
        }

        CalculateItemAttributes();
    }

    private static VisualsBlock BuildChassisVisualsFromLoadoutVisuals(LoadoutConfig_Visual[] visuals, ChassisWarpaintResult baseWarpaint)
    {
        var warpaint = BuildChassisWarpaintFromLoadoutVisuals(visuals, baseWarpaint);

        var patterns = visuals
            .Where(visual => visual.VisualType == LoadoutConfig_Visual.LoadoutVisualType.Pattern && visual.ItemSdbId > 0)
            .Select((visual, index) => new VisualsPatternBlock
            {
                PatternId = visual.ItemSdbId,
                TransformValues = BuildPatternTransform(visual.Transform),
                Usage = GetPatternUsage(visual, index),
            })
            .ToArray();

        var decals = visuals
            .Where(visual => visual.VisualType == LoadoutConfig_Visual.LoadoutVisualType.Decal && visual.ItemSdbId > 0)
            .Select(visual => new VisualsDecalsBlock
            {
                DecalId = visual.ItemSdbId,
                Color = visual.Data2,
                Transform = BuildDecalTransforms(visual.Transform),
                Usage = 0,
            })
            .ToArray();

        return new VisualsBlock
        {
            Decals = decals,
            Gradients = warpaint.Gradients ?? Array.Empty<uint>(),
            Colors = warpaint.Colors ?? Array.Empty<uint>(),
            Palettes = warpaint.Palettes ?? Array.Empty<VisualsPaletteBlock>(),
            Patterns = patterns,
            OrnamentGroupIds = Array.Empty<uint>(),
            CziMapAssetIds = Array.Empty<uint>(),
            MorphWeights = Array.Empty<HalfFloat>(),
            Overlays = Array.Empty<VisualsOverlayBlock>(),
        };
    }

    private static ChassisWarpaintResult BuildChassisWarpaintFromLoadoutVisuals(LoadoutConfig_Visual[] visuals, ChassisWarpaintResult baseWarpaint)
    {
        var fallback = baseWarpaint ?? new ChassisWarpaintResult
        {
            Gradients = Array.Empty<uint>(),
            Colors = CreateDefaultColors(),
            Palettes = Array.Empty<VisualsPaletteBlock>(),
        };

        var paletteVisuals = visuals
            .Where(visual => visual.VisualType == LoadoutConfig_Visual.LoadoutVisualType.Palette && visual.ItemSdbId > 0)
            .ToArray();

        if (paletteVisuals.Length == 0)
        {
            return new ChassisWarpaintResult
            {
                Gradients = fallback.Gradients ?? Array.Empty<uint>(),
                Colors = fallback.Colors ?? CreateDefaultColors(),
                Palettes = fallback.Palettes ?? Array.Empty<VisualsPaletteBlock>(),
            };
        }

        var gradients = new List<uint>();
        var palettes = new List<VisualsPaletteBlock>();
        var colors = fallback.Colors?.Length == 7 ? fallback.Colors.ToArray() : CreateDefaultColors();

        foreach (var visual in paletteVisuals)
        {
            var palette = SDBInterface.GetWarpaintPalette(visual.ItemSdbId);
            if (palette == null)
            {
                continue;
            }

            if (SDBUtils.TryMapWarpaintTypeFlagsToPaletteType(palette.TypeFlags, out var paletteType))
            {
                palettes.Add(new VisualsPaletteBlock
                {
                    PaletteId = palette.Id,
                    PaletteType = paletteType,
                });
            }

            var paletteColors = new uint[7]
            {
                FColor.CombineLightDark(palette.Color1Highlight, palette.Color1Shadow),
                FColor.CombineLightDark(palette.Color2Highlight, palette.Color2Shadow),
                FColor.CombineLightDark(palette.Color3Highlight, palette.Color3Shadow),
                FColor.CombineLightDark(palette.Color4Highlight, palette.Color4Shadow),
                FColor.CombineLightDark(palette.Color5Highlight, palette.Color5Shadow),
                FColor.CombineLightDark(palette.Color6Highlight, palette.Color6Shadow),
                FColor.CombineLightDark(palette.Color7Highlight, palette.Color7Shadow),
            };

            if ((palette.TypeFlags & (uint)Math.Pow(2, 4)) != 0)
            {
                colors[0] = paletteColors[0];
                colors[1] = paletteColors[1];
                colors[2] = paletteColors[2];
                colors[3] = paletteColors[3];
                colors[4] = paletteColors[4];
                colors[5] = paletteColors[5];
                colors[6] = paletteColors[6];
            }

            if ((palette.TypeFlags & (uint)Math.Pow(2, 0)) != 0)
            {
                colors[0] = paletteColors[0];
                colors[1] = paletteColors[1];
                colors[2] = paletteColors[2];
            }

            if ((palette.TypeFlags & (uint)Math.Pow(2, 1)) != 0)
            {
                colors[3] = paletteColors[3];
                colors[4] = paletteColors[4];
            }

            if ((palette.TypeFlags & (uint)Math.Pow(2, 3)) != 0)
            {
                colors[5] = paletteColors[5];
                colors[6] = paletteColors[6];
            }

            if (palette.TextureGradientId != 0)
            {
                gradients.Add(palette.TextureGradientId);
            }
        }

        return new ChassisWarpaintResult
        {
            Gradients = gradients.Count > 0 ? gradients.ToArray() : fallback.Gradients ?? Array.Empty<uint>(),
            Colors = colors,
            Palettes = palettes.Count > 0 ? palettes.ToArray() : fallback.Palettes ?? Array.Empty<VisualsPaletteBlock>(),
        };
    }

    private static byte GetPatternUsage(LoadoutConfig_Visual visual, int index)
    {
        if (visual.Data1 > 0)
        {
            return (byte)Math.Clamp((int)visual.Data1, 0, 3);
        }

        return (byte)Math.Clamp(index, 0, 3);
    }

    private static HalfVector4 BuildPatternTransform(float[] rawTransform)
    {
        if (rawTransform == null || rawTransform.Length == 0)
        {
            return (HalfVector4)Vector4.Zero;
        }

        return (HalfVector4)new Vector4(
            rawTransform.Length > 0 ? rawTransform[0] : 0f,
            rawTransform.Length > 1 ? rawTransform[1] : 0f,
            rawTransform.Length > 2 ? rawTransform[2] : 0f,
            rawTransform.Length > 3 ? rawTransform[3] : 0f);
    }

    private static HalfVector4[] BuildDecalTransforms(float[] rawTransform)
    {
        var transforms = new HalfVector4[3];
        if (rawTransform == null || rawTransform.Length == 0)
        {
            return transforms;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            int baseIndex = i * 4;
            transforms[i] = (HalfVector4)new Vector4(
                baseIndex < rawTransform.Length ? rawTransform[baseIndex] : 0f,
                baseIndex + 1 < rawTransform.Length ? rawTransform[baseIndex + 1] : 0f,
                baseIndex + 2 < rawTransform.Length ? rawTransform[baseIndex + 2] : 0f,
                baseIndex + 3 < rawTransform.Length ? rawTransform[baseIndex + 3] : 0f);
        }

        return transforms;
    }

    private static uint[] CreateDefaultColors()
    {
        return new uint[7]
        {
            4278190080,
            4278190080,
            4278190080,
            4278190080,
            4278190080,
            4278190080,
            4278190080,
        };
    }

    private void ApplyItemStats(uint itemTypeId, Dictionary<ushort, float> totalAttributes, Dictionary<ushort, float> totalModuleScalars, Dictionary<ushort, float> totalCharacterScalars)
    {
        var itemAttributes = SDBInterface.GetItemAttributeRange(itemTypeId);
        foreach (var range in itemAttributes.Values)
        {
            if (totalAttributes.ContainsKey(range.AttributeId))
            {
                totalAttributes[range.AttributeId] += range.Base;
            }
            else
            {
                totalAttributes.Add(range.AttributeId, range.Base);
            }
        }

        var itemModuleScalars = SDBInterface.GetItemModuleScalars(itemTypeId);
        foreach ((ushort attributeCategoryId, (float value, float perLevel)) in itemModuleScalars)
        {
            var attributeCategory = SDBInterface.GetAttributeCategory(attributeCategoryId);
            if (!totalCharacterScalars.ContainsKey(attributeCategoryId))
            {
                totalCharacterScalars.Add(attributeCategoryId, 0.0f);
            }

            if (attributeCategory.IsScalar == 1)
            {
                totalCharacterScalars[attributeCategoryId] += totalCharacterScalars[attributeCategoryId] / 100 * attributeCategory.ModuleEffectiveness;
            }
            else
            {
                totalCharacterScalars[attributeCategoryId] += value - 1;
            }
        }

        var itemCharacterScalars = SDBInterface.GetItemCharacterScalars(itemTypeId);
        foreach ((ushort attributeCategoryId, (float value, float perLevel)) in itemCharacterScalars)
        {
            var attributeCategory = SDBInterface.GetAttributeCategory(attributeCategoryId);
            if (!totalModuleScalars.ContainsKey(attributeCategoryId))
            {
                totalModuleScalars.Add(attributeCategoryId, 0.0f);
            }

            if (attributeCategory.IsScalar == 1)
            {
                totalModuleScalars[attributeCategoryId] += totalModuleScalars[attributeCategoryId] / 100 * attributeCategory.ModuleEffectiveness;
            }
            else
            {
                totalModuleScalars[attributeCategoryId] += value - 1;
            }
        }
    }

    internal void CalculateItemAttributes()
    {
        var attributes = new Dictionary<ushort, float>()
        {
            { 12, 2.5f }, // Temporary base Run Speed boost because it's so slow otherwise
        };
        var moduleScalars = new Dictionary<ushort, float>()
        {
        };
        var characterScalars = new Dictionary<ushort, float>()
        {
        };

        // Get Base Health and Shields from Battleframe
        var chassis = SDBInterface.GetBattleframe(ChassisID);
        if (chassis != null)
        {
            // Attribute 6 is Max Health
            if (attributes.ContainsKey(6))
            {
                attributes[6] += chassis.BaseHealth;
            }
            else
            {
                attributes.Add(6, chassis.BaseHealth);
            }

            // Apply Level Category Scalars for Health (Category 3 usually, but we check definition)
            var healthDef = SDBInterface.GetAttributeDefinition(6);
            if (healthDef != null)
            {
                var scalar = SDBInterface.GetLevelCategoryScalar(healthDef.AttributeCategory, Level);
                if (scalar != null)
                {
                    attributes[6] *= scalar.Scalar;
                }
            }
        }

        ApplyItemStats(ChassisID, attributes, moduleScalars, characterScalars);
        foreach (var pair in SlottedItems)
        {
            if (LoadoutAbilitySlots.Contains(pair.Key) || LoadoutChassisSlots.Contains(pair.Key))
            {
                ApplyItemStats(pair.Value, attributes, moduleScalars, characterScalars);
            }
        }

        foreach (var pair in _fallbackAttributes)
        {
            if (!attributes.ContainsKey(pair.Key))
            {
                attributes.Add(pair.Key, pair.Value);
            }
        }

        foreach (var pair in _fallbackModuleScalars)
        {
            if (!moduleScalars.ContainsKey(pair.Key))
            {
                attributes.Add(pair.Key, pair.Value);
            }
        }

        foreach (var pair in _fallbackCharacterScalars)
        {
            if (!characterScalars.ContainsKey(pair.Key))
            {
                attributes.Add(pair.Key, pair.Value);
            }
        }

        ItemAttributes = attributes;
        ItemModuleScalars = moduleScalars;
        ItemCharacterScalars = characterScalars;
    }
}