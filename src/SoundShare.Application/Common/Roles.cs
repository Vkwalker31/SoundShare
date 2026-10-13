using SoundShare.Domain.Rooms;
using SoundShare.Domain.Users;

namespace SoundShare.Application.Common;

public static class Roles
{
    public static SystemRole ParseSystemRole(string role) =>
        Enum.TryParse<SystemRole>(role, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ConflictException($"Unknown role '{role}'.");

    public static RoomRole ParseRoomRole(string role) =>
        Enum.TryParse<RoomRole>(role, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ConflictException($"Unknown room role '{role}'.");
}
