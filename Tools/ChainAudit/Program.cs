using System.Xml;
using GameServer.Data;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.apt;
using SDB = FauFau.Formats.StaticDB;

var sdbPath = @"H:\MeldingWars\bin.db.ini\production\prod\prod-1962.0\system\db\clientdb.sd2";
var localStringsPath = @"D:\SteamLibrary\steamapps\common\Firefall\system\gui\localstrings.xml";
var customDataPath = @"H:\PIN\UdpHosts\GameServer\StaticDB\CustomData";

var sdb = new SDB();
sdb.Read(sdbPath);
SDBInterface.Init(sdb);
CustomDBInterface.Init();

var nameById = LoadLocalStrings(localStringsPath);
var abilityNameById = LoadAbilityNamesFromCustomData(customDataPath);
var baseCommands = SDBInterface.GetBaseCommandDefDictionary();
var fallbackItems = HardcodedCharacterData.FallbackInventoryItems.Distinct().ToArray();
var fallbackResourceItemIds = HardcodedCharacterData.FallbackInventoryResources.Select(r => r.Item1).Distinct().ToArray();
var defaultSeedItemIds = fallbackItems.Concat(fallbackResourceItemIds).Distinct().ToArray();

var brokenDefaultItems = new List<(uint Id, string Name, string Reason)>();

foreach (var itemId in defaultSeedItemIds)
{
    var root = SDBInterface.GetRootItem(itemId);
    if (root == null)
    {
        continue;
    }

    var module = SDBInterface.GetAbilityModule(itemId);
    if (module == null || module.AbilityChainId == 0)
    {
        continue;
    }

    var ability = SDBInterface.GetAbilityData(module.AbilityChainId);
    var displayName = ResolveItemDisplayName(root.NameId, module.AbilityChainId, nameById, abilityNameById);
    if (ability == null)
    {
        brokenDefaultItems.Add((itemId, displayName, $"Missing ability {module.AbilityChainId}"));
        continue;
    }

    if (ability.Chain == 0)
    {
        brokenDefaultItems.Add((itemId, displayName, $"Ability {ability.Id} has chain=0"));
        continue;
    }

    var visitedChains = new HashSet<uint>();
    var visitedAbilities = new HashSet<uint>();
    if (!ValidateChain(ability.Chain, ability.Id, visitedChains, visitedAbilities, out var reason))
    {
        brokenDefaultItems.Add((itemId, displayName, reason));
    }
}

var workingGliders = new List<(uint Id, string Name)>();
var workingRgvs = new List<(uint Id, string Name)>();

foreach (var module in SDBInterface.GetAbilityModuleDictionary().Values)
{
    if (module == null || module.AbilityChainId == 0)
    {
        continue;
    }

    var root = SDBInterface.GetRootItem(module.Id);
    if (root == null)
    {
        continue;
    }

    var name = ResolveItemDisplayName(root.NameId, module.AbilityChainId, nameById, abilityNameById);
    if (string.IsNullOrWhiteSpace(name))
    {
        continue;
    }

    var lower = name.ToLowerInvariant();
    var isGlider = lower.Contains("glider");
    var isRgv = lower.Contains("rgv");

    if (!isGlider && !isRgv)
    {
        continue;
    }

    var ability = SDBInterface.GetAbilityData(module.AbilityChainId);
    if (ability == null || ability.Chain == 0)
    {
        continue;
    }

    var visitedChains = new HashSet<uint>();
    var visitedAbilities = new HashSet<uint>();
    if (!ValidateChain(ability.Chain, ability.Id, visitedChains, visitedAbilities, out _))
    {
        continue;
    }

    if (!isRgv)
    {
        isRgv = ChainContainsVehicleCalldown(ability.Chain, new HashSet<uint>());
    }

    if (isGlider)
    {
        workingGliders.Add((root.SdbId, name));
    }

    if (isRgv)
    {
        workingRgvs.Add((root.SdbId, name));
    }
}

Console.WriteLine("=== BROKEN_DEFAULT_ITEMS ===");
foreach (var item in brokenDefaultItems.OrderBy(i => i.Id))
{
    Console.WriteLine($"{item.Id}\t{item.Name}\t{item.Reason}");
}

Console.WriteLine("=== WORKING_GLIDERS ===");
foreach (var item in workingGliders.OrderBy(i => i.Name).ThenBy(i => i.Id))
{
    Console.WriteLine($"{item.Id}\t{item.Name}");
}

Console.WriteLine("=== WORKING_RGVS ===");
foreach (var item in workingRgvs.OrderBy(i => i.Name).ThenBy(i => i.Id))
{
    Console.WriteLine($"{item.Id}\t{item.Name}");
}

return;

bool ValidateChain(uint chainId, uint sourceAbilityId, HashSet<uint> visitedChains, HashSet<uint> visitedAbilities, out string reason)
{
    reason = string.Empty;

    if (chainId == 0)
    {
        return true;
    }

    if (!visitedChains.Add(chainId))
    {
        return true;
    }

    var next = chainId;
    while (next != 0)
    {
        if (!baseCommands.TryGetValue(next, out var baseDef))
        {
            reason = $"Missing chain entry {next} (ability {sourceAbilityId})";
            return false;
        }

        if (!ValidateSpecialRefs(baseDef.Id, baseDef.Subtype, sourceAbilityId, visitedChains, visitedAbilities, out reason))
        {
            return false;
        }

        next = baseDef.Next;
    }

    return true;
}

