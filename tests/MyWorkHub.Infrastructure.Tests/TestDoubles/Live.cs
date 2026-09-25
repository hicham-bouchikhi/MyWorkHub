using MyWorkHub.Infrastructure.Configuration;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>Fixed-value <see cref="LiveOptions{T}"/> for tests that do not exercise reloading.</summary>
internal static class Live
{
    public static LiveOptions<T> Of<T>(T value)
        where T : class
        => new(() => value);
}
