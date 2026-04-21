using System.Collections;
using System.Reflection;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.apt;
using GameServer.Data.SDB.Records.dbcharacter;
using GameServer.Data.SDB.Records.dbitems;
using SDB = FauFau.Formats.StaticDB;

var options = ParseArgs(args);
var command = options.Command;

if (string.IsNullOrWhiteSpace(command))
{
    PrintUsage();
    return;
}

var sdbPath = options.SdbPath ?? @"H:\MeldingWars\bin.db.ini\production\prod\prod-1962.0\system\db\clientdb.sd2";
var customDataRoot = options.CustomDataRoot ?? @"H:\PIN\UdpHosts\GameServer";

var sdb = new SDB();
sdb.Read(sdbPath);
SDBInterface.Init(sdb);
Directory.SetCurrentDirectory(customDataRoot);
CustomDBInterface.Init();
var knownTableNames = LoadKnownTableNames(customDataRoot);

switch (command.ToLowerInvariant())
{
    case "item":
        if (!options.TryGetUInt("id", out var itemId))
        {
            Console.WriteLine("Missing required --id <itemId> argument.");
            return;
        }

        DumpItem(itemId);
        break;

    case "effect":
        if (!options.TryGetUInt("id", out var effectId))
        {
            Console.WriteLine("Missing required --id <effectId> argument.");
            return;
        }

        DumpEffect(effectId);
        break;

    case "glider-profiles":
        DumpGliderProfiles();
        break;

    case "sdb-members":
        DumpObjectMembers(sdb);
        break;

    case "sdb-methods":
        DumpTypeMethods(typeof(SDB));
        break;

    case "tables":
        options.TryGetString("pattern", out var tablePattern);
        ListTables(sdb, knownTableNames, tablePattern);
        break;

    case "tables-raw":
        options.TryGetString("pattern", out var rawTablePattern);
        ListRawTables(sdb, rawTablePattern);
        break;

    case "table":
        var hasTableId = options.TryGetUInt("table-id", out var tableId);
        string? tableName = null;
        if (!hasTableId && !options.TryGetString("name", out tableName) && !options.TryGetString("table", out tableName))
        {
            Console.WriteLine("Missing required --name <tableName> or --table-id <tableId> argument.");
            return;
        }

        var hasRowId = options.TryGetUInt("id", out var rowId);
        var rowLimit = options.TryGetInt("limit", out var parsedLimit) ? parsedLimit : 5;
        options.TryGetString("field", out var filterField);
        options.TryGetString("equals", out var filterEquals);
        DumpTable(sdb, tableName, hasTableId ? tableId : null, hasRowId ? rowId : null, rowLimit, filterField, filterEquals);
        break;

    case "scan-id":
        if (!options.TryGetUInt("value", out var scanValue) && !options.TryGetUInt("id", out scanValue))
        {
            Console.WriteLine("Missing required --value <rowId> argument.");
            return;
        }

        options.TryGetString("field", out var scanField);
        ScanTablesForValue(sdb, scanValue, scanField);
        break;

    case "table-members":
        if (!options.TryGetString("name", out var membersTableName) && !options.TryGetString("table", out membersTableName))
        {
            Console.WriteLine("Missing required --name <tableName> argument.");
            return;
        }

        var membersTable = TryGetTable(sdb, membersTableName);
        if (membersTable == null)
        {
            Console.WriteLine($"Table '{membersTableName}' not found.");
            return;
        }

        DumpObjectMembers(membersTable);
        if (membersTable.Columns.Count > 0)
        {
            Console.WriteLine("FIRST COLUMN");
            DumpObjectMembers(membersTable.Columns[0]);
        }

        if (membersTable.Rows.Count > 0)
        {
            Console.WriteLine("FIRST ROW");
            DumpObjectMembers(membersTable.Rows[0]);
        }
        break;

    case "search-strings":
        if (!options.TryGetString("pattern", out var pattern))
        {
            Console.WriteLine("Missing required --pattern <text> argument.");
            return;
        }

        SearchStrings(sdb, pattern);
        break;

    default:
        Console.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        break;
}

return;

