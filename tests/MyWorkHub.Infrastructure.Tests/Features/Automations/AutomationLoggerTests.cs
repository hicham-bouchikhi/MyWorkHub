using Microsoft.EntityFrameworkCore;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Persistence;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

public sealed class AutomationLoggerTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    private readonly InMemorySqliteContextFactory _factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    private AutomationLogger Logger(DateTimeOffset? at = null) => new(_factory, new FixedTimeProvider(at ?? _now));

    private string RawColumn(Guid id, string column)
    {
        using var db = _factory.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Column names are test constants; the id is a parameter.
        command.CommandText = $"SELECT {column} FROM AutomationRuns WHERE Id = $id";
#pragma warning restore CA2100
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        parameter.Value = id.ToString().ToUpperInvariant();
        command.Parameters.Add(parameter);
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    [Fact]
    public async Task Should_have_no_pending_model_changes_against_the_migrations()
    {
        // The slice re-maps the pre-existing AutomationRuns and RemoteWorkSchedules tables (InitialCreate
        // migration); the model must still match the snapshot exactly, or the startup Migrate() would throw.
        await using var db = _factory.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Should_record_a_started_run_as_pending()
    {
        var id = await Logger().BeginRunAsync("RemoteWorkSync", Ct);

        var run = Assert.Single(await Logger().GetRecentRunsAsync(10, Ct));
        Assert.Equal(id, run.Id);
        Assert.Equal("RemoteWorkSync", run.AutomationId);
        Assert.Equal(AutomationRunStatus.PENDING, run.Status);
        Assert.Equal(_now.UtcDateTime, run.StartedAt);
        Assert.Null(run.CompletedAt);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public async Task Should_round_trip_the_outcome_and_every_step_of_a_completed_run()
    {
        var id = await Logger().BeginRunAsync("RemoteWorkSync", Ct);
        var outcome = AutomationRunOutcome.FromSteps([
            new AutomationStepResult("Outlook calendar", AutomationStepStatus.SUCCESS),
            new AutomationStepResult("PeopleNet", AutomationStepStatus.FAILED, "login rejected"),
            new AutomationStepResult("mwork", AutomationStepStatus.SKIPPED, "not configured"),
        ]);

        await Logger(_now.AddMinutes(2)).CompleteRunAsync(id, outcome, Ct);

        var run = Assert.Single(await Logger().GetRecentRunsAsync(10, Ct));
        Assert.Equal(AutomationRunStatus.PARTIAL, run.Status);
        Assert.Equal("PeopleNet: login rejected", run.ErrorMessage);
        Assert.Equal(_now.AddMinutes(2).UtcDateTime, run.CompletedAt);
        Assert.Equal(outcome.Steps, run.Steps);
    }

    [Fact]
    public async Task Should_store_the_status_and_step_statuses_by_name()
    {
        var id = await Logger().BeginRunAsync("TransportReimbursement", Ct);
        await Logger().CompleteRunAsync(
            id,
            AutomationRunOutcome.FromSteps([new AutomationStepResult("Download attestation", AutomationStepStatus.FAILED, "timeout")]),
            Ct);

        Assert.Equal("FAILED", RawColumn(id, "Status"));
        Assert.Contains("\"Status\":\"FAILED\"", RawColumn(id, "Details"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_list_the_most_recent_runs_first_up_to_the_requested_count()
    {
        await Logger(_now.AddHours(-2)).BeginRunAsync("A", Ct);
        await Logger(_now).BeginRunAsync("C", Ct);
        await Logger(_now.AddHours(-1)).BeginRunAsync("B", Ct);

        var runs = await Logger().GetRecentRunsAsync(2, Ct);

        Assert.Equal(["C", "B"], runs.Select(r => r.AutomationId));
    }

    [Fact]
    public async Task Should_ignore_completing_an_unknown_run()
    {
        await Logger().CompleteRunAsync(Guid.NewGuid(), AutomationRunOutcome.FromSteps([]), Ct);

        Assert.Empty(await Logger().GetRecentRunsAsync(10, Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    public async Task Should_read_a_run_with_missing_or_unreadable_details_as_having_no_steps(string? details)
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Add(new AutomationRunEntity
            {
                Id = Guid.NewGuid(),
                JobName = "Legacy",
                StartedAt = _now.UtcDateTime,
                Status = AutomationRunStatus.SUCCESS,
                Details = details,
            });
            await db.SaveChangesAsync(Ct);
        }

        Assert.Empty(Assert.Single(await Logger().GetRecentRunsAsync(10, Ct)).Steps);
    }
}
