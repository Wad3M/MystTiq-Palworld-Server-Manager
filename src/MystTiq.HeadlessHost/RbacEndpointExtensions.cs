using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MystTiq.Core.Operations;
using MystTiq.Core.Security;

namespace MystTiq.HeadlessHost;

// Minimal API IEndpointFilter -- no [Authorize]/policy DI machinery needed, since this app has
// none set up (routes are bare app.MapGet/app.MapPost closures). The auth middleware stashes the
// resolved MystTiqPrincipal on HttpContext.Items before routes run; when auth is disabled
// (today's default) every request resolves to MystTiqPrincipal.LegacyOwner, so RequireRole never
// rejects anything -- identical to today's "auth disabled = full access" behavior.
//
// v0.6.2.0: an optional `profile` parameter enables per-server scoping -- since each server
// profile's routes are mapped from its own MapProfileRoutes(...) call, the profile a given route
// belongs to is known at registration time (a closure capture), not something that needs
// resolving from the request at runtime. A principal with a non-null ScopedServerProfileId is
// rejected (403) on any route registered for a different profile.
public static class RbacEndpointExtensions
{
    public const string PrincipalItemKey = "mysttiq.principal";

    public static RouteHandlerBuilder RequireRole(this RouteHandlerBuilder builder, MystTiqRole minimum, ServerProfileId? profile = null) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var principal = context.HttpContext.Items[PrincipalItemKey] as MystTiqPrincipal;
            if (principal is null || principal.Role < minimum)
                return Results.Json(new { error = "insufficient-role", required = minimum.ToString() }, statusCode: StatusCodes.Status403Forbidden);

            if (profile is { } routeProfile && principal.ScopedServerProfileId is { } scope &&
                !scope.Equals(routeProfile.Value, StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { error = "server-scope-mismatch", scopedTo = scope }, statusCode: StatusCodes.Status403Forbidden);

            return await next(context);
        });
}
