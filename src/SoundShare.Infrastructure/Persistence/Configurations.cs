using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SoundShare.Domain.Audit;
using SoundShare.Domain.Catalog;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;
using SoundShare.Domain.Users;

namespace SoundShare.Infrastructure.Persistence;

internal static class EnumConverters
{
    public static ValueConverter<TEnum, string> UpperSnake<TEnum>()
        where TEnum : struct, Enum =>
        new(
            v => SoundShareDbContext.ToUpperSnake(v.ToString()),
            v => Enum.Parse<TEnum>(v.Replace("_", string.Empty, StringComparison.Ordinal), true));
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("USERS");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedOnAdd();
        builder.Property(u => u.Role).HasColumnName("ROLE_ID").HasConversion<int>();
        builder.Property(u => u.Username).HasMaxLength(32);
        builder.Property(u => u.Email).HasMaxLength(254);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("REFRESH_TOKENS");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Property(t => t.TokenHash).HasColumnType("RAW(32)");
        builder.Ignore(t => t.IsRevoked);
    }
}

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("ROOMS");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.InviteCode).HasColumnType("CHAR(6)");
    }
}

internal sealed class RoomMemberConfiguration : IEntityTypeConfiguration<RoomMember>
{
    public void Configure(EntityTypeBuilder<RoomMember> builder)
    {
        builder.ToTable("ROOM_MEMBERS");
        builder.HasKey(m => new { m.RoomId, m.UserId });
        builder.Property(m => m.Role).HasColumnName("MEMBER_ROLE").HasConversion(EnumConverters.UpperSnake<RoomRole>());
        builder.Ignore(m => m.IsActive);
    }
}

internal sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.ToTable("ARTISTS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();
    }
}

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ToTable("TRACKS");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Ignore(t => t.ThumbnailUrl);
        builder.HasMany(t => t.Artists).WithOne(a => a.Track).HasForeignKey(a => a.TrackId);
        builder.Navigation(t => t.Artists).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TrackArtistConfiguration : IEntityTypeConfiguration<TrackArtist>
{
    public void Configure(EntityTypeBuilder<TrackArtist> builder)
    {
        builder.ToTable("TRACK_ARTISTS");
        builder.HasKey(a => new { a.TrackId, a.ArtistId });
        builder.HasOne(a => a.Artist).WithMany().HasForeignKey(a => a.ArtistId);
    }
}

internal sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.ToTable("PLAYLISTS");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.QueueVersion).IsConcurrencyToken();
    }
}

internal sealed class QueueItemConfiguration : IEntityTypeConfiguration<QueueItem>
{
    public void Configure(EntityTypeBuilder<QueueItem> builder)
    {
        builder.ToTable("PLAYLIST_TRACKS");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Property(i => i.Status).HasConversion(EnumConverters.UpperSnake<QueueItemStatus>());
        builder.HasOne(i => i.Track).WithMany().HasForeignKey(i => i.TrackId);
        builder.Ignore(i => i.IsPinned);
        builder.Ignore(i => i.IsActive);
    }
}

internal sealed class TrackVoteConfiguration : IEntityTypeConfiguration<TrackVote>
{
    public void Configure(EntityTypeBuilder<TrackVote> builder)
    {
        builder.ToTable("TRACK_VOTES");
        builder.HasKey(v => new { v.PlaylistTrackId, v.UserId });
    }
}

internal sealed class SkipVoteConfiguration : IEntityTypeConfiguration<SkipVote>
{
    public void Configure(EntityTypeBuilder<SkipVote> builder)
    {
        builder.ToTable("SKIP_VOTES");
        builder.HasKey(v => new { v.PlaylistTrackId, v.UserId });
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AUDIT_LOGS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Property(a => a.Details).HasColumnType("CLOB");
    }
}
