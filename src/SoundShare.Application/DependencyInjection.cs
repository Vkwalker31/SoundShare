using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SoundShare.Application.Services;

namespace SoundShare.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);
        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<RoomService>();
        services.AddScoped<QueueService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<PlaylistService>();
        services.AddScoped<AuditService>();
        return services;
    }
}
