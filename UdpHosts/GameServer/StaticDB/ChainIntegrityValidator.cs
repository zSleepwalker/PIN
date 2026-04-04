namespace GameServer.Data.SDB;

using System;
using System.Collections.Generic;
using System.Linq;
using Records.apt;

/// <summary>
/// Validates chain graph integrity after StaticDB loads. 
/// Traverses all known chain entry points and reports missing nodes, dangling links, and structural issues.
/// Runs at startup in warn-and-continue mode; results can be examined for diagnostics.
/// </summary>
public class ChainIntegrityValidator
{
    private readonly ISDBLoader _loader;
    private List<ValidationResult> _results = new();

    // Category constants for result grouping
    public const string MissingNode = "MissingNode";
    public const string DanglingLink = "DanglingLink";
    public const string MissingAbility = "MissingAbility";
    public const string CircularReference = "CircularReference";
    public const string UnreachableChain = "UnreachableChain";
    public const string StructuralIssue = "StructuralIssue";

    public class ValidationResult
    {
        public string Category { get; set; }
        public string TableName { get; set; }
        public uint ReferencedId { get; set; }
        public uint SourceId { get; set; }
        public string SourceField { get; set; }
        public string Details { get; set; }
    }

    public ChainIntegrityValidator(ISDBLoader loader)
    {
        _loader = loader;
        _results = new();
    }

    /// <summary>
    /// Run full chain integrity validation. Returns list of issues found.
    /// </summary>
    /// <returns>List of diagnostic messages describing validation issues found.</returns>
    public List<string> ValidateAll()
    {
        _results.Clear();
        var diagnostics = new List<string>();

        try
        {
            // Validate ability chain integrity
            ValidateAbilityChains();

            // Validate status effect chains
            ValidateStatusEffectChains();

            // Validate branch command chains (if/then/else)
            ValidateBranchChains();

            // Validate loop command chains (while)
            ValidateLoopChains();

            // Validate logic chains (and/or/negate)
            ValidateLogicChains();

            // Validate special command chains
            ValidateSpecialChains();

            // Validate item-to-ability bridges
            ValidateItemAbilityBridges();

            // Format results into diagnostic messages, grouped by category
            diagnostics = FormatDiagnostics();
        }
        catch (Exception ex)
        {
            diagnostics.Add($"[CHAIN-VALIDATOR] FATAL: Exception during validation: {ex.Message}");
        }

        return diagnostics;
    }

    /// <summary>
    /// Validate ability chains: each AbilityData.Chain should point to valid BaseCommandDef.
    /// </summary>
    private void ValidateAbilityChains()
    {
        var abilities = SDBInterface.GetAbilityDataDictionary();
        var baseCommands = SDBInterface.GetBaseCommandDefDictionary();

        foreach (var ability in abilities.Values)
        {
            if (ability.Chain == 0)
            {
                continue; // 0 is valid (no chain)
            }

            if (!baseCommands.ContainsKey(ability.Chain))
            {
                _results.Add(new ValidationResult
                {
                    Category = MissingNode,
                    TableName = "apt::BaseCommandDef",
                    ReferencedId = ability.Chain,
                    SourceId = ability.Id,
                    SourceField = "AbilityData.Chain",
                    Details = $"Ability {ability.Id} references missing chain entry {ability.Chain}"
                });
            }
            else
            {
                // Validate the entire chain
                ValidateChainSegment(ability.Chain, baseCommands, ability.Id, "AbilityData.Chain");
            }
        }
    }

    /// <summary>
    /// Validate status effect chains: ApplyChain, RemoveChain, UpdateChain, DurationChain.
    /// </summary>
    private void ValidateStatusEffectChains()
    {
        var effects = SDBInterface.GetStatusEffectDataDictionary();
        var baseCommands = SDBInterface.GetBaseCommandDefDictionary();

        foreach (var effect in effects.Values)
        {
            ValidateChainReference(effect.ApplyChain, baseCommands, effect.Id, "StatusEffectData.ApplyChain");
            ValidateChainReference(effect.RemoveChain, baseCommands, effect.Id, "StatusEffectData.RemoveChain");
            ValidateChainReference(effect.UpdateChain, baseCommands, effect.Id, "StatusEffectData.UpdateChain");
            ValidateChainReference(effect.DurationChain, baseCommands, effect.Id, "StatusEffectData.DurationChain");
        }
    }

