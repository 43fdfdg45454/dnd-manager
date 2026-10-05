using System.Security.Claims;
using Dnd.Application.Common;
using Dnd.Infrastructure.Auth;

namespace Dnd.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id of the authenticated user, from the <c>sub</c> claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtClaimTypes.Subject), out var id)
            ? id
            : throw AppException.Unauthorized("La sesión no es válida.");
}