void DumpItem(uint itemId)
{
    var root = SDBInterface.GetRootItem(itemId);
    var module = SDBInterface.GetAbilityModule(itemId);
    var ability = module != null ? SDBInterface.GetAbilityData(module.AbilityChainId) : null;

    Console.WriteLine($"ITEM {itemId}");
    Console.WriteLine($"  Root: {(root == null ? "missing" : "present")}");
    if (root != null)
    {
        Console.WriteLine($"  Root.NameId={root.NameId}");
        Console.WriteLine($"  Root.ItemSubtype={root.ItemSubtype}");
        Console.WriteLine($"  Root.Type={root.Type}");
        Console.WriteLine($"  Root.ModuleTable={root.ModuleTable}");
    }

    Console.WriteLine($"  AbilityModule: {(module == null ? "missing" : "present")}");
    if (module != null)
    {
        Console.WriteLine($"  AbilityModule.AbilityChainId={module.AbilityChainId}");
        Console.WriteLine($"  AbilityModule.UiCategory={module.UiCategory}");
        Console.WriteLine($"  AbilityModule.ModuleType={module.ModuleType}");
        Console.WriteLine($"  AbilityModule.PowerLevel={module.PowerLevel}");
    }

    Console.WriteLine($"  Ability: {(ability == null ? "missing" : "present")}");
    if (ability != null)
    {
        Console.WriteLine($"  Ability.Id={ability.Id}");
        Console.WriteLine($"  Ability.Chain={ability.Chain}");
        Console.WriteLine($"  Ability.LocalizedNameId={ability.LocalizedNameId}");
    }

    if (ability == null)
    {
        return;
    }

    Console.WriteLine("  AbilityChain");
    WalkChain(ability.Chain, 2, new HashSet<uint>(), new HashSet<uint>());
}

void DumpEffect(uint effectId)
{
    var effect = SDBInterface.GetStatusEffectData(effectId);
    Console.WriteLine($"EFFECT {effectId}");
    Console.WriteLine($"  StatusEffectData: {(effect == null ? "missing" : "present")}");
    if (effect == null)
    {
        return;
    }

    Console.WriteLine($"  ApplyChain={effect.ApplyChain}");
    Console.WriteLine($"  RemoveChain={effect.RemoveChain}");
    Console.WriteLine($"  UpdateChain={effect.UpdateChain}");
    Console.WriteLine($"  DurationChain={effect.DurationChain}");

    if (effect.ApplyChain != 0)
    {
        Console.WriteLine("  ApplyChainWalk");
        WalkChain(effect.ApplyChain, 2, new HashSet<uint>(), new HashSet<uint>());
    }

    if (effect.RemoveChain != 0)
    {
        Console.WriteLine("  RemoveChainWalk");
        WalkChain(effect.RemoveChain, 2, new HashSet<uint>(), new HashSet<uint>());
    }

    if (effect.DurationChain != 0)
    {
        Console.WriteLine("  DurationChainWalk");
        WalkChain(effect.DurationChain, 2, new HashSet<uint>(), new HashSet<uint>());
    }
}

void DumpGliderProfiles()
{
    var profiles = LoadStaticDb<GliderParameters>("dbcharacter::GliderParameters")
        .OrderBy(profile => profile.Id)
        .ToArray();

    Console.WriteLine($"GLIDER PROFILES ({profiles.Length})");
    foreach (var profile in profiles)
    {
        Console.WriteLine($"  Id={profile.Id} TurnRate={profile.TurnRate} ThrustAccel={profile.ThrustAccel} ThrustMaxSpeed={profile.ThrustMaxSpeed} Efficiency={profile.Efficiency} PlaneMode={profile.PlaneMode}");
    }
}

void ListTables(SDB sdbInstance, IReadOnlyCollection<string> knownTableNames, string? pattern)
{
    var matches = knownTableNames
        .Select(name => new { Name = name, Table = TryGetTable(sdbInstance, name) })
        .Where(entry => entry.Table != null)
        .Where(entry => string.IsNullOrWhiteSpace(pattern) || entry.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
        .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"TABLES ({matches.Length} matches)");
    foreach (var match in matches)
    {
        Console.WriteLine($"  {match.Name} ({match.Table!.Id})");
    }
}

void ListRawTables(SDB sdbInstance, string? pattern)
{
    var tablesValue = TryGetMemberValueByName(sdbInstance, "Tables") ?? TryGetMemberValueByName(sdbInstance, "tables");
    if (tablesValue is not IEnumerable tables)
    {
        Console.WriteLine("Could not enumerate raw SDB tables.");
        return;
    }

    var matches = new List<(uint TableId, int RowCount, int ColumnCount)>();
    foreach (var table in tables)
    {
        if (table == null || !TryConvertToUInt(TryGetMemberValueByName(table, "Id"), out var rawTableId))
        {
            continue;
        }

        var rawTableIdText = rawTableId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(pattern) &&
            !rawTableIdText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var rowCount = (TryGetMemberValueByName(table, "Rows") as IEnumerable)?.Cast<object?>().Count() ?? 0;
        var columnCount = (TryGetMemberValueByName(table, "Columns") as IEnumerable)?.Cast<object?>().Count() ?? 0;
        matches.Add((rawTableId, rowCount, columnCount));
    }

    Console.WriteLine($"RAW TABLES ({matches.Count} matches)");
    foreach (var match in matches.OrderBy(entry => entry.TableId))
    {
        Console.WriteLine($"  TableId={match.TableId} RowCount={match.RowCount} ColumnCount={match.ColumnCount}");
    }
}

