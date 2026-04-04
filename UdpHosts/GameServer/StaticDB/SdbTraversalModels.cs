namespace GameServer.Data.SDB;

using System.Collections.Generic;

public enum SdbEffectType
{
    StatusEffect,
    PermanentEffect,
    ItemUnlock,
    PetSpawn,
    Emote,
    Boost,
    Blueprint,
    Ability,
    Unknown
}

public class SdbItemChainResult
{
    public uint ItemSdbId { get; set; }
    public List<SdbEffectEntry> Effects { get; set; } = new();
    public SdbBlueprintInfo Blueprint { get; set; }
}

public class SdbEffectEntry
{
    public SdbEffectType Type { get; set; }
    public uint SdbId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public override string ToString() => $"[ {Type} ] ID: {SdbId} ({Name})";
}

public class SdbBlueprintInfo
{
    public uint Id { get; set; }
    public uint NameId { get; set; }
    public List<SdbBlueprintIngredient> Ingredients { get; set; } = new();
}

public class SdbBlueprintIngredient
{
    public uint ItemId { get; set; }
    public uint Quantity { get; set; }
}