using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Common;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;

namespace SoundShare.Application.Services;

public sealed class RoomService(
    IRoomRepository rooms,
    IQueueRepository queues,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IRoomNotifier notifier,
    IValidator<CreateRoomRequest> createValidator,
    IValidator<JoinByCodeRequest> joinValidator,
    IValidator<BanRequest> banValidator)
{
    public async Task<IReadOnlyList<RoomSummaryDto>> ListPublicAsync(CancellationToken cancellationToken) =>
        (await rooms.ListPublicAsync(cancellationToken))
            .Select(r => new RoomSummaryDto(r.Room.Id, r.Room.Name, r.MembersCount))
            .ToList();

    public async Task<RoomCreatedDto> CreateAsync(long userId, CreateRoomRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        return await unitOfWork.InTransactionAsync(
            async ct =>
            {
                await LeaveCurrentRoomAsync(userId, ct);
                var now = time.UtcNow();
                var room = new Room(request.Name.Trim(), userId, request.IsPrivate, InviteCode.Generate(), now);
                rooms.Add(room);
                await unitOfWork.SaveChangesAsync(ct);

                queues.AddPlaylist(Playlist.ForRoom(room.Id, room.Name, now));
                rooms.AddMember(new RoomMember(room.Id, userId, RoomRole.Owner, now));
                await unitOfWork.SaveChangesAsync(ct);
                return new RoomCreatedDto(room.Id, room.Name, room.InviteCode);
            },
            cancellationToken);
    }

    public async Task<RoomStateDto> GetStateAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        var member = await rooms.GetMemberAsync(roomId, userId, cancellationToken);
        if (room is null || !room.IsActive || (room.IsPrivate && member is not { IsActive: true }))
        {
            throw new NotFoundException("Room not found.");
        }

        var count = await rooms.CountActiveMembersAsync(roomId, cancellationToken);
        var role = member is { IsActive: true } ? member.Role.ToString() : "None";
        var code = member is { IsActive: true } ? room.InviteCode : null;
        return new RoomStateDto(room.Id, room.Name, room.IsPrivate, code, count, role);
    }

    public async Task<RoomStateDto> JoinPublicAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        if (room is null || !room.IsActive || room.IsPrivate)
        {
            throw new NotFoundException("Room not found.");
        }

        await JoinAsync(room, userId, cancellationToken);
        return await GetStateAsync(roomId, userId, cancellationToken);
    }

    public async Task<RoomStateDto> JoinByCodeAsync(long userId, JoinByCodeRequest request, CancellationToken cancellationToken)
    {
        await joinValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!InviteCode.TryParse(request.InviteCode, out var code))
        {
            throw new NotFoundException("Room not found.");
        }

        var room = await rooms.GetByInviteCodeAsync(code.Value, cancellationToken);
        if (room is null || !room.IsActive)
        {
            throw new NotFoundException("Room not found.");
        }

        await JoinAsync(room, userId, cancellationToken);
        return await GetStateAsync(room.Id, userId, cancellationToken);
    }

    public async Task LeaveAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        var member = await rooms.GetMemberAsync(roomId, userId, cancellationToken);
        if (member is not { IsActive: true })
        {
            throw new NotFoundException("You are not a member of this room.");
        }

        await unitOfWork.InTransactionAsync(
            async ct =>
            {
                await LeaveAndTransferAsync(member, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
        await notifier.QueueChangedAsync(roomId, cancellationToken);
    }

    public async Task BanAsync(long roomId, long actorId, BanRequest request, CancellationToken cancellationToken)
    {
        await banValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await RequireActiveMemberAsync(roomId, actorId, cancellationToken);
        var target = await rooms.GetMemberAsync(roomId, request.UserId, cancellationToken);
        if (target is not { IsActive: true })
        {
            throw new NotFoundException("Member not found.");
        }

        if (!RoomMember.CanModerate(actor.Role, target.Role))
        {
            throw new ForbiddenException("You can only ban members with a lower role.");
        }

        var now = time.UtcNow();
        var until = request.DurationMinutes is { } minutes ? now.AddMinutes(minutes) : new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        target.Ban(now, until);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.MemberRemovedAsync(roomId, target.UserId, "banned", cancellationToken);
        await notifier.QueueChangedAsync(roomId, cancellationToken);
    }

    public async Task ChangeMemberRoleAsync(long roomId, long actorId, long targetUserId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = await RequireActiveMemberAsync(roomId, actorId, cancellationToken);
        if (actor.Role != RoomRole.Owner)
        {
            throw new ForbiddenException("Only the owner can change roles.");
        }

        var target = await rooms.GetMemberAsync(roomId, targetUserId, cancellationToken);
        if (target is not { IsActive: true })
        {
            throw new NotFoundException("Member not found.");
        }

        var role = Roles.ParseRoomRole(request.Role);
        if (role == RoomRole.Owner)
        {
            actor.ChangeRole(RoomRole.Moderator);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        target.ChangeRole(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseAsync(long roomId, long actorId, bool isAdmin, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        if (room is null || !room.IsActive)
        {
            throw new NotFoundException("Room not found.");
        }

        if (!isAdmin)
        {
            var actor = await RequireActiveMemberAsync(roomId, actorId, cancellationToken);
            if (actor.Role != RoomRole.Owner)
            {
                throw new ForbiddenException("Only the owner can close the room.");
            }
        }

        var now = time.UtcNow();
        foreach (var member in await rooms.GetActiveMembersAsync(roomId, cancellationToken))
        {
            member.Leave(now);
        }

        room.Close();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.RoomClosedAsync(roomId, cancellationToken);
    }

    public Task<int> GetActiveMemberCountAsync(long roomId, CancellationToken cancellationToken) =>
        rooms.CountActiveMembersAsync(roomId, cancellationToken);

    public async Task<RoomMember> RequireActiveMemberAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetByIdAsync(roomId, cancellationToken);
        var member = await rooms.GetMemberAsync(roomId, userId, cancellationToken);
        if (room is null || !room.IsActive || member is not { IsActive: true })
        {
            throw new NotFoundException("Room not found.");
        }

        return member;
    }

    internal async Task LeaveCurrentRoomAsync(long userId, CancellationToken cancellationToken)
    {
        var current = await rooms.GetActiveMembershipAsync(userId, cancellationToken);
        if (current is not null)
        {
            await LeaveAndTransferAsync(current, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task JoinAsync(Room room, long userId, CancellationToken cancellationToken)
    {
        var now = time.UtcNow();
        var existing = await rooms.GetMemberAsync(room.Id, userId, cancellationToken);
        if (existing is { IsActive: true })
        {
            return;
        }

        if (existing is not null && existing.IsBanned(now))
        {
            throw new ForbiddenException($"You are banned from this room until {existing.BannedUntil:yyyy-MM-dd HH:mm} UTC.");
        }

        await unitOfWork.InTransactionAsync(
            async ct =>
            {
                await rooms.LockAsync(room.Id, ct);
                if (await rooms.CountActiveMembersAsync(room.Id, ct) >= QueueRules.MaxRoomMembers)
                {
                    throw new ConflictException("The room is full.");
                }

                await LeaveCurrentRoomAsync(userId, ct);
                if (existing is null)
                {
                    rooms.AddMember(new RoomMember(room.Id, userId, RoomRole.Contributor, now));
                }
                else
                {
                    try
                    {
                        existing.Rejoin(now);
                    }
                    catch (DomainException ex)
                    {
                        throw new ForbiddenException(ex.Message);
                    }
                }

                room.MarkOccupied();
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);
        await notifier.QueueChangedAsync(room.Id, cancellationToken);
    }

    private async Task LeaveAndTransferAsync(RoomMember member, CancellationToken cancellationToken)
    {
        var now = time.UtcNow();
        var wasOwner = member.Role == RoomRole.Owner;
        member.Leave(now);
        if (wasOwner)
        {
            member.ChangeRole(RoomRole.Contributor);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var remaining = (await rooms.GetActiveMembersAsync(member.RoomId, cancellationToken))
            .Where(m => m.UserId != member.UserId)
            .ToList();

        if (wasOwner)
        {
            var successor = remaining
                .OrderByDescending(m => m.Role == RoomRole.Moderator)
                .ThenBy(m => m.JoinedAt)
                .FirstOrDefault();
            successor?.ChangeRole(RoomRole.Owner);
        }

        if (remaining.Count == 0)
        {
            var room = await rooms.GetByIdAsync(member.RoomId, cancellationToken);
            room?.MarkEmpty(now);
        }
    }
}
