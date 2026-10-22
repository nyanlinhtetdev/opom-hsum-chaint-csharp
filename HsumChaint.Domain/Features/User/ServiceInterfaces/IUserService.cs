using HsumChaint.Shared;
using HsumChaint.Domain.Features.User.DTOs;
using HsumChaint.Database.Models;

namespace HsumChaint.Domain.Features.User.ServiceInterfaces
{
    public interface IUserService
    {
        Task<PagedResult<UserDto>> GetAllUsers(PaginationRequest? pagination = null);

        Task<Result<UserDto>> GetUser(int id);

        Task<Result> PutUser(UserDto user);

        Task<Result> DeleteUser(int id);

        Task<PagedResult<InvitationDto>> GetUserInvitationList(int id, PaginationRequest? pagination = null);

        Task<PagedResult<InvitationDto>> GetInvitedByOtherList(int id, PaginationRequest? pagination = null);

        Task<PagedResult<NotificationDto>> GetUserNotificationList(int id, PaginationRequest? pagination = null);

        Task<Result> DeleteUserNotificationList(int id);

        Task<Result<MonkProfileDto>> GetMonkProfile(int currentUserId, int targetUserId);

        Task<Result<MonkProfileDto>> UpdateMonkProfile(int currentUserId, int targetUserId, UpdateMonkProfileRequestDto request);

        Task<Result<UserSettingDto>> GetUserSettings(int currentUserId, int targetUserId);

        Task<Result<UserSettingDto>> UpdateUserSettings(int currentUserId, int targetUserId, UpdateUserSettingRequestDto request);
    }
}