    /// <summary>
    /// Validate branch command chains: IfChain, ThenChain, ElseChain.
    /// </summary>
    private void ValidateBranchChains()
    {
        var branches = SDBInterface.GetConditionalBranchCommandDefDictionary();
        var baseCommands = SDBInterface.GetBaseCommandDefDictionary();

        foreach (var branch in branches.Values)
        {
            ValidateChainReference(branch.IfChain, baseCommands, branch.Id, "ConditionalBranchCommandDef.IfChain");
            ValidateChainReference(branch.ThenChain, baseCommands, branch.Id, "ConditionalBranchCommandDef.ThenChain");
            ValidateChainReference(branch.ElseChain, baseCommands, branch.Id, "ConditionalBranchCommandDef.ElseChain");
        }
    }

    /// <summary>
    /// Validate while loop command chains: BodyChain, ConditionChain.
    /// </summary>
    private void ValidateLoopChains()
    {
        var loops = SDBInterface.GetWhileLoopCommandDefDictionary();
        var baseCommands = SDBInterface.GetBaseCommandDefDictionary();

        foreach (var loop in loops.Values)
        {
            ValidateChainReference(loop.BodyChain, baseCommands, loop.Id, "WhileLoopCommandDef.BodyChain");
            ValidateChainReference(loop.ConditionChain, baseCommands, loop.Id, "WhileLoopCommandDef.ConditionChain");
        }
    }

    /// <summary>
    /// Validate logic command chains: AndChain, OrChain, NegateChain, AChain, BChain.
    /// </summary>
    private void ValidateLogicChains()
    {
        var baseCommands = SDBInterface.GetBaseCommandDefDictionary();

        var andChains = SDBInterface.GetLogicAndChainCommandDefDictionary();
        foreach (var and in andChains.Values)
        {
            ValidateChainReference(and.AndChain, baseCommands, and.Id, "LogicAndChainCommandDef.AndChain");
        }

        var orChains = SDBInterface.GetLogicOrChainCommandDefDictionary();
        foreach (var or in orChains.Values)
        {
            ValidateChainReference(or.OrChain, baseCommands, or.Id, "LogicOrChainCommandDef.OrChain");
        }

        var negates = SDBInterface.GetLogicNegateCommandDefDictionary();
        foreach (var negate in negates.Values)
        {
            ValidateChainReference(negate.NegateChain, baseCommands, negate.Id, "LogicNegateCommandDef.NegateChain");
        }

        var orCmds = SDBInterface.GetLogicOrCommandDefDictionary();
        foreach (var orCmd in orCmds.Values)
        {
            ValidateChainReference(orCmd.AChain, baseCommands, orCmd.Id, "LogicOrCommandDef.AChain");
            ValidateChainReference(orCmd.BChain, baseCommands, orCmd.Id, "LogicOrCommandDef.BChain");
        }
    }

    /// <summary>
    /// Validate special command chains: ImpactToggleEffectCommandDef.PreApplyChain, etc.
    /// </summary>
    private void ValidateSpecialChains()
    {
        var baseCommands = SDBInterface.GetBaseCommandDefDictionary();

        var toggles = SDBInterface.GetImpactToggleEffectCommandDefDictionary();
        foreach (var toggle in toggles.Values)
        {
            ValidateChainReference(toggle.PreApplyChain, baseCommands, toggle.Id, "ImpactToggleEffectCommandDef.PreApplyChain");
        }

        var updateWaitFire = SDBInterface.GetUpdateWaitAndFireOnceCommandDefDictionary();
        foreach (var cmd in updateWaitFire.Values)
        {
            ValidateChainReference(cmd.Chain, baseCommands, cmd.Id, "UpdateWaitAndFireOnceCommandDef.Chain");
        }

        var regProx = SDBInterface.GetRegisterClientProximityCommandDefDictionary();
        foreach (var cmd in regProx.Values)
        {
            ValidateChainReference(cmd.Chain, baseCommands, cmd.Id, "RegisterClientProximityCommandDef.Chain");
        }
    }

