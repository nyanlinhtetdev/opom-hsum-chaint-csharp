using System;
using System.Collections.Generic;

namespace HsumChaint.Shared.Authorization;

public static class SpacePermissions
{
    public const string SpaceView = "Space.View";
    public const string SpaceUpdate = "Space.Update";
    public const string SpaceDelete = "Space.Delete";
    public const string SpaceTransferOwnership = "Space.TransferOwnership";
    public const string SpaceInviteMember = "Space.InviteMember";
    public const string SpaceUpdateMemberRole = "Space.UpdateMemberRole";
    public const string SpaceRemoveMember = "Space.RemoveMember";
    public const string SpaceViewMembers = "Space.ViewMembers";

    public const string DonationView = "Donation.View";
    public const string DonationCreate = "Donation.Create";
    public const string DonationReview = "Donation.Review";
    public const string DonationSchedule = "Donation.Schedule";
    public const string DonationCancel = "Donation.Cancel";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        SpaceView, SpaceUpdate, SpaceDelete, SpaceTransferOwnership,
        SpaceInviteMember, SpaceUpdateMemberRole, SpaceRemoveMember, SpaceViewMembers,
        DonationView, DonationCreate, DonationReview, DonationSchedule, DonationCancel
    });
}
