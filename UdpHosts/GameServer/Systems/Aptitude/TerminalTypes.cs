namespace GameServer.Aptitude;

public static class TerminalTypes
{
    public const byte BattleframeLoadoutSelector = 8;
    public const byte WareffortLeaderboard = 14;

    public static string GetName(int terminalType)
    {
        return terminalType switch
        {
            -1 => "UNKNOWN",
            1 => "ARMY",
            2 => "MATCHMAKER",
            3 => "MANUFACTURING",
            4 => "FORGING",
            5 => "NEWYOU",
            7 => "VENDOR",
            8 => "LOADOUT_SELECTOR",
            10 => "VIDEO",
            11 => "PLAYER_MARKET",
            12 => "VENDING_MACHINE",
            14 => "WAREFFORT_LEADERBOARD",
            16 => "WAR_MONUMENT",
            17 => "ARCFOLDER",
            18 => "EXPERIMENTAL_LOADOUTS",
            19 => "ITEM_UPGRADES",
            _ => $"UNMAPPED_{terminalType}"
        };
    }
}
