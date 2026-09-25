using Microsoft.EntityFrameworkCore;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Persistence;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

public sealed class RemoteWorkPlanRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    private readonly InMemorySqliteContextFactory _factory = new();
    private readonly RemoteWorkPlanRepository _repository;

    public RemoteWorkPlanRepositoryTests()
    {
        _repository = new RemoteWorkPlanRepository(_factory, new FixedTimeProvider(_now));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Should_return_an_empty_plan_for_a_month_never_saved()
    {
        var plan = await _repository.GetAsync(2026, 9, Ct);

        Assert.Equal((2026, 9), (plan.Year, plan.Month));
        Assert.Empty(plan.Days);
    }

    [Fact]
    public async Task Should_read_back_a_saved_plan()
    {
        await _repository.SaveAsync(RemoteWorkPlan.Create(2026, 9, [9, 2]), Ct);

        Assert.Equal([2, 9], (await _repository.GetAsync(2026, 9, Ct)).Days.Select(d => d.Day));
    }

    [Fact]
    public async Task Should_replace_the_month_plan_in_place_when_saved_again()
    {
        await _repository.SaveAsync(RemoteWorkPlan.Create(2026, 9, [2]), Ct);
        await _repository.SaveAsync(RemoteWorkPlan.Create(2026, 9, [16, 23]), Ct);

        await using var db = _factory.CreateDbContext();
        var row = Assert.Single(await db.Set<RemoteWorkScheduleEntity>().ToListAsync(Ct));
        Assert.Equal("[16,23]", row.DaysJson);
        Assert.Equal(_now.UtcDateTime, row.CreatedAt);
    }

    [Fact]
    public async Task Should_keep_months_apart()
    {
        await _repository.SaveAsync(RemoteWorkPlan.Create(2026, 9, [2]), Ct);
        await _repository.SaveAsync(RemoteWorkPlan.Create(2026, 10, [5]), Ct);

        Assert.Equal([2], (await _repository.GetAsync(2026, 9, Ct)).Days.Select(d => d.Day));
        Assert.Equal([5], (await _repository.GetAsync(2026, 10, Ct)).Days.Select(d => d.Day));
    }

    [Theory]
    [InlineData("[2,31,40]", new[] { 2 })]
    [InlineData("garbage", new int[0])]
    public async Task Should_drop_unreadable_or_out_of_month_days_from_a_hand_edited_row(string daysJson, int[] expected)
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Add(new RemoteWorkScheduleEntity { Id = Guid.NewGuid(), Year = 2026, Month = 9, DaysJson = daysJson, CreatedAt = _now.UtcDateTime });
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(expected, (await _repository.GetAsync(2026, 9, Ct)).Days.Select(d => d.Day));
    }
}
