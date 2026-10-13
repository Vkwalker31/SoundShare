using SoundShare.Domain.Rooms;

namespace SoundShare.Application.Abstractions;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(long roomId, CancellationToken cancellationToken);

    Task<Room?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<(Room Room, int MembersCount)>> ListPublicAsync(CancellationToken cancellationToken);

    Task LockAsync(long roomId, CancellationToken cancellationToken);

    Task<RoomMember?> GetMemberAsync(long roomId, long userId, CancellationToken cancellationToken);

    Task<RoomMember?> GetActiveMembershipAsync(long userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoomMember>> GetActiveMembersAsync(long roomId, CancellationToken cancellationToken);

    Task<int> CountActiveMembersAsync(long roomId, CancellationToken cancellationToken);

    void Add(Room room);

    void AddMember(RoomMember member);
}
