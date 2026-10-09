using System.Security.Claims;
using TrafficFine.Web.Domain;
namespace TrafficFine.Web.Controllers;
internal static class ActorExtensions
{
    internal static Actor ToActor(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Oturum gerekli."),
        user.Identity?.Name ?? "Kullanıcı",
        user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet());
}
