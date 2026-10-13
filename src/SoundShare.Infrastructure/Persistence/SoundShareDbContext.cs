using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SoundShare.Application.Abstractions;
using SoundShare.Domain.Audit;
using SoundShare.Domain.Catalog;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;
using SoundShare.Domain.Users;

namespace SoundShare.Infrastructure.Persistence;

public sealed class SoundShareDbContext(DbContextOptions<SoundShareDbContext> options) : DbContext(options), IUnitOfWork
{
    public const string Schema = "SOUNDSHARE";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

    public DbSet<Artist> Artists => Set<Artist>();

    public DbSet<Track> Tracks => Set<Track>();

    public DbSet<TrackArtist> TrackArtists => Set<TrackArtist>();

    public DbSet<Playlist> Playlists => Set<Playlist>();

    public DbSet<QueueItem> QueueItems => Set<QueueItem>();

    public DbSet<TrackVote> TrackVotes => Set<TrackVote>();

    public DbSet<SkipVote> SkipVotes => Set<SkipVote>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Database.CurrentTransaction is not null)
        {
            return await action(cancellationToken);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<string>().AreUnicode(false);
        configurationBuilder.Properties<bool>().HaveColumnType("NUMBER(1)");
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>().HaveColumnType("TIMESTAMP(6)");
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>().HaveColumnType("TIMESTAMP(6)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SoundShareDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnName() == property.Name)
                {
                    property.SetColumnName(ToUpperSnake(property.Name));
                }
            }
        }
    }

    internal static string ToUpperSnake(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(name[i]));
        }

        return builder.ToString();
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
