using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
//
// v0.8.19.0: secure by default. An audit found 105 of 185 routes with no role at all, so any signed-in principal --
// a Viewer, or an account scoped to another server -- could run RCON commands, kick or ban, edit PalWorldSettings.ini or
// change mods. RequireRole now also records its role on the endpoint, and every server's route group gets a default
// (RequireRoleByDefault): a route that declares no role needs Viewer to read (GET) and Admin to change anything, and is
// scoped to its server. A route that should be open to a lower role says so with its own RequireRole.
public sealed record RequiredRoleMetadata(MystTiqRole Minimum);

public static class RbacEndpointExtensions
{
    public const string PrincipalItemKey = "mysttiq.principal";

    public static RouteHandlerBuilder RequireRole(this RouteHandlerBuilder builder, MystTiqRole minimum, ServerProfileId? profile = null) =>
        builder.WithMetadata(new RequiredRoleMetadata(minimum)).AddEndpointFilter(async (context, next) =>
            Check(context.HttpContext, minimum, profile) ?? await next(context));

    // Reading needs Viewer, changing needs Admin: the least a route that declares nothing can safely allow.
    public static MystTiqRole DefaultMinimum(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ? MystTiqRole.Viewer : MystTiqRole.Admin;

    public static RouteGroupBuilder RequireRoleByDefault(this RouteGroupBuilder group, ServerProfileId profile)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            // A route with its own RequireRole is checked by that filter (it may allow a lower role on purpose).
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<RequiredRoleMetadata>() is not null)
                return await next(context);
            return Check(context.HttpContext, DefaultMinimum(context.HttpContext.Request.Method), profile) ?? await next(context);
        });
        return group;
    }

    private static IResult? Check(HttpContext http, MystTiqRole minimum, ServerProfileId? profile)
    {
        var principal = http.Items[PrincipalItemKey] as MystTiqPrincipal;
        if (principal is null || principal.Role < minimum)
            return Results.Json(new { error = "insufficient-role", required = minimum.ToString() }, statusCode: StatusCodes.Status403Forbidden);

        if (profile is { } routeProfile && principal.ScopedServerProfileId is { } scope &&
            !scope.Equals(routeProfile.Value, StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = "server-scope-mismatch", scopedTo = scope }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }
}