bool ValidateSpecialRefs(uint commandId, uint subtype, uint sourceAbilityId, HashSet<uint> visitedChains, HashSet<uint> visitedAbilities, out string reason)
{
    reason = string.Empty;

    switch (subtype)
    {
        case 102: // ConditionalBranch
        {
            var def = SDBInterface.GetConditionalBranchCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing ConditionalBranchCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.IfChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            if (!ValidateChain(def.ThenChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            if (!ValidateChain(def.ElseChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 107: // WhileLoop
        {
            var def = SDBInterface.GetWhileLoopCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing WhileLoopCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.BodyChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            if (!ValidateChain(def.ConditionChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 114: // LogicAndChain
        {
            var def = SDBInterface.GetLogicAndChainCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing LogicAndChainCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.AndChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 113: // LogicOrChain
        {
            var def = SDBInterface.GetLogicOrChainCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing LogicOrChainCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.OrChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 111: // LogicNegate
        {
            var def = SDBInterface.GetLogicNegateCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing LogicNegateCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.NegateChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 110: // LogicOr
        {
            var def = SDBInterface.GetLogicOrCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing LogicOrCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.AChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            if (!ValidateChain(def.BChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 13: // ImpactToggleEffect
        {
            var def = SDBInterface.GetImpactToggleEffectCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing ImpactToggleEffectCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(def.PreApplyChain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        case 106: // Call
        {
            var def = SDBInterface.GetCallCommandDef(commandId);
            if (def == null)
            {
                reason = $"Missing CallCommandDef {commandId} (ability {sourceAbilityId})";
                return false;
            }

            if (def.AbilityId == 0)
            {
                return true;
            }

            if (!visitedAbilities.Add(def.AbilityId))
            {
                return true;
            }

            var calledAbility = SDBInterface.GetAbilityData(def.AbilityId);
            if (calledAbility == null)
            {
                reason = $"CallCommand {commandId} references missing ability {def.AbilityId} (source ability {sourceAbilityId})";
                return false;
            }

            if (!ValidateChain(calledAbility.Chain, sourceAbilityId, visitedChains, visitedAbilities, out reason)) return false;
            return true;
        }
        default:
            return true;
    }
}

bool ChainContainsVehicleCalldown(uint chainId, HashSet<uint> visitedChains)
{
    if (chainId == 0 || !visitedChains.Add(chainId))
    {
        return false;
    }

    var next = chainId;
    while (next != 0)
    {
        if (!baseCommands.TryGetValue(next, out var baseDef))
        {
            return false;
        }

        if (SDBInterface.GetVehicleCalldownCommandDef(baseDef.Id) != null ||
            SDBInterface.GetAttemptToCalldownVehicleCommandDef(baseDef.Id) != null ||
            CustomDBInterface.GetCalldownVehicleCommandDef(baseDef.Id) != null)
        {
            return true;
        }

        var callDef = SDBInterface.GetCallCommandDef(baseDef.Id);
        if (callDef != null && callDef.AbilityId != 0)
        {
            var calledAbility = SDBInterface.GetAbilityData(callDef.AbilityId);
            if (calledAbility != null && ChainContainsVehicleCalldown(calledAbility.Chain, visitedChains))
            {
                return true;
            }
        }

        var branchDef = SDBInterface.GetConditionalBranchCommandDef(baseDef.Id);
        if (branchDef != null)
        {
            if (ChainContainsVehicleCalldown(branchDef.IfChain, visitedChains) ||
                ChainContainsVehicleCalldown(branchDef.ThenChain, visitedChains) ||
                ChainContainsVehicleCalldown(branchDef.ElseChain, visitedChains))
            {
                return true;
            }
        }

        next = baseDef.Next;
    }

    return false;
}

static Dictionary<uint, string> LoadLocalStrings(string path)
{
    var map = new Dictionary<uint, string>();
    if (!File.Exists(path))
    {
        return map;
    }

    try
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true });
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!reader.Name.Equals("String", StringComparison.OrdinalIgnoreCase) && !reader.Name.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var idRaw = reader.GetAttribute("Id") ?? reader.GetAttribute("id") ?? reader.GetAttribute("ID");
            if (!uint.TryParse(idRaw, out var id))
            {
                continue;
            }

            var value = reader.GetAttribute("Text") ?? reader.GetAttribute("text") ?? reader.ReadElementContentAsString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                map[id] = value.Trim();
            }
        }
    }
    catch
    {
        // Localization text is optional for this audit utility.
    }

    return map;
}

static string ResolveName(uint nameId, IReadOnlyDictionary<uint, string> names)
{
    return names.TryGetValue(nameId, out var value) ? value : $"NameId:{nameId}";
}

static Dictionary<uint, string> LoadAbilityNamesFromCustomData(string directory)
{
    var map = new Dictionary<uint, string>();
    if (!Directory.Exists(directory))
    {
        return map;
    }

    var files = Directory.GetFiles(directory, "aptgss_*.json", SearchOption.TopDirectoryOnly);
    var regex = new System.Text.RegularExpressions.Regex("Triggered by Ability\\s+(\\d+)\\s*-\\s*([^\\u0000\\\"\\r\\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    foreach (var file in files)
    {
        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch
        {
            continue;
        }

        foreach (System.Text.RegularExpressions.Match match in regex.Matches(text))
        {
            if (!uint.TryParse(match.Groups[1].Value, out var abilityId))
            {
                continue;
            }

            var rawName = match.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawName))
            {
                continue;
            }

            if (!map.ContainsKey(abilityId))
            {
                map[abilityId] = rawName;
            }
        }
    }

    return map;
}

static string ResolveItemDisplayName(uint nameId, uint abilityId, IReadOnlyDictionary<uint, string> names, IReadOnlyDictionary<uint, string> abilityNames)
{
    if (abilityNames.TryGetValue(abilityId, out var abilityName) && !string.IsNullOrWhiteSpace(abilityName))
    {
        return abilityName;
    }

    return ResolveName(nameId, names);
}