void DumpTable(SDB sdbInstance, string? tableName, uint? tableId, uint? rowId, int limit, string? filterField = null, string? filterEquals = null)
{
    var table = tableId.HasValue ? TryGetTableById(sdbInstance, tableId.Value) : TryGetTable(sdbInstance, tableName!);
    if (table == null)
    {
        Console.WriteLine(tableId.HasValue
            ? $"Table with id '{tableId.Value}' not found."
            : $"Table '{tableName}' not found.");
        return;
    }

    var safeLimit = Math.Max(limit, 1);
    var columnNames = TryGetColumnNames(table);
    var idColumnIndex = FindIdColumnIndex(columnNames);
    var hasFieldFilter = !string.IsNullOrWhiteSpace(filterField) && filterEquals != null;
    var fieldFilterIndex = hasFieldFilter ? ResolveColumnIndex(columnNames, filterField!) : -1;

    Console.WriteLine(tableId.HasValue ? $"TABLE_ID {tableId.Value}" : $"TABLE {tableName}");
    Console.WriteLine($"  RowCount={table.Rows.Count}");
    Console.WriteLine($"  ColumnCount={(columnNames.Count == 0 ? table.Rows.Cast<SDB.Row>().Select(row => row.Fields.Count).DefaultIfEmpty(0).Max() : columnNames.Count)}");
    if (columnNames.Count > 0)
    {
        Console.WriteLine($"  Columns={string.Join(", ", columnNames)}");
    }
    if (hasFieldFilter)
    {
        Console.WriteLine($"  Filter={filterField} == {filterEquals}");
    }

    if (rowId.HasValue && idColumnIndex == -1)
    {
        Console.WriteLine("  Could not identify an 'Id' column for row filtering.");
        return;
    }

    if (hasFieldFilter && fieldFilterIndex < 0)
    {
        Console.WriteLine($"  Could not resolve filter column '{filterField}'.");
        return;
    }

    var matches = new List<(int Index, SDB.Row Row)>();
    for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
    {
        var row = table.Rows[rowIndex];
        if (rowId.HasValue)
        {
            if (idColumnIndex >= row.Fields.Count || !TryConvertToUInt(row[idColumnIndex], out var currentRowId) || currentRowId != rowId.Value)
            {
                continue;
            }
        }

        if (hasFieldFilter)
        {
            if (fieldFilterIndex >= row.Fields.Count || !MatchesFilter(row[fieldFilterIndex], filterEquals!))
            {
                continue;
            }
        }

        matches.Add((rowIndex, row));
        if (matches.Count >= safeLimit)
        {
            break;
        }
    }

    Console.WriteLine($"  MatchedRows={matches.Count}");
    if (matches.Count == 0)
    {
        return;
    }

    foreach (var match in matches)
    {
        Console.WriteLine($"  Row[{match.Index}]");
        for (var fieldIndex = 0; fieldIndex < match.Row.Fields.Count; fieldIndex++)
        {
            var columnName = fieldIndex < columnNames.Count ? columnNames[fieldIndex] : $"column_{fieldIndex}";
            Console.WriteLine($"    {columnName}={FormatValue(match.Row[fieldIndex])}");
        }
    }
}

