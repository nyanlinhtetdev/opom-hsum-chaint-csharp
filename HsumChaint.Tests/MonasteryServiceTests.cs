using System;
using System.Linq;
using System.Threading.Tasks;
using HsumChaint.Database.Models;
using HsumChaint.Domain.Features.Monastery.DTOs;
using HsumChaint.Domain.Features.Monastery.Services;
using HsumChaint.Shared.Authorization;
using HsumChaint.Shared.CommonEnum;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HsumChaint.Tests;

public class MonasteryServiceTests
{
    [Fact]
    public async Task CustomRole_GranularPermissions_ControlMembershipOperationsSeparately()
    {
        await using var db = CreateDbContext();
        SeedUser(db, 1, "Owner", "091", roleId: 2);
        SeedUser(db, 2, "Custom Actor", "092", roleId: 3);
        SeedUser(db, 3, "Invitee", "093", roleId: 3);
        SeedMonastery(db, 10, 1);
        var customRole = new SpaceRole { Id = 20, Name = "Custom Actor" };
        db.SpaceRoles.Add(customRole);
        db.MonasteryMembers.Add(new MonasteryMember { UserId = 2, MonasterySpaceId = 10, SpaceRoleId = 20, SpaceRole = customRole });
        await db.SaveChangesAsync();
        var service = new MonasteryService(db);
        var request = new InviteMemberRequestDto { UserId = 3, SpaceRoleId = SpaceRoles.ViewerId };

        // 1. Without SpaceInviteMember permission, inviting fails
        Assert.False((await service.InviteMember(2, 10, request)).IsSuccess);

        // 2. Grant SpaceInviteMember permission only
        var invitePerm = db.SpacePermissions.Single(p => p.Name == SpacePermissions.SpaceInviteMember);
        var inviteMapping = new SpaceRolePermission { SpaceRoleId = 20, SpacePermissionId = invitePerm.Id, SpaceRole = customRole, SpacePermission = invitePerm };
        db.SpaceRolePermissions.Add(inviteMapping);
        await db.SaveChangesAsync();

        // 3. Now invite succeeds
        Assert.True((await service.InviteMember(2, 10, request)).IsSuccess);
        Assert.Equal("Viewer", (await service.RespondToInvitation(3, db.Invitations.Single().Id,
            new() { Status = InvitationStatus.Accept })).Data!.SpaceRoleName);

        // 4. BUT UpdateMemberRole and RemoveMember FAIL because they are separate permissions!
        Assert.False((await service.UpdateMemberRole(2, 10, 3, new() { SpaceRoleId = SpaceRoles.EditorId })).IsSuccess);
        Assert.False((await service.RemoveMember(2, 10, 3)).IsSuccess);

        // 5. Grant SpaceUpdateMemberRole and SpaceRemoveMember permissions
        var updatePerm = db.SpacePermissions.Single(p => p.Name == SpacePermissions.SpaceUpdateMemberRole);
        var removePerm = db.SpacePermissions.Single(p => p.Name == SpacePermissions.SpaceRemoveMember);
        db.SpaceRolePermissions.AddRange(
            new SpaceRolePermission { SpaceRoleId = 20, SpacePermissionId = updatePerm.Id, SpaceRole = customRole, SpacePermission = updatePerm },
            new SpaceRolePermission { SpaceRoleId = 20, SpacePermissionId = removePerm.Id, SpaceRole = customRole, SpacePermission = removePerm }
        );
        await db.SaveChangesAsync();

        // 6. Now UpdateMemberRole and RemoveMember succeed!
        Assert.True((await service.UpdateMemberRole(2, 10, 3, new() { SpaceRoleId = SpaceRoles.EditorId })).IsSuccess);
        Assert.Equal("Editor", (await service.GetMembers(2, 10)).Data!.Single(m => m.UserId == 3).SpaceRoleName);
        Assert.True((await service.RemoveMember(2, 10, 3)).IsSuccess);
    }

