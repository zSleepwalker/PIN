namespace GameServer.Enums.Visuals;

/// <summary>
/// Visual property types stored in dbcharacter::MonsterVisualOption.
/// These map MonsterVisualOption.Type to the corresponding field in VisualsBlock.
/// </summary>
public enum MonsterVisualOptionType : int
{
    Gradient = 0,
    CziMap = 1,
    MorphWeight = 2,
}
