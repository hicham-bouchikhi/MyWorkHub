using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Core.Tests.Features.Automations;

public sealed class AutomationRunOutcomeTests
{
    private static AutomationStepResult Ok(string name) => new(name, AutomationStepStatus.SUCCESS);

    private static AutomationStepResult Failed(string name, string message) => new(name, AutomationStepStatus.FAILED, message);

    private static AutomationStepResult Skipped(string name) => new(name, AutomationStepStatus.SKIPPED, "nothing to do");

    [Fact]
    public void Should_succeed_when_every_step_succeeded()
    {
        var outcome = AutomationRunOutcome.FromSteps([Ok("A"), Ok("B")]);

        Assert.Equal(AutomationRunStatus.SUCCESS, outcome.Status);
        Assert.Null(outcome.ErrorMessage);
    }

    [Fact]
    public void Should_be_partial_when_some_steps_failed_and_others_succeeded()
    {
        var outcome = AutomationRunOutcome.FromSteps([Ok("Outlook"), Failed("PeopleNet", "login rejected"), Ok("mwork")]);

        Assert.Equal(AutomationRunStatus.PARTIAL, outcome.Status);
        Assert.Equal("PeopleNet: login rejected", outcome.ErrorMessage);
    }

    [Fact]
    public void Should_fail_when_steps_failed_and_none_succeeded()
    {
        var outcome = AutomationRunOutcome.FromSteps([Failed("Download", "timeout"), Skipped("Send")]);

        Assert.Equal(AutomationRunStatus.FAILED, outcome.Status);
        Assert.Equal("Download: timeout", outcome.ErrorMessage);
    }

    [Fact]
    public void Should_summarize_every_failed_step_in_order()
    {
        var outcome = AutomationRunOutcome.FromSteps([Failed("A", "one"), Failed("B", "two")]);

        Assert.Equal("A: one; B: two", outcome.ErrorMessage);
    }

    [Fact]
    public void Should_succeed_when_every_step_was_skipped()
    {
        Assert.Equal(AutomationRunStatus.SUCCESS, AutomationRunOutcome.FromSteps([Skipped("A")]).Status);
    }

    [Fact]
    public void Should_keep_the_finished_steps_of_a_cancelled_run()
    {
        var outcome = AutomationRunOutcome.Cancelled([Ok("A")]);

        Assert.Equal(AutomationRunStatus.CANCELLED, outcome.Status);
        Assert.Equal("A", Assert.Single(outcome.Steps).Name);
    }

    [Fact]
    public void Should_name_known_automations_and_fall_back_to_the_id()
    {
        Assert.Equal("Remote-work sync", AutomationCatalog.DisplayNameOf(AutomationCatalog.RemoteWorkSync.Id));
        Assert.Equal("RetiredJob", AutomationCatalog.DisplayNameOf("RetiredJob"));
    }
}