void ScanTablesForValue(SDB sdbInstance, uint value, string? filterField)
{
    var tablesValue = TryGetMemberValueByName(sdbInstance, "Tables") ?? TryGetMemberValueByName(sdbInstance, "tables");
    if (tablesValue is not IEnumerable tables)
    {
        Console.WriteLine("Could not enumerate raw SDB tables.");
        return;
    }

    var matches = new List<string>();
    foreach (var table in tables)
    {
        if (table == null || !TryConvertToUInt(TryGetMemberValueByName(table, "Id"), out var rawTableId))
        {
            continue;
        }

        var rowsValue = TryGetMemberValueByName(table, "Rows") ?? TryGetMemberValueByName(table, "rows");
        if (rowsValue is not IEnumerable rows)
        {
            continue;
        }

        var columns = table is SDB.Table typedTable ? TryGetColumnNames(typedTable) : Array.Empty<string>();
        var fieldIndexes = !string.IsNullOrWhiteSpace(filterField)
            ? new[] { Math.Max(ResolveColumnIndex(columns, filterField!), 0) }
            : Array.Empty<int>();

        var rowIndex = 0;
        foreach (var row in rows)
        {
            var fieldsValue = TryGetMemberValueByName(row!, "Fields") ?? TryGetMemberValueByName(row!, "fields");
            if (fieldsValue is not IEnumerable fieldEnumerable)
            {
                rowIndex++;
                continue;
            }

            var fieldArray = fieldEnumerable.Cast<object?>().ToArray();
            var indexesToCheck = fieldIndexes.Length > 0 ? fieldIndexes : Enumerable.Range(0, fieldArray.Length);
            foreach (var fieldIndex in indexesToCheck)
            {
                if (fieldIndex >= fieldArray.Length)
                {
                    continue;
                }

                if (TryConvertToUInt(fieldArray[fieldIndex], out var fieldValue) && fieldValue == value)
                {
                    matches.Add($"  TableId={rawTableId} RowIndex={rowIndex} ColumnIndex={fieldIndex} FieldCount={fieldArray.Length}");
                }
            }

            rowIndex++;
        }
    }

    Console.WriteLine($"SCAN_ID {value}");
    Console.WriteLine($"  Matches={matches.Count}");
    foreach (var match in matches)
    {
        Console.WriteLine(match);
    }
}

void DumpObjectMembers(object instance)
{
    var type = instance.GetType();
    Console.WriteLine($"TYPE {type.FullName}");

    var members = type
        .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(member => member is FieldInfo || member is PropertyInfo property && property.GetIndexParameters().Length == 0)
        .OrderBy(member => member.Name, StringComparer.Ordinal)
        .ToArray();

    foreach (var member in members)
    {
        var value = TryGetMemberValue(instance, member);
        Console.WriteLine($"  {member.MemberType} {member.Name}: {SummarizeValue(value)}");
    }
}

