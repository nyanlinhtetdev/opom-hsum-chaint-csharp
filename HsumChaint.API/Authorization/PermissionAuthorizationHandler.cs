using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HsumChaint.API.Extensions;
using HsumChaint.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HsumChaint.API.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(IHttpContextAccessor? httpContextAccessor = null, AppDbContext? dbContext = null) 
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var httpContext = httpContextAccessor?.HttpContext;
        var routeValues = httpContext?.GetRouteData().Values;

        int? monasterySpaceId = null;
        if (routeValues != null)
        {
            if (routeValues.TryGetValue("id", out var idVal) && int.TryParse(idVal?.ToString(), out var parsedId))
            {
                var path = httpContext?.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/api/v1/monasteries", StringComparison.OrdinalIgnoreCase))
                {
                    monasterySpaceId = parsedId;
                }
            }
            else if (routeValues.TryGetValue("monasterySpaceId", out var spaceVal) && int.TryParse(spaceVal?.ToString(), out var parsedSpaceId))
            {
                monasterySpaceId = parsedSpaceId;
            }
        }

        var currentUserId = context.User.GetCurrentUserId();

        if (monasterySpaceId.HasValue && currentUserId.HasValue && dbContext != null)
        {
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return;
            }

            var member = await dbContext.MonasteryMembers
                .Include(m => m.SpaceRole)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == currentUserId.Value && m.MonasterySpaceId == monasterySpaceId.Value);

            if (member != null)
            {
                if (member.IsOwner == true)
                {
                    context.Succeed(requirement);
                    return;
                }

                var hasSpacePerm = await dbContext.SpaceRolePermissions
                    .AsNoTracking()
                    .AnyAsync(srp => srp.SpaceRoleId == member.SpaceRoleId
                                  && !srp.IsDeleted
                                  && !srp.SpaceRole.IsDeleted
                                  && !srp.SpacePermission.IsDeleted
                                  && srp.SpacePermission.Name == requirement.Permission);

                if (hasSpacePerm)
                {
                    context.Succeed(requirement);
                    return;
                }
            }
        }

        if (context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
