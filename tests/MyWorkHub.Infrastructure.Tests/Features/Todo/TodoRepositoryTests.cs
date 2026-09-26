using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Infrastructure.DependencyInjection;
using MyWorkHub.Infrastructure.Features.Todo;
using MyWorkHub.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Infrastructure.Tests.Features.Todo;

public sealed class TodoRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    private readonly InMemorySqliteContextFactory _factory = new();
    private readonly MutableTimeProvider _clock = new(_now);
    private readonly TodoRepository _repository;

    public TodoRepositoryTests()
    {
        _repository = new TodoRepository(_factory, _clock);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Should_have_no_pending_model_changes_against_the_migrations()
    {
        // The Todo slice re-maps the pre-existing Todos table; the model must still match the
        // migration snapshot exactly, otherwise the startup Migrate() call would throw.
        await using var db = _factory.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Should_return_the_added_item_with_an_id_and_the_current_time()
    {
        var ct = TestContext.Current.CancellationToken;

        var added = await _repository.AddAsync("  Buy milk  ", ct);

        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal("Buy milk", added.Title);
        Assert.False(added.IsCompleted);
        Assert.Equal(_now.UtcDateTime, added.CreatedAt);
        Assert.Equal([added], await _repository.GetAllAsync(ct));
    }

    [Fact]
    public async Task Should_reject_a_blank_title_when_adding()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.AddAsync("   ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_return_items_newest_first()
    {
        var ct = TestContext.Current.CancellationToken;
        await _repository.AddAsync("Older", ct);
        _clock.Advance(TimeSpan.FromHours(1));
        await _repository.AddAsync("Newer", ct);

        var all = await _repository.GetAllAsync(ct);

        Assert.Equal(["Newer", "Older"], all.Select(t => t.Title));
    }

    [Fact]
    public async Task Should_persist_completion_and_its_timestamp_when_completing()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await _repository.AddAsync("Ship it", ct);
        _clock.Advance(TimeSpan.FromMinutes(5));

        await _repository.SetCompletedAsync(item.Id, isCompleted: true, ct);

        Assert.True((await _repository.GetAllAsync(ct)).Single().IsCompleted);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(_now.AddMinutes(5).UtcDateTime, db.Set<TodoItemEntity>().Single().CompletedAt);
    }

    [Fact]
    public async Task Should_clear_the_completion_timestamp_when_reopening()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await _repository.AddAsync("Ship it", ct);
        await _repository.SetCompletedAsync(item.Id, isCompleted: true, ct);

        await _repository.SetCompletedAsync(item.Id, isCompleted: false, ct);

        Assert.False((await _repository.GetAllAsync(ct)).Single().IsCompleted);
        await using var db = _factory.CreateDbContext();
        Assert.Null(db.Set<TodoItemEntity>().Single().CompletedAt);
    }

    [Fact]
    public async Task Should_remove_the_item_when_deleting()
    {
        var ct = TestContext.Current.CancellationToken;
        var keep = await _repository.AddAsync("Keep", ct);
        var obsolete = await _repository.AddAsync("Obsolete", ct);

        await _repository.DeleteAsync(obsolete.Id, ct);

        Assert.Equal([keep], await _repository.GetAllAsync(ct));
    }

    [Fact]
    public async Task Should_ignore_unknown_ids_when_completing_or_deleting()
    {
        var ct = TestContext.Current.CancellationToken;
        var item = await _repository.AddAsync("Keep", ct);

        await _repository.SetCompletedAsync(Guid.NewGuid(), isCompleted: true, ct);
        await _repository.DeleteAsync(Guid.NewGuid(), ct);

        Assert.Equal([item], await _repository.GetAllAsync(ct));
    }

    [Fact]
    public void Should_register_the_repository_in_the_default_infrastructure_composition()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        Assert.IsType<TodoRepository>(provider.GetRequiredService<ITodoRepository>());
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _utcNow = start;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow += by;
    }
}
