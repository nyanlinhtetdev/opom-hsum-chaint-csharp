using HsumChaint.Shared;
using HsumChaint.Domain.Features.Monastery.DTOs;

namespace HsumChaint.Domain.Features.Monastery.ServiceInterfaces
{
    public interface IMonasteryService
    {
        Task<PagedResult<MonasterySpaceDto>> GetMonasteries(GetMonasteriesRequestDto request);
        Task<PagedResult<MonasterySpaceDto>> SearchMonasteries(SearchMonasteriesRequestDto request);
        Task<Result<MonasterySpaceDto>> CreateMonastery(int currentUserId, CreateMonasteryRequestDto request);
        Task<Result<MonasterySpaceDto>> UpdateMonastery(int currentUserId, UpdateMonasteryRequestDto request);
        Task<Result<bool>> DeleteMonastery(int currentUserId, DeleteMonasteryRequestDto request);
        Task<Result<MonasterySpaceDto>> GetMonastery(int currentUserId, GetMonasteryRequestDto request);
        Task<PagedResult<MonasterySpaceDto>> GetMyMonasteries(int currentUserId, PaginationRequest? pagination = null);
        Task<Result<InvitationResponseDto>> InviteMember(int currentUserId, InviteMemberRequestDto request);
        Task<Result<InvitationResponseDto>> RespondToInvitation(int currentUserId, RespondInvitationRequestDto request);
        Task<PagedResult<MonasteryMemberDto>> GetMembers(int currentUserId, GetMonasteryMembersRequestDto request);
        Task<Result<MonasteryMemberDto>> UpdateMemberRole(int currentUserId, UpdateMemberRoleRequestDto request);
        Task<Result<MonasteryMemberDto>> RemoveMember(int currentUserId, RemoveMemberRequestDto request);
    }
}
