using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oracle.EntityFrameworkCore.Infrastructure;
using SoundShare.Application.Abstractions;
using SoundShare.Infrastructure.Auditing;
using SoundShare.Infrastructure.External;
using SoundShare.Infrastructure.Persistence;
using SoundShare.Infrastructure.Playback;
using SoundShare.Infrastructure.Security;

namespace SoundShare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool enableBackgroundServices = true)
    {
        var connectionString = configuration.GetConnectionString("SoundShare")
            ?? throw new InvalidOperationException("Connection string 'SoundShare' is not configured.");

        services.AddDbContext<SoundShareDbContext>(options =>
            options.UseOracle(connectionString, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SoundShareDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IQueueRepository, QueueRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenService, TokenService>();

        services.Configure<YouTubeOptions>(configuration.GetSection(YouTubeOptions.SectionName));
        services.AddHttpClient<ITrackSource, YouTubeTrackSource>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IOptions<YouTubeOptions>>().Value.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddSingleton<AuditBuffer>();
        if (enableBackgroundServices)
        {
            services.AddHostedService<AuditWriterService>();
            services.AddHostedService<PlaybackWorker>();
        }

        return services;
    }
}
