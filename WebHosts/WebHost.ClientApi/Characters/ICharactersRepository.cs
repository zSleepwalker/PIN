using System.Threading.Tasks;
using WebHost.ClientApi.Characters.Models;

namespace WebHost.ClientApi.Characters;

public interface ICharactersRepository
{
    Task<CharactersList> GetCharactersAsync(Microsoft.AspNetCore.Http.IHeaderDictionary headers);
}