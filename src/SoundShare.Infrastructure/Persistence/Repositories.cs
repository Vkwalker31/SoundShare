using Microsoft.EntityFrameworkCore;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Domain.Audit;
using SoundShare.Domain.Catalog;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;
using SoundShare.Domain.Users;

namespace SoundShare.Infrastructure.Persistence;

internal sealed class UserRepository(SoundShareDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        return db.Users.FirstOrDefaultAsync(u => u.Username.ToUpper() == normalized, cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.Username.ToUpper() == normalized, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.Email.ToUpper() == normalized, cancellationToken);
    }

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        db.Users.CountAsync(u => u.Role == SystemRole.Admin && u.IsActive, cancellationToken);

    public async Task<PagedResult<User>> ListAsync(SystemRole? role, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (role is { } r)
        {
            query = query.Where(u => u.Role == r);
        }

        if (isActive is { } active)
        {
            query = query.Where(u => u.IsActive == active);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<User>(items, total, page, pageSize);
    }

    public void Add(User user) => db.Users.Add(user);

    public Task<RefreshToken?> GetRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(long userId, DateTime now, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now).ToListAsync(cancellationToken);

    public void AddRefreshToken(RefreshToken token) => db.RefreshTokens.Add(token);
}

internal sealed class RoomRepository(SoundShareDbContext db) : IRoomRepository
{
    public Task<Room?> GetByIdAsync(long roomId, CancellationToken cancellationToken) =>
        db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

    public Task<Room?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        db.Rooms.FirstOrDefaultAsync(r => r.InviteCode == inviteCode, cancellationToken);

    public async Task<IReadOnlyList<(Room Room, int MembersCount)>> ListPublicAsync(CancellationToken cancellationToken)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.IsActive && !r.IsPrivate)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
        var ids = rooms.Select(r => r.Id).ToList();
        var counts = await db.RoomMembers
            .Where(m => ids.Contains(m.RoomId) && m.LeftAt == null)
            .GroupBy(m => m.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoomId, x => x.Count, cancellationToken);
        return rooms.Select(r => (r, counts.GetValueOrDefault(r.Id))).ToList();
    }

    public Task LockAsync(long roomId, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT ID FROM SOUNDSHARE.ROOMS WHERE ID = {roomId} FOR UPDATE", cancellationToken);

    public Task<RoomMember?> GetMemberAsync(long roomId, long userId, CancellationToken cancellationToken) =>
        db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, cancellationToken);

    public Task<RoomMember?> GetActiveMembershipAsync(long userId, CancellationToken cancellationToken) =>
        db.RoomMembers.FirstOrDefaultAsync(m => m.UserId == userId && m.LeftAt == null, cancellationToken);

    public async Task<IReadOnlyList<RoomMember>> GetActiveMembersAsync(long roomId, CancellationToken cancellationToken) =>
        await db.RoomMembers.Where(m => m.RoomId == roomId && m.LeftAt == null).ToListAsync(cancellationToken);

    public Task<int> CountActiveMembersAsync(long roomId, CancellationToken cancellationToken) =>
        db.RoomMembers.CountAsync(m => m.RoomId == roomId && m.LeftAt == null, cancellationToken);

    public void Add(Room room) => db.Rooms.Add(room);

    public void AddMember(RoomMember member) => db.RoomMembers.Add(member);
}

internal sealed class QueueRepository(SoundShareDbContext db) : IQueueRepository
{
    public Task<Playlist?> GetRoomQueueAsync(long roomId, CancellationToken cancellationToken) =>
        db.Playlists.FirstOrDefaultAsync(p => p.RoomId == roomId, cancellationToken);

    public Task<Playlist?> GetPlaylistAsync(long playlistId, CancellationToken cancellationToken) =>
        db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

    public void AddPlaylist(Playlist playlist) => db.Playlists.Add(playlist);

    public Task<QueueItem?> GetItemAsync(long itemId, CancellationToken cancellationToken) =>
        db.QueueItems.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

    public Task LockItemAsync(long itemId, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT ID FROM SOUNDSHARE.PLAYLIST_TRACKS WHERE ID = {itemId} FOR UPDATE", cancellationToken);

    public Task<QueueItem?> GetPlayingAsync(long playlistId, CancellationToken cancellationToken) =>
        db.QueueItems.Include(i => i.Track)
            .FirstOrDefaultAsync(i => i.PlaylistId == playlistId && i.Status == QueueItemStatus.Playing, cancellationToken);

    public async Task<IReadOnlyList<QueueItemView>> GetActiveItemsAsync(long playlistId, CancellationToken cancellationToken)
    {
        var items = await db.QueueItems
            .Include(i => i.Track).ThenInclude(t => t.Artists).ThenInclude(a => a.Artist)
            .Where(i => i.PlaylistId == playlistId && (i.Status == QueueItemStatus.Queued || i.Status == QueueItemStatus.Playing))
            .ToListAsync(cancellationToken);
        var ids = items.Select(i => i.Id).ToList();
        var userIds = items.Select(i => i.AddedById).Distinct().ToList();

        var scores = await db.TrackVotes
            .Where(v => ids.Contains(v.PlaylistTrackId))
            .GroupBy(v => v.PlaylistTrackId)
            .Select(g => new { Id = g.Key, Score = g.Sum(v => v.VoteValue) })
            .ToDictionaryAsync(x => x.Id, x => x.Score, cancellationToken);
        var names = await db.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);

        return items
            .Select(i => new QueueItemView(i, scores.GetValueOrDefault(i.Id), names.GetValueOrDefault(i.AddedById, string.Empty)))
            .ToList();
    }

    public async Task<IReadOnlyList<QueueItem>> GetPlaylistItemsAsync(long playlistId, CancellationToken cancellationToken) =>
        await db.QueueItems
            .Include(i => i.Track).ThenInclude(t => t.Artists).ThenInclude(a => a.Artist)
            .Where(i => i.PlaylistId == playlistId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<long>> GetRoomIdsWithFinishedTrackAsync(DateTime now, CancellationToken cancellationToken)
    {
        var playing = await db.QueueItems.AsNoTracking()
            .Where(i => i.Status == QueueItemStatus.Playing)
            .Join(db.Playlists, i => i.PlaylistId, p => p.Id, (i, p) => new { i.StartedAt, i.Track.DurationSec, p.RoomId })
            .ToListAsync(cancellationToken);
        return playing
            .Where(x => x.RoomId is not null && x.StartedAt is not null && x.StartedAt.Value.AddSeconds(x.DurationSec) <= now)
            .Select(x => x.RoomId!.Value)
            .Distinct()
            .ToList();
    }

    public Task<int> CountActiveItemsByUserAsync(long playlistId, long userId, CancellationToken cancellationToken) =>
        db.QueueItems.CountAsync(
            i => i.PlaylistId == playlistId && i.AddedById == userId
                && (i.Status == QueueItemStatus.Queued || i.Status == QueueItemStatus.Playing),
            cancellationToken);

    public void AddItem(QueueItem item) => db.QueueItems.Add(item);

    public Task<TrackVote?> GetVoteAsync(long itemId, long userId, CancellationToken cancellationToken) =>
        db.TrackVotes.FirstOrDefaultAsync(v => v.PlaylistTrackId == itemId && v.UserId == userId, cancellationToken);

    public void AddVote(TrackVote vote) => db.TrackVotes.Add(vote);

    public void RemoveVote(TrackVote vote) => db.TrackVotes.Remove(vote);

    public async Task<int> GetScoreAsync(long itemId, CancellationToken cancellationToken) =>
        await db.TrackVotes.Where(v => v.PlaylistTrackId == itemId).SumAsync(v => (int?)v.VoteValue, cancellationToken) ?? 0;

    public Task<bool> HasSkipVoteAsync(long itemId, long userId, CancellationToken cancellationToken) =>
        db.SkipVotes.AnyAsync(v => v.PlaylistTrackId == itemId && v.UserId == userId, cancellationToken);

    public void AddSkipVote(SkipVote vote) => db.SkipVotes.Add(vote);

    public Task<int> CountSkipVotesAsync(long itemId, CancellationToken cancellationToken) =>
        db.SkipVotes.CountAsync(v => v.PlaylistTrackId == itemId, cancellationToken);
}

internal sealed class CatalogRepository(SoundShareDbContext db) : ICatalogRepository
{
    public Task<Track?> GetTrackAsync(long trackId, CancellationToken cancellationToken) =>
        db.Tracks.FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

    public Task<Track?> GetByExternalIdAsync(string externalSourceId, CancellationToken cancellationToken) =>
        db.Tracks.Include(t => t.Artists).ThenInclude(a => a.Artist)
            .FirstOrDefaultAsync(t => t.ExternalSource == Track.YouTubeSource && t.ExternalSourceId == externalSourceId, cancellationToken);

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, int take, CancellationToken cancellationToken)
    {
        var normalized = query.ToUpperInvariant();
        return await db.Tracks
            .Include(t => t.Artists).ThenInclude(a => a.Artist)
            .Where(t => t.Title.ToUpper().Contains(normalized) || t.Artists.Any(a => a.Artist.Name.ToUpper().Contains(normalized)))
            .OrderBy(t => t.Title)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<Artist?> GetArtistByNameAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = name.ToUpperInvariant();
        return db.Artists.FirstOrDefaultAsync(a => a.Name.ToUpper() == normalized, cancellationToken);
    }

    public void AddTrack(Track track) => db.Tracks.Add(track);

    public void AddArtist(Artist artist) => db.Artists.Add(artist);
}

internal sealed class AuditRepository(SoundShareDbContext db) : IAuditRepository
{
    public async Task<IReadOnlyList<AuditLog>> ListAsync(long? userId, string? action, DateTime? since, DateTime? until, long? beforeId, int take, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (userId is { } u)
        {
            query = query.Where(a => a.UserId == u);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (since is { } s)
        {
            query = query.Where(a => a.CreatedAt >= s);
        }

        if (until is { } t)
        {
            query = query.Where(a => a.CreatedAt < t);
        }

        if (beforeId is { } b)
        {
            query = query.Where(a => a.Id < b);
        }

        return await query.OrderByDescending(a => a.Id).Take(take).ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<AuditLog> entries, CancellationToken cancellationToken)
    {
        db.AuditLogs.AddRange(entries);
        await db.SaveChangesAsync(cancellationToken);
    }
}
