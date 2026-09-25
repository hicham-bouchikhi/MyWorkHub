using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyWorkHub.Automation.Jobs;
using MyWorkHub.Automation.Scheduling;
using MyWorkHub.Core.Features.Automations;
using Quartz;

namespace MyWorkHub.Automation.Tests.Scheduling;

/// <summary>
/// Composes the real Quartz scheduler (in-memory store, never started) through <c>AddAutomations</c>. Each
/// test uses its own scheduler name: Quartz keeps schedulers in a process-wide repository by name.
/// </summary>
public sealed class AutomationSchedulingTests : IAsyncDisposable
{
    private const string TRANSPORT_CRON = "0 0 9 1 * ?";

    private readonly List<ServiceProvider> _providers = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.Shutdown(waitForJobsToComplete: false);
            await provider.DisposeAsync();
        }
    }

    private ServiceProvider Compose(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();

        // Quartz 3 binds a process-wide static log provider to the first container's ILoggerFactory, so
        // every test container shares one never-disposed factory (the app has a single container anyway).
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddAutomations(configuration);
        services.AddQuartz(q => q.SchedulerName = "test-" + Guid.NewGuid().ToString("N"));

        // The jobs' Core dependencies, as Infrastructure would provide them.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAutomationLogger, FakeAutomationLogger>();
        services.AddSingleton<IRemoteWorkPlanRepository, FakePlanRepository>();
        services.AddSingleton<IRemoteWorkSyncAutomation, FakeRemoteWorkSync>();
        services.AddSingleton<ITransportReimbursementAutomation, FakeTransportAutomation>();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    private static Dictionary<string, string?> TransportEnabled(string cron = TRANSPORT_CRON) => new()
    {
        ["Automation:TransportReimbursement:Enabled"] = "true",
        ["Automation:TransportReimbursement:CronExpression"] = cron,
    };

    [Fact]
    public async Task Should_list_every_discovered_automation_in_display_order()
    {
        var scheduler = Compose([]).GetRequiredService<IAutomationScheduler>();

        var schedules = await scheduler.GetSchedulesAsync(Ct);

        Assert.Equal(
            [AutomationCatalog.RemoteWorkSync.Id, AutomationCatalog.TransportReimbursement.Id],
            schedules.Select(s => s.Automation.Id));
    }

    [Fact]
    public async Task Should_schedule_an_enabled_automation_on_its_cron_expression()
    {
        var scheduler = Compose(TransportEnabled()).GetRequiredService<IAutomationScheduler>();

        var transport = (await scheduler.GetSchedulesAsync(Ct)).Single(s => s.Automation == AutomationCatalog.TransportReimbursement);

        Assert.Equal(TRANSPORT_CRON, transport.CronExpression);
        Assert.NotNull(transport.NextRunAt);
        Assert.True(transport.NextRunAt > DateTimeOffset.UtcNow);
        Assert.Null(transport.Note);
    }

    [Fact]
    public async Task Should_not_schedule_a_disabled_automation_and_say_how_to_enable_it()
    {
        var scheduler = Compose([]).GetRequiredService<IAutomationScheduler>();

        var sync = (await scheduler.GetSchedulesAsync(Ct)).Single(s => s.Automation == AutomationCatalog.RemoteWorkSync);

        Assert.Null(sync.CronExpression);
        Assert.Null(sync.NextRunAt);
        Assert.Contains("Automation:RemoteWorkSync:Enabled", sync.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_not_schedule_an_invalid_cron_expression_and_name_it()
    {
        var scheduler = Compose(TransportEnabled("every monday")).GetRequiredService<IAutomationScheduler>();

        var transport = (await scheduler.GetSchedulesAsync(Ct)).Single(s => s.Automation == AutomationCatalog.TransportReimbursement);

        Assert.Null(transport.NextRunAt);
        Assert.Contains("\"every monday\"", transport.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_queue_an_immediate_run_even_for_an_unscheduled_automation()
    {
        var provider = Compose([]);
        var quartz = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(Ct);
        var jobKey = new JobKey(AutomationCatalog.RemoteWorkSync.Id);
        Assert.Empty(await quartz.GetTriggersOfJob(jobKey, Ct));

        await provider.GetRequiredService<IAutomationScheduler>().RunNowAsync(AutomationCatalog.RemoteWorkSync.Id, Ct);

        Assert.Single(await quartz.GetTriggersOfJob(jobKey, Ct));
    }

    [Fact]
    public async Task Should_reject_running_an_unknown_automation()
    {
        var scheduler = Compose([]).GetRequiredService<IAutomationScheduler>();

        await Assert.ThrowsAsync<ArgumentException>(() => scheduler.RunNowAsync("NoSuchAutomation", Ct));
    }

    [Theory]
    [InlineData(typeof(RemoteWorkSyncJob))]
    [InlineData(typeof(TransportReimbursementJob))]
    public void Should_resolve_each_job_from_the_container(Type jobType)
    {
        Assert.IsType(jobType, Compose([]).GetRequiredService(jobType));
    }

    [Fact]
    public async Task Should_register_each_job_durably_with_quartz()
    {
        var quartz = await Compose([]).GetRequiredService<ISchedulerFactory>().GetScheduler(Ct);

        var job = await quartz.GetJobDetail(new JobKey(AutomationCatalog.TransportReimbursement.Id), Ct);

        Assert.NotNull(job);
        Assert.True(job.Durable);
        Assert.Equal(typeof(TransportReimbursementJob), job.JobType);
        Assert.True(job.ConcurrentExecutionDisallowed);
    }
}
