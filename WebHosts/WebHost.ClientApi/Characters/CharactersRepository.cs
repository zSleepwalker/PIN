
public class CharactersRepository : ICharactersRepository
{
    private readonly IRinClient _rinClient;

    public CharactersRepository(IRinClient rinClient)
    {
        _rinClient = rinClient;
    }

    public async Task<CharactersList> GetCharactersAsync(IHeaderDictionary headers)
    {
        try
        {
            var json = await _rinClient.GetAsync("api/v2/characters/list", headers);
            var rinResp = JsonSerializer.Deserialize<RinCharacterList>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var list = new CharactersList
            {
                Characters = new List<Character>(),
                IsDev = rinResp.IsDev,
                RbBalance = rinResp.RbBalance,
                NameChangeCost = rinResp.NameChangeCost
            };

            foreach (var rc in rinResp.Characters)
            {
                list.Characters.Add(MapCharacter(rc));
            }

            return list;
        }
        catch (Exception ex)
        {
            // Fallback or log error
            return new CharactersList { Characters = new List<Character>() };
        }
    }

    private Character MapCharacter(RinCharacter rc)
    {
        // Mapping logic from RIN character model to PIN character model
        return new Character
        {
            CharacterGuid = rc.CharacterGuid,
            Name = rc.Name,
            UniqueName = rc.UniqueName,
            IsDev = rc.IsDev,
            IsActive = rc.IsActive,
            CreatedAt = rc.CreatedAt,
            TitleId = rc.TitleId,
            TimePlayedSecs = rc.TimePlayedSecs,
            NeedsNameChange = rc.NeedsNameChange,
            MaxFrameLevel = rc.MaxFrameLevel,
            FrameSdbId = rc.FrameSdbId,
            CurrentLevel = rc.CurrentLevel,
            IsVip = rc.IsVip,
            VipRank = rc.VipRank,
            Gender = rc.Gender,
            CurrentGender = rc.CurrentGender,
            EliteRank = rc.EliteRank,
            LastSeenAt = rc.LastSeenAt,
            Visuals = MapVisuals(rc.Visuals),
            Race = rc.Race
        };
    }

    private Visuals MapVisuals(RinVisuals rv)
    {
        if (rv == null) return null;
        return new Visuals
        {
            Id = rv.Id,
            Race = rv.Race,
            Gender = rv.Gender,
            SkinColor = MapColoredItem(rv.SkinColor),
            VoiceSet = MapItem(rv.VoiceSet),
            Head = MapItem(rv.Head),
            EyeColor = MapColoredItem(rv.EyeColor),
            LipColor = MapColoredItem(rv.LipColor),
            HairColor = MapColoredItem(rv.HairColor),
            FacialHairColor = MapColoredItem(rv.FacialHairColor),
            HeadAccessories = MapColoredItems(rv.HeadAccessories),
            Ornaments = MapItems(rv.Ornaments),
            Eyes = MapItem(rv.Eyes),
            Hair = MapHairItem(rv.Hair),
            FacialHair = MapHairItem(rv.FacialHair),
            Glider = MapItem(rv.Glider),
            Vehicle = MapItem(rv.Vehicle),
            WarpaintId = rv.WarpaintId,
            Warpaint = rv.Warpaint
        };
    }

    private ColoredItem MapColoredItem(RinColoredItem rci) => rci == null ? null : new ColoredItem { Id = rci.Id, Value = rci.Value == null ? null : new ColorValue { Color = rci.Value.Color } };
    private Item MapItem(RinItem ri) => ri == null ? null : new Item { Id = ri.Id };
    private List<ColoredItem> MapColoredItems(List<RinColoredItem> list) => list?.ConvertAll(MapColoredItem);
    private List<Item> MapItems(List<RinItem> list) => list?.ConvertAll(MapItem);
    private HairItem MapHairItem(RinHairItem rhi) => rhi == null ? null : new HairItem { Id = rhi.Id, Color = rhi.Color == null ? null : new ColorItem { Id = rhi.Color.Id, Value = rhi.Color.Value } };

}

// Helper models for deserializing RIN response
public class RinCharacterList
{
    public List<RinCharacter> Characters { get; set; }
    public bool IsDev { get; set; }
    public uint RbBalance { get; set; }
    public uint NameChangeCost { get; set; }
}

public class RinCharacter
{
    public ulong CharacterGuid { get; set; }
    public string Name { get; set; }
    public string UniqueName { get; set; }
    public bool IsDev { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public uint TitleId { get; set; }
    public uint TimePlayedSecs { get; set; }
    public bool NeedsNameChange { get; set; }
    public byte MaxFrameLevel { get; set; }
    public uint FrameSdbId { get; set; }
    public byte CurrentLevel { get; set; }
    public bool IsVip { get; set; }
    public int VipRank { get; set; }
    public byte Gender { get; set; }
    public string CurrentGender { get; set; }
    public uint EliteRank { get; set; }
    public DateTime LastSeenAt { get; set; }
    public RinVisuals Visuals { get; set; }
    public string Race { get; set; }
}

public class RinVisuals
{
    public int Id { get; set; }
    public int Race { get; set; }
    public int Gender { get; set; }
    public RinColoredItem SkinColor { get; set; }
    public RinItem VoiceSet { get; set; }
    public RinItem Head { get; set; }
    public RinColoredItem EyeColor { get; set; }
    public RinColoredItem LipColor { get; set; }
    public RinColoredItem HairColor { get; set; }
    public RinColoredItem FacialHairColor { get; set; }
    public List<RinColoredItem> HeadAccessories { get; set; }
    public List<RinItem> Ornaments { get; set; }
    public RinItem Eyes { get; set; }
    public RinHairItem Hair { get; set; }
    public RinHairItem FacialHair { get; set; }
    public RinItem Glider { get; set; }
    public RinItem Vehicle { get; set; }
    public uint WarpaintId { get; set; }
    public List<long> Warpaint { get; set; }
}

public class RinColoredItem { public uint Id { get; set; } public RinColorValue Value { get; set; } }
public class RinColorValue { public uint Color { get; set; } }
public class RinItem { public uint Id { get; set; } }
public class RinHairItem { public uint Id { get; set; } public RinColorItem Color { get; set; } }
public class RinColorItem { public uint Id { get; set; } public uint Value { get; set; } }