using System.Globalization;

namespace MyWorkHub.Core.Features.Automations;

/// <summary>The days of one month the user works remotely — the source of truth the sync job copies out.</summary>
public sealed class RemoteWorkPlan
{
    private static readonly char[] _separators = [',', ';', ' '];

    private RemoteWorkPlan(int year, int month, IReadOnlyList<DateOnly> days)
    {
        Year = year;
        Month = month;
        Days = days;
    }

    public int Year { get; }

    public int Month { get; }

    /// <summary>Distinct days, ascending, all inside <see cref="Year"/>/<see cref="Month"/>.</summary>
    public IReadOnlyList<DateOnly> Days { get; }

    /// <summary>Builds a plan from day-of-month numbers (duplicates collapse). Throws for a day outside the month.</summary>
    public static RemoteWorkPlan Create(int year, int month, IEnumerable<int> daysOfMonth)
    {
        ArgumentNullException.ThrowIfNull(daysOfMonth);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var days = new SortedSet<int>();
        foreach (var day in daysOfMonth)
        {
            if (day < 1 || day > daysInMonth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(daysOfMonth), day, $"Day {day} is not in {year}-{month:00} (1-{daysInMonth}).");
            }

            days.Add(day);
        }

        return new RemoteWorkPlan(year, month, [.. days.Select(d => new DateOnly(year, month, d))]);
    }

    /// <summary>
    /// Parses a user-typed list of day numbers separated by commas, semicolons or spaces (e.g. "2, 5 9").
    /// Returns false with a displayable <paramref name="error"/> for anything that is not a day of the month.
    /// </summary>
    public static bool TryParse(int year, int month, string? text, out RemoteWorkPlan? plan, out string? error)
    {
        var tokens = (text ?? "").Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var days = new List<int>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var day) || day < 1 || day > daysInMonth)
            {
                plan = null;
                error = $"\"{token}\" is not a day of this month (1-{daysInMonth}).";
                return false;
            }

            days.Add(day);
        }

        plan = Create(year, month, days);
        error = null;
        return true;
    }

    /// <summary>The days as a comma-separated list of day numbers (what <see cref="TryParse"/> reads back).</summary>
    public string FormatDays() => string.Join(", ", Days.Select(d => d.Day.ToString(CultureInfo.InvariantCulture)));
}

/// <summary>Local persistence of the monthly remote-work plans.</summary>
public interface IRemoteWorkPlanRepository
{
    /// <summary>The plan for the month; an empty plan when none was saved.</summary>
    Task<RemoteWorkPlan> GetAsync(int year, int month, CancellationToken ct = default);

    /// <summary>Creates or replaces the plan for <paramref name="plan"/>'s month.</summary>
    Task SaveAsync(RemoteWorkPlan plan, CancellationToken ct = default);
}