    [Fact]
    public async Task CreateMonastery_CreatesOwnerMembership()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner", "091", roleId: 2);
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var response = await service.CreateMonastery(1, new CreateMonasteryRequestDto
        {
            MonasteryName = "Aung Myae",
            Address = "Yangon"
        });

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Data);
        Assert.Equal(SpaceRoles.OwnerId, response.Data!.CurrentUserRoleId);
        Assert.Equal(SpaceRoles.Owner, response.Data.CurrentUserRoleName);
        Assert.True(dbContext.MonasteryMembers.Single().IsOwner);
    }

    [Fact]
    public async Task InviteMember_CreatesPendingInvitationForExistingUser()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner", "091", roleId: 2);
        SeedUser(dbContext, 2, "Editor", "092", roleId: 3);
        SeedMonastery(dbContext, 10, 1);
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var response = await service.InviteMember(1, 10, new InviteMemberRequestDto
        {
            UserId = 2,
            SpaceRoleId = SpaceRoles.EditorId
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(InvitationStatus.Pending, response.Data!.Status);
        Assert.Equal(SpaceRoles.EditorId, response.Data.SpaceRoleId);
        Assert.Single(dbContext.Notifications);
    }

    [Fact]
    public async Task RespondToInvitation_Accept_CreatesMember()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner", "091", roleId: 2);
        SeedUser(dbContext, 2, "Editor", "092", roleId: 3);
        SeedMonastery(dbContext, 10, 1);
        dbContext.Invitations.Add(new Invitation
        {
            Id = 50,
            MonasterySpaceId = 10,
            InvitedUserId = 2,
            InvitedById = 1,
            SpaceRoleId = SpaceRoles.EditorId,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var response = await service.RespondToInvitation(2, 50, new RespondInvitationRequestDto
        {
            Status = InvitationStatus.Accept
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(InvitationStatus.Accept, response.Data!.Status);
        Assert.Contains(dbContext.MonasteryMembers, x => x.UserId == 2 && x.SpaceRoleId == SpaceRoles.EditorId);
    }

    [Fact]
    public async Task UpdateMemberRole_RejectsViewerActor()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner", "091", roleId: 2);
        SeedUser(dbContext, 2, "Viewer", "092", roleId: 3);
        SeedUser(dbContext, 3, "Editor", "093", roleId: 3);
        SeedMonastery(dbContext, 10, 1);
        dbContext.MonasteryMembers.AddRange(
            new MonasteryMember { UserId = 2, MonasterySpaceId = 10, SpaceRoleId = SpaceRoles.ViewerId, IsOwner = false },
            new MonasteryMember { UserId = 3, MonasterySpaceId = 10, SpaceRoleId = SpaceRoles.EditorId, IsOwner = false });
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var response = await service.UpdateMemberRole(2, 10, 3, new UpdateMemberRoleRequestDto
        {
            SpaceRoleId = SpaceRoles.AdminId
        });

        Assert.False(response.IsSuccess);
        Assert.Contains("not authorized", response.Message);
    }

    [Fact]
    public async Task DeleteMonastery_OwnerMonkSucceeds_AndHidesMonasteryFromQueries()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner Monk", "091", roleId: 2);
        SeedUser(dbContext, 2, "Invitee", "092", roleId: 3);
        SeedMonastery(dbContext, 10, 1);
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var deleteResponse = await service.DeleteMonastery(1, 10);

        Assert.True(deleteResponse.IsSuccess);
        Assert.True(deleteResponse.Data);
        Assert.True(dbContext.MonasterySpaces.Single(x => x.Id == 10).IsDeleted);

        var getResponse = await service.GetMonastery(1, 10);
        Assert.False(getResponse.IsSuccess);

        var myMonasteries = await service.GetMyMonasteries(1);
        Assert.Empty(myMonasteries.Data);

        var updateResponse = await service.UpdateMonastery(1, 10, new UpdateMonasteryRequestDto { MonasteryName = "Renamed" });
        Assert.False(updateResponse.IsSuccess);

        var inviteResponse = await service.InviteMember(1, 10, new InviteMemberRequestDto { UserId = 2, SpaceRoleId = SpaceRoles.EditorId });
        Assert.False(inviteResponse.IsSuccess);
    }

    [Fact]
    public async Task DeleteMonastery_RejectsNonOwnerAdmin_NonOwnerMonk_AndNonMonkOwner()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner Monk", "091", roleId: 2);
        SeedUser(dbContext, 2, "Space Admin", "092", roleId: 3);
        SeedUser(dbContext, 3, "Other Monk", "093", roleId: 2);
        SeedUser(dbContext, 4, "NonMonk Owner", "094", roleId: 3);
        SeedMonastery(dbContext, 10, 1);
        dbContext.MonasteryMembers.AddRange(
            new MonasteryMember { UserId = 2, MonasterySpaceId = 10, SpaceRoleId = SpaceRoles.AdminId, IsOwner = false },
            new MonasteryMember { UserId = 3, MonasterySpaceId = 10, SpaceRoleId = SpaceRoles.ViewerId, IsOwner = false });
        dbContext.MonasterySpaces.Add(new MonasterySpace
        {
            Id = 20,
            MonasteryName = "Admin Space",
            CreatedById = 4
        });
        dbContext.MonasteryMembers.Add(new MonasteryMember
        {
            UserId = 4,
            MonasterySpaceId = 20,
            SpaceRoleId = SpaceRoles.OwnerId,
            IsOwner = true
        });
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var adminAttempt = await service.DeleteMonastery(2, 10);
        Assert.False(adminAttempt.IsSuccess);
        Assert.Contains("not authorized", adminAttempt.Message);

        var nonOwnerMonkAttempt = await service.DeleteMonastery(3, 10);
        Assert.False(nonOwnerMonkAttempt.IsSuccess);
        Assert.Contains("not authorized", nonOwnerMonkAttempt.Message);

        var nonMonkOwnerAttempt = await service.DeleteMonastery(4, 20);
        Assert.False(nonMonkOwnerAttempt.IsSuccess);
        Assert.Contains("not authorized", nonMonkOwnerAttempt.Message);
    }

    [Fact]
    public async Task DeleteMonastery_NonExistentOrAlreadyDeleted_ReturnsNotFoundFailure()
    {
        await using var dbContext = CreateDbContext();
        SeedUser(dbContext, 1, "Owner Monk", "091", roleId: 2);
        SeedMonastery(dbContext, 10, 1);
        await dbContext.SaveChangesAsync();
        var service = new MonasteryService(dbContext);

        var missingAttempt = await service.DeleteMonastery(1, 999);
        Assert.False(missingAttempt.IsSuccess);
        Assert.Contains("not found", missingAttempt.Message, StringComparison.OrdinalIgnoreCase);

        Assert.True((await service.DeleteMonastery(1, 10)).IsSuccess);

        var secondAttempt = await service.DeleteMonastery(1, 10);
        Assert.False(secondAttempt.IsSuccess);
        Assert.Contains("not found", secondAttempt.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        RbacTestData.Seed(context);
        return context;
    }

    private static void SeedUser(AppDbContext dbContext, int id, string name, string phone, int roleId = 2)
    {
        dbContext.Users.Add(new HsumChaint.Database.Models.User
        {
            Id = id,
            Name = name,
            PhoneNumber = phone,
            Password = "pw",
            RoleId = roleId,
            IsDeleted = false
        });
    }

    private static void SeedMonastery(AppDbContext dbContext, int monasteryId, int ownerUserId)
    {
        dbContext.MonasterySpaces.Add(new MonasterySpace
        {
            Id = monasteryId,
            MonasteryName = "Aung Myae",
            CreatedById = ownerUserId
        });
        dbContext.MonasteryMembers.Add(new MonasteryMember
        {
            UserId = ownerUserId,
            MonasterySpaceId = monasteryId,
            SpaceRoleId = SpaceRoles.OwnerId,
            IsOwner = true
        });
    }
}
