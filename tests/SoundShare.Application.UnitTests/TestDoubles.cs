// Test doubles shared by Application unit tests: fixed clock, in-memory unit of work, entity id setter.
using System.Reflection;
using SoundShare.Application.Abstractions;

namespace SoundShare.Application.UnitTests;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public int TransactionCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        TransactionCount++;
        return await action(cancellationToken);
    }
}

internal static class TestClock
{
    public static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    public static TimeProvider Fixed()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(new DateTimeOffset(Now));
        return time;
    }
}

internal static class EntityExtensions
{
    public static T With<T>(this T entity, string property, object? value)
        where T : class
    {
        typeof(T).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(entity, value);
        return entity;
    }

    public static T WithId<T>(this T entity, long id)
        where T : class => entity.With("Id", id);
}
