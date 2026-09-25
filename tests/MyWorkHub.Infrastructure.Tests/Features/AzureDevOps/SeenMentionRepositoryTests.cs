using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Infrastructure.DependencyInjection;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.AzureDevOps;

public sealed class SeenMentionRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    private readonly InMemorySqliteContextFactory _factory = new();
    private readonly SeenMentionRepository _repository;

    public SeenMentionRepositoryTests()
    {
        _repository = new SeenMentionRepository(_factory, new FixedTimeProvider(_now));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Should_have_no_pending_model_changes_against_the_migrations()
    {
        // The slice re-maps the pre-existing SeenMentions table (AddSeenMentions migration); the model
        // must still match the migration snapshot exactly, otherwise the startup Migrate() would throw.
        await using var db = _factory.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Should_have_seen_nothing_on_a_fresh_database()
    {
        Assert.Empty(await _repository.GetSeenCommentIdsAsync(Ct));
    }

    [Fact]
    public async Task Should_remember_every_mention_marked_seen()
    {
        await _repository.MarkSeenAsync(101, Ct);
        await _repository.MarkSeenAsync(202, Ct);

        Assert.Equal([101, 202], (await _repository.GetSeenCommentIdsAsync(Ct)).Order());
    }

    [Fact]
    public async Task Should_keep_the_first_time_seen_when_marking_again()
    {
        await _repository.MarkSeenAsync(101, Ct);
        var later = new SeenMentionRepository(_factory, new FixedTimeProvider(_now.AddDays(1)));

        await later.MarkSeenAsync(101, Ct);

        await using var db = _factory.CreateDbContext();
        var row = Assert.Single(db.Set<SeenWorkItemMentionEntity>());
        Assert.Equal(_now.UtcDateTime, row.SeenAt);
    }

    [Fact]
    public async Task Should_store_the_azure_devops_comment_id_as_the_key_verbatim()
    {
        await _repository.MarkSeenAsync(987654, Ct);

        await using var db = _factory.CreateDbContext();
        Assert.Equal(987654, Assert.Single(db.Set<SeenWorkItemMentionEntity>()).CommentId);
    }

    [Fact]
    public void Should_register_the_repository_in_the_default_infrastructure_composition()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        Assert.IsType<SeenMentionRepository>(provider.GetRequiredService<ISeenMentionRepository>());
    }
}