void SearchStrings(SDB sdbInstance, string pattern)
{
    var field = typeof(SDB).GetField("stringHashLookup", BindingFlags.NonPublic | BindingFlags.Instance);
    if (field?.GetValue(sdbInstance) is not IDictionary dictionary)
    {
        Console.WriteLine("Could not access StaticDB string hash lookup.");
        return;
    }

    var matches = new List<string>();
    foreach (DictionaryEntry entry in dictionary)
    {
        var keyText = entry.Key?.ToString() ?? string.Empty;
        var valueText = entry.Value?.ToString() ?? string.Empty;
        if (keyText.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            valueText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            matches.Add($"  {keyText} => {valueText}");
        }
    }

    Console.WriteLine($"STRING SEARCH '{pattern}' ({matches.Count} matches)");
    foreach (var match in matches.OrderBy(match => match, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(match);
    }
}

void WalkChain(uint chainId, int indent, HashSet<uint> visitedChains, HashSet<uint> visitedAbilities)
{
    if (chainId == 0)
    {
        Console.WriteLine($"{new string(' ', indent)}<chain 0>");
        return;
    }

    if (!visitedChains.Add(chainId))
    {
        Console.WriteLine($"{new string(' ', indent)}<chain {chainId} already visited>");
        return;
    }

    uint current = chainId;
    while (current != 0)
    {
        var baseDef = SDBInterface.GetBaseCommandDef(current);
        if (baseDef == null)
        {
            Console.WriteLine($"{new string(' ', indent)}<missing base command {current}>");
            return;
        }

        Console.WriteLine($"{new string(' ', indent)}Command {baseDef.Id} subtype={baseDef.Subtype} next={baseDef.Next}");

        switch ((GameServer.Aptitude.CommandType)baseDef.Subtype)
        {
            case GameServer.Aptitude.CommandType.ImpactApplyEffect:
            {
                var apply = SDBInterface.GetImpactApplyEffectCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}ImpactApplyEffect effectId={apply?.EffectId}");
                break;
            }
            case GameServer.Aptitude.CommandType.ImpactToggleEffect:
            {
                var toggle = SDBInterface.GetImpactToggleEffectCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}ImpactToggleEffect preApplyChain={toggle?.PreApplyChain}");
                if (toggle?.PreApplyChain > 0)
                {
                    WalkChain(toggle.PreApplyChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.ConditionalBranch:
            {
                var branch = SDBInterface.GetConditionalBranchCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}ConditionalBranch if={branch?.IfChain} then={branch?.ThenChain} else={branch?.ElseChain}");
                if (branch != null)
                {
                    WalkChain(branch.IfChain, indent + 4, visitedChains, visitedAbilities);
                    WalkChain(branch.ThenChain, indent + 4, visitedChains, visitedAbilities);
                    WalkChain(branch.ElseChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.Call:
            {
                var call = SDBInterface.GetCallCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}Call abilityId={call?.AbilityId}");
                if (call?.AbilityId > 0 && visitedAbilities.Add(call.AbilityId))
                {
                    var calledAbility = SDBInterface.GetAbilityData(call.AbilityId);
                    Console.WriteLine($"{new string(' ', indent + 2)}CalledAbility chain={calledAbility?.Chain}");
                    if (calledAbility?.Chain > 0)
                    {
                        WalkChain(calledAbility.Chain, indent + 4, visitedChains, visitedAbilities);
                    }
                }

                break;
            }
            case GameServer.Aptitude.CommandType.WhileLoop:
            {
                var loop = SDBInterface.GetWhileLoopCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}WhileLoop body={loop?.BodyChain} condition={loop?.ConditionChain}");
                if (loop != null)
                {
                    WalkChain(loop.BodyChain, indent + 4, visitedChains, visitedAbilities);
                    WalkChain(loop.ConditionChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.LogicOr:
            {
                var logicOr = SDBInterface.GetLogicOrCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}LogicOr a={logicOr?.AChain} b={logicOr?.BChain}");
                if (logicOr != null)
                {
                    WalkChain(logicOr.AChain, indent + 4, visitedChains, visitedAbilities);
                    WalkChain(logicOr.BChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.LogicNegate:
            {
                var negate = SDBInterface.GetLogicNegateCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}LogicNegate chain={negate?.NegateChain}");
                if (negate?.NegateChain > 0)
                {
                    WalkChain(negate.NegateChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.LogicOrChain:
            {
                var orChain = SDBInterface.GetLogicOrChainCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}LogicOrChain chain={orChain?.OrChain}");
                if (orChain?.OrChain > 0)
                {
                    WalkChain(orChain.OrChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.LogicAndChain:
            {
                var andChain = SDBInterface.GetLogicAndChainCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}LogicAndChain chain={andChain?.AndChain}");
                if (andChain?.AndChain > 0)
                {
                    WalkChain(andChain.AndChain, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.StagedActivation:
            {
                var staged = SDBInterface.GetStagedActivationCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}StagedActivation selfEffectId={staged?.SelfEffectId}");
                if (staged?.SelfEffectId > 0)
                {
                    DumpNestedEffect(staged.SelfEffectId, indent + 4, visitedChains, visitedAbilities);
                }

                break;
            }
            case GameServer.Aptitude.CommandType.ForcePush:
            {
                var forcePush = SDBInterface.GetForcePushCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}ForcePush strength={forcePush?.Strength} loft={forcePush?.Loft} falloff={forcePush?.Falloff} impactPosition={forcePush?.ImpactPosition} doAnimation={forcePush?.DoAnimation} strengthRegop={forcePush?.StrengthRegop}");
                break;
            }
            case GameServer.Aptitude.CommandType.RegisterClientProximity:
            {
                var proximity = SDBInterface.GetRegisterClientProximityCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}RegisterClientProximity abilityId={proximity?.AbilityId} chain={proximity?.Chain} radius={proximity?.Radius} maxTargets={proximity?.MaxTargets} retryMs={proximity?.RetryInterval} radiusRegop={proximity?.RadiusRegop}");
                break;
            }
            case GameServer.Aptitude.CommandType.RegisterMovementEffect:
            {
                var movementEffect = SDBInterface.GetRegisterMovementEffectCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}RegisterMovementEffect statusfxId={movementEffect?.StatusfxId} movestateIndex={movementEffect?.MovestateIndex} onClient={movementEffect?.OnClient} onServer={movementEffect?.OnServer} sprinting={movementEffect?.Sprinting} reapply={movementEffect?.Reapply}");
                break;
            }
            case GameServer.Aptitude.CommandType.RequireHasEffect:
            {
                var requireHasEffect = SDBInterface.GetRequireHasEffectCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}RequireHasEffect effectId={requireHasEffect?.EffectId} stackCount={requireHasEffect?.StackCount} sameInitiator={requireHasEffect?.SameInitiator} negate={requireHasEffect?.Negate}");
                break;
            }
            case GameServer.Aptitude.CommandType.RequireMovestate:
            {
                var requireMovestate = SDBInterface.GetRequireMovestateCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}RequireMovestate standing={requireMovestate?.Standing} running={requireMovestate?.Running} walking={requireMovestate?.Walking} falling={requireMovestate?.Falling} sliding={requireMovestate?.Sliding} jetpack={requireMovestate?.Jetpack} gliding={requireMovestate?.Gliding} thruster={requireMovestate?.Thruster} stall={requireMovestate?.Stall} jetpackSprint={requireMovestate?.JetpackSprint} knockdownOnGround={requireMovestate?.KnockdownOnground} knockdownFalling={requireMovestate?.KnockdownFalling} negate={requireMovestate?.Negate}");
                break;
            }
            case GameServer.Aptitude.CommandType.SetGliderParametersDef:
            {
                var glider = CustomDBInterface.GetSetGliderParametersCommandDef(baseDef.Id);
                Console.WriteLine($"{new string(' ', indent + 2)}SetGliderParameters value={glider?.Value?.ToString() ?? "<null>"}");
                break;
            }
        }

        current = baseDef.Next;
    }
}

void DumpNestedEffect(uint effectId, int indent, HashSet<uint> visitedChains, HashSet<uint> visitedAbilities)
{
    var effect = SDBInterface.GetStatusEffectData(effectId);
    Console.WriteLine($"{new string(' ', indent)}Effect {effectId}: apply={effect?.ApplyChain} remove={effect?.RemoveChain} duration={effect?.DurationChain}");
    if (effect?.ApplyChain > 0)
    {
        WalkChain(effect.ApplyChain, indent + 2, visitedChains, visitedAbilities);
    }
}

static T[] LoadStaticDb<T>(string tableName)
    where T : class, new()
{
    var loaderType = typeof(GameServer.Data.SDB.StaticDBLoader);
    var method = loaderType.GetMethod("LoadStaticDB", BindingFlags.NonPublic | BindingFlags.Static);
    var generic = method!.MakeGenericMethod(typeof(T));
    return (T[])generic.Invoke(null, new object[] { tableName })!;
}

static void PrintUsage()
{
    Console.WriteLine("SdbInspect usage:");
    Console.WriteLine("  SdbInspect item --id <itemId> [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect effect --id <effectId> [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect glider-profiles [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect sdb-members [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect sdb-methods [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect tables [--pattern <text>] [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect tables-raw [--pattern <text>] [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect table --name <tableName> | --table-id <tableId> [--id <rowId>] [--limit <count>] [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect table-members --name <tableName> [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect scan-id --value <rowId> [--field <column>] [--sdb <path>] [--custom-root <path>]");
    Console.WriteLine("  SdbInspect search-strings --pattern <text> [--sdb <path>] [--custom-root <path>]");
}

static SDB.Table? TryGetTable(SDB sdbInstance, string tableName)
{
    try
    {
        return sdbInstance.GetTableByName(tableName);
    }
    catch (ArgumentOutOfRangeException)
    {
        return null;
    }
}

static SDB.Table? TryGetTableById(SDB sdbInstance, uint tableId)
{
    try
    {
        return sdbInstance.GetTableById(tableId);
    }
    catch (ArgumentOutOfRangeException)
    {
        return null;
    }
}

static object? TryGetMemberValue(object instance, MemberInfo member)
{
    try
    {
        return member switch
        {
            FieldInfo field => field.GetValue(instance),
            PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(instance),
            _ => null,
        };
    }
    catch
    {
        return null;
    }
}

static object? TryGetMemberValueByName(object instance, string memberName)
{
    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var property = instance.GetType().GetProperty(memberName, flags);
    if (property != null && property.GetIndexParameters().Length == 0)
    {
        try
        {
            return property.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    var field = instance.GetType().GetField(memberName, flags);
    if (field == null)
    {
        return null;
    }

    try
    {
        return field.GetValue(instance);
    }
    catch
    {
        return null;
    }
}

static string? GetNamedMemberValue(object instance, params string[] memberNames)
{
    foreach (var memberName in memberNames)
    {
        var value = TryGetMemberValueByName(instance, memberName);
        if (value == null)
        {
            continue;
        }

        var text = value.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
    }

    return null;
}

static IReadOnlyList<string> TryGetColumnNames(SDB.Table table)
{
    var columnsValue = TryGetMemberValueByName(table, "Columns") ?? TryGetMemberValueByName(table, "columns");
    if (columnsValue is not IEnumerable columns)
    {
        return Array.Empty<string>();
    }

    var names = new List<string>();
    foreach (var column in columns)
    {
        if (column == null)
        {
            names.Add($"column_{names.Count}");
            continue;
        }

        names.Add(GetNamedMemberValue(column, "Name", "ColumnName", "name") ?? $"column_{names.Count}");
    }

    return names;
}

static int FindIdColumnIndex(IReadOnlyList<string> columnNames)
{
    for (var index = 0; index < columnNames.Count; index++)
    {
        if (string.Equals(columnNames[index], "id", StringComparison.OrdinalIgnoreCase))
        {
            return index;
        }
    }

    return columnNames.Count > 0 ? 0 : -1;
}

static bool TryConvertToUInt(object? value, out uint result)
{
    switch (value)
    {
        case null:
            result = 0;
            return false;
        case uint uintValue:
            result = uintValue;
            return true;
        case ushort ushortValue:
            result = ushortValue;
            return true;
        case byte byteValue:
            result = byteValue;
            return true;
        case int intValue when intValue >= 0:
            result = (uint)intValue;
            return true;
        case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
            result = (uint)longValue;
            return true;
        case string text when uint.TryParse(text, out var parsed):
            result = parsed;
            return true;
        default:
            result = 0;
            return false;
    }
}

static string FormatValue(object? value)
{
    if (value == null)
    {
        return "<null>";
    }

    if (value is string text)
    {
        return text;
    }

    if (TryFormatVector3Like(value, out var vectorText))
    {
        return vectorText;
    }

    if (value is IEnumerable enumerable and not string)
    {
        var parts = new List<string>();
        var index = 0;
        foreach (var item in enumerable)
        {
            if (index++ >= 16)
            {
                parts.Add("...");
                break;
            }

            parts.Add(item?.ToString() ?? "<null>");
        }

        return $"[{string.Join(", ", parts)}]";
    }

    if (TryFormatObjectMembers(value, out var memberText))
    {
        return memberText;
    }

    return value.ToString() ?? string.Empty;
}

static bool TryFormatVector3Like(object value, out string formatted)
{
    formatted = string.Empty;

    var type = value.GetType();
    if (!TryReadFloatLikeMember(type, value, "X", out var x) ||
        !TryReadFloatLikeMember(type, value, "Y", out var y) ||
        !TryReadFloatLikeMember(type, value, "Z", out var z))
    {
        return false;
    }

    formatted = $"({x}, {y}, {z})";
    return true;
}

static bool TryReadFloatLikeMember(Type type, object instance, string memberName, out float value)
{
    value = 0;

    var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (field != null)
    {
        return TryConvertToFloat(field.GetValue(instance), out value);
    }

    var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (property == null || property.GetIndexParameters().Length != 0)
    {
        return false;
    }

    return TryConvertToFloat(property.GetValue(instance), out value);
}

static bool TryConvertToFloat(object? value, out float result)
{
    switch (value)
    {
        case float floatValue:
            result = floatValue;
            return true;
        case double doubleValue:
            result = (float)doubleValue;
            return true;
        case decimal decimalValue:
            result = (float)decimalValue;
            return true;
        case int intValue:
            result = intValue;
            return true;
        case long longValue:
            result = longValue;
            return true;
        case string text when float.TryParse(text, out var parsed):
            result = parsed;
            return true;
        default:
            result = 0;
            return false;
    }
}

static bool TryFormatObjectMembers(object value, out string formatted)
{
    formatted = string.Empty;

    var type = value.GetType();
    var rawText = value.ToString();
    if (!string.Equals(rawText, type.FullName, StringComparison.Ordinal) &&
        !string.Equals(rawText, type.Name, StringComparison.Ordinal))
    {
        return false;
    }

    var members = type
        .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(member => member is FieldInfo || member is PropertyInfo property && property.GetIndexParameters().Length == 0)
        .Where(member => !string.Equals(member.Name, "EqualityContract", StringComparison.Ordinal))
        .Select(member => new { member.Name, Value = TryGetMemberValue(value, member) })
        .Where(entry => entry.Value == null || IsSimpleValue(entry.Value.GetType()))
        .Take(8)
        .ToArray();

    if (members.Length == 0)
    {
        return false;
    }

    formatted = $"{{{string.Join(", ", members.Select(member => $"{member.Name}={member.Value ?? "<null>"}"))}}}";
    return true;
}

static bool IsSimpleValue(Type type)
{
    var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
    return underlyingType.IsPrimitive ||
        underlyingType.IsEnum ||
        underlyingType == typeof(decimal) ||
        underlyingType == typeof(string);
}

static string SummarizeValue(object? value)
{
    if (value == null)
    {
        return "<null>";
    }

    if (value is string text)
    {
        return $"string '{text}'";
    }

    if (value is IDictionary dictionary)
    {
        var firstEntry = dictionary.Count > 0 ? dictionary.Cast<DictionaryEntry>().FirstOrDefault() : default;
        var keyType = firstEntry.Key?.GetType().FullName ?? "?";
        var valueType = firstEntry.Value?.GetType().FullName ?? "?";
        return $"{value.GetType().FullName} count={dictionary.Count} sample=({keyType} -> {valueType})";
    }

    if (value is IEnumerable enumerable and not string)
    {
        var sample = enumerable.Cast<object?>().Take(3).Select(item => item?.GetType().FullName ?? "<null>").ToArray();
        return $"{value.GetType().FullName} sample=[{string.Join(", ", sample)}]";
    }

    return $"{value.GetType().FullName} value={value}";
}

static void DumpTypeMethods(Type type)
{
    Console.WriteLine($"TYPE {type.FullName}");
    var methods = type
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .OrderBy(method => method.Name, StringComparer.Ordinal)
        .ToArray();

    foreach (var method in methods)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}"));
        Console.WriteLine($"  Method {method.Name}({parameters}) -> {method.ReturnType.Name}");
    }
}

static string[] LoadKnownTableNames(string customDataRoot)
{
    var loaderPath = Path.Combine(customDataRoot, "StaticDB", "Loaders", "StaticDBLoader.cs");
    if (!File.Exists(loaderPath))
    {
        return Array.Empty<string>();
    }

    var loaderText = File.ReadAllText(loaderPath);
    var matches = System.Text.RegularExpressions.Regex.Matches(loaderText, "LoadStaticDB<[^>]+>\\(\"([^\"]+)\"\\)");
    var knownTableNames = new HashSet<string>(StringComparer.Ordinal);

    foreach (System.Text.RegularExpressions.Match match in matches)
    {
        if (!match.Success)
        {
            continue;
        }

        var tableName = match.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            continue;
        }

        knownTableNames.Add(tableName);
    }

    return knownTableNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
}

static ParsedArgs ParseArgs(string[] rawArgs)
{
    if (rawArgs.Length == 0)
    {
        return new ParsedArgs(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    var command = rawArgs[0];
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 1; index < rawArgs.Length; index++)
    {
        var token = rawArgs[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = token[2..];
        var value = index + 1 < rawArgs.Length && !rawArgs[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? rawArgs[++index]
            : "true";
        options[key] = value;
    }

    return new ParsedArgs(command, options);
}

static int ResolveColumnIndex(IReadOnlyList<string> columnNames, string filterField)
{
    if (int.TryParse(filterField, out var numericIndex) && numericIndex >= 0)
    {
        return numericIndex;
    }

    for (var index = 0; index < columnNames.Count; index++)
    {
        if (string.Equals(columnNames[index], filterField, StringComparison.OrdinalIgnoreCase))
        {
            return index;
        }
    }

    if (filterField.StartsWith("column_", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(filterField[7..], out var parsedIndex) &&
        parsedIndex >= 0)
    {
        return parsedIndex;
    }

    return -1;
}

static bool MatchesFilter(object? value, string expected)
{
    if (value == null)
    {
        return string.Equals(expected, "<null>", StringComparison.OrdinalIgnoreCase);
    }

    if (value is string text)
    {
        return string.Equals(text, expected, StringComparison.OrdinalIgnoreCase);
    }

    if (value is IFormattable formattable)
    {
        var invariant = formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        if (string.Equals(invariant, expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);
}

readonly record struct ParsedArgs(string? Command, Dictionary<string, string> Options)
{
    public string? SdbPath => Options.GetValueOrDefault("sdb");

    public string? CustomDataRoot => Options.GetValueOrDefault("custom-root");

    public bool TryGetUInt(string key, out uint value)
    {
        value = 0;
        return Options.TryGetValue(key, out var raw) && uint.TryParse(raw, out value);
    }

    public bool TryGetString(string key, out string value)
    {
        value = string.Empty;
        if (!Options.TryGetValue(key, out var raw))
        {
            return false;
        }

        value = raw;
        return true;
    }

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        return Options.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }
}