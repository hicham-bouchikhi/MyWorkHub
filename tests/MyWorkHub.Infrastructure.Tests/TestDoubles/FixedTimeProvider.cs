namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>A clock frozen at a given instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
