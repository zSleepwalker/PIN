using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Web.Rin;

namespace WebHost.InGameApi.Controllers;

[ApiController]
public class CharactersData : ControllerBase
{
    private readonly IRinClient _rinClient;

    public CharactersData(IRinClient rinClient)
    {
        _rinClient = rinClient;
    }

    [Route("character/data")]
    [Route("api/v1/character/data")]
    [HttpGet]
    [Produces("application/json")]
    public async Task<object> Data()
    {
        try
        {
            var json = await _rinClient.GetAsync("api/v1/character/data", Request.Headers);
            return Content(json, "application/json");
        }
        catch (System.Exception)
        {
            // Fallback to static mock if RIN is down
            return new
            {
                CharacterGuid = 0x99aabbccddee0000 + 448,
                Name = "Sleepwalker",
                Redbux = 1094,
                Crystite = 4104594,
                Gender = 0,
                UniqueName = "SLEEPWALKER",
                Race = 0
            };
        }
    }

    [Route("api/v1/character_sheet.json")]
    [HttpGet]
    [Produces("application/json")]
    public async Task<object> CharacterSheet()
    {
        try
        {
            var json = await _rinClient.GetAsync("api/v1/character_sheet.json", Request.Headers);
            return Content(json, "application/json");
        }
        catch (System.Exception)
        {
            // Fallback to static mock
            return new 
            {
                Battleframe = new
                {
                    ItemSdbId = 76332,
                    Name = "Astrek \"Rhino\"",
                    WebIcon = "Rhino",
                    Constraints = new
                    {
                        Mass = new { Level = new { Total = 10, Current = 4 }, Value = new { Total = 1160, Current = 0 } },
                        Power = new { Level = new { Total = 10, Current = 4 }, Value = new { Total = 580, Current = 0 } },
                        Cpu = new { Level = new { Total = 10, Current = 4 }, Value = new { Total = 12, Current = 0 } }
                    },
                    Xp = new { CurrentXp = 477962, LifetimeXp = 731962 }
                }
            };
        }
    }

    [Route("api/v1/character_sheet/equipped_items.json")]
    [HttpGet]
    [Produces("application/json")]
    public object EquippedItems()
    {
        // Still hardcoded for now or proxy if implemented in RIN
        var equipped = new 
        {
            Primary = new { ItemId = string.Empty, DefaultItemSdbId = 78324, IsUnlocked = true },
            Secondary = new { ItemId = string.Empty, DefaultItemSdbId = 78043, IsUnlocked = true },
            Ability1 = new { ItemId = string.Empty, DefaultItemSdbId = 78326, IsUnlocked = true },
            Ability2 = new { ItemId = string.Empty, DefaultItemSdbId = 78328, IsUnlocked = true },
            Ability3 = new { ItemId = string.Empty, DefaultItemSdbId = 78330, IsUnlocked = true },
            Hkm = new { ItemId = "9186949129711219709", DefaultItemSdbId = 0, IsUnlocked = true },
            Passive = new { ItemId = "9190664271895347709", DefaultItemSdbId = 78334, IsUnlocked = true },
            Jumpjets = new { ItemId = string.Empty, DefaultItemSdbId = 78070, IsUnlocked = true },
            Servos = new { ItemId = string.Empty, DefaultItemSdbId = 78068, IsUnlocked = true },
            Backpack = new { ItemId = string.Empty, DefaultItemSdbId = 76018, IsUnlocked = true },
            Plating = new { ItemId = string.Empty, DefaultItemSdbId = 85203, IsUnlocked = true }
        };

        return equipped;
    }
}