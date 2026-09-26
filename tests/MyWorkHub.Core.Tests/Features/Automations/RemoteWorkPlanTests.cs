using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Core.Tests.Features.Automations;

public sealed class RemoteWorkPlanTests
{
    [Fact]
    public void Should_sort_and_deduplicate_the_days()
    {
        var plan = RemoteWorkPlan.Create(2026, 9, [9, 2, 9, 5]);

        Assert.Equal([new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 9)], plan.Days);
    }

    [Fact]
    public void Should_reject_a_day_outside_the_month()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RemoteWorkPlan.Create(2026, 9, [31]));
    }

    [Theory]
    [InlineData("2, 5, 9")]
    [InlineData("2 5 9")]
    [InlineData(" 2;5,,9 ")]
    public void Should_parse_days_separated_by_commas_semicolons_or_spaces(string text)
    {
        Assert.True(RemoteWorkPlan.TryParse(2026, 9, text, out var plan, out var error));

        Assert.Null(error);
        Assert.Equal([2, 5, 9], plan!.Days.Select(d => d.Day));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_parse_blank_text_as_an_empty_plan(string? text)
    {
        Assert.True(RemoteWorkPlan.TryParse(2026, 9, text, out var plan, out _));

        Assert.Empty(plan!.Days);
    }

    [Theory]
    [InlineData("2, x", "\"x\"")]
    [InlineData("31", "\"31\"")]
    [InlineData("0", "\"0\"")]
    [InlineData("-3", "\"-3\"")]
    public void Should_explain_which_token_is_not_a_day_of_the_month(string text, string expected)
    {
        Assert.False(RemoteWorkPlan.TryParse(2026, 9, text, out var plan, out var error));

        Assert.Null(plan);
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.Contains("1-30", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_format_the_days_as_parseable_text()
    {
        var plan = RemoteWorkPlan.Create(2026, 9, [12, 3]);

        Assert.Equal("3, 12", plan.FormatDays());
        Assert.True(RemoteWorkPlan.TryParse(2026, 9, plan.FormatDays(), out var roundTripped, out _));
        Assert.Equal(plan.Days, roundTripped!.Days);
    }
}