    /// <summary>
    /// Validate item-to-ability bridges: AbilityModule.AbilityChainId should point to AbilityData.
    /// </summary>
    private void ValidateItemAbilityBridges()
    {
        var modules = SDBInterface.GetAbilityModuleDictionary();
        var abilities = SDBInterface.GetAbilityDataDictionary();

        foreach (var module in modules.Values)
        {
            if (module.AbilityChainId == 0)
            {
                continue; // 0 is valid (no ability)
            }

            if (!abilities.ContainsKey(module.AbilityChainId))
            {
                _results.Add(new ValidationResult
                {
                    Category = MissingAbility,
                    TableName = "apt::AbilityData",
                    ReferencedId = module.AbilityChainId,
                    SourceId = module.Id,
                    SourceField = "AbilityModule.AbilityChainId",
                    Details = $"AbilityModule {module.Id} references missing ability {module.AbilityChainId}"
                });
            }
        }
    }

    /// <summary>
    /// Helper: validate a single chain reference (checks if ID exists and traverses chain).
    /// </summary>
    private void ValidateChainReference(
        uint chainId,
        Dictionary<uint, BaseCommandDef> baseCommands,
        uint sourceId,
        string sourceField)
    {
        if (chainId == 0)
        {
            return; // 0 is valid (no chain)
        }

        if (!baseCommands.ContainsKey(chainId))
        {
            _results.Add(new ValidationResult
            {
                Category = MissingNode,
                TableName = "apt::BaseCommandDef",
                ReferencedId = chainId,
                SourceId = sourceId,
                SourceField = sourceField,
                Details = $"{sourceField} from {sourceId} references missing chain entry {chainId}"
            });
        }
        else
        {
            ValidateChainSegment(chainId, baseCommands, sourceId, sourceField);
        }
    }

    /// <summary>
    /// Traverse a chain starting at chainId and validate all Next pointers.
    /// Detects dangling links and stops at first issue in each chain.
    /// </summary>
    private void ValidateChainSegment(
        uint chainId,
        Dictionary<uint, BaseCommandDef> baseCommands,
        uint sourceId,
        string sourceField)
    {
        var visited = new HashSet<uint>();
        var current = chainId;

        while (current != 0)
        {
            if (visited.Contains(current))
            {
                // Cycle detected - might be intentional in some cases, but flag it
                _results.Add(new ValidationResult
                {
                    Category = CircularReference,
                    TableName = "apt::BaseCommandDef",
                    ReferencedId = current,
                    SourceId = sourceId,
                    SourceField = sourceField,
                    Details = $"Circular chain reference at {current} in chain from {sourceId}"
                });
                break;
            }

            visited.Add(current);

            if (!baseCommands.TryGetValue(current, out var cmd))
            {
                _results.Add(new ValidationResult
                {
                    Category = DanglingLink,
                    TableName = "apt::BaseCommandDef",
                    ReferencedId = current,
                    SourceId = sourceId,
                    SourceField = sourceField,
                    Details = $"Dangling chain link at {current} in chain from {sourceId} via {sourceField}"
                });
                break;
            }

            current = cmd.Next;
        }
    }

    /// <summary>
    /// Format validation results into diagnostic strings, grouped by category.
    /// Aggregates similar issues to reduce log spam.
    /// </summary>
    private List<string> FormatDiagnostics()
    {
        var output = new List<string>();

        if (_results.Count == 0)
        {
            output.Add("[CHAIN-VALIDATOR] OK: All chain references valid.");
            return output;
        }

        output.Add($"[CHAIN-VALIDATOR] Found {_results.Count} issue(s):");

        // Group by category
        var grouped = _results.GroupBy(r => r.Category);

        foreach (var group in grouped)
        {
            output.Add($"  {group.Key}: {group.Count()} issue(s)");

            // Limit detail output to first few per category
            var detailsToShow = group.Take(5).ToList();
            foreach (var result in detailsToShow)
            {
                output.Add($"    - {result.Details} (source={result.SourceId}, field={result.SourceField})");
            }

            if (group.Count() > 5)
            {
                output.Add($"    ... and {group.Count() - 5} more {group.Key} issue(s)");
            }
        }

        return output;
    }
}