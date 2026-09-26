namespace MyWorkHub.Core.Navigation;

/// <summary>
/// Where to navigate: a page view-model type, optionally a specific element on that page (e.g. the
/// id of one work item or email row) and an optional page-specific parameter. Lives in Core so that
/// UI-agnostic callers — notifications raised by background services, automations — can carry a
/// structured destination without referencing presentation types.
/// </summary>
/// <param name="ViewModelType">The page view-model type to show.</param>
/// <param name="ElementId">When set, the page is asked to focus/scroll to this element after navigation
/// (only if it supports deep links).</param>
/// <param name="Parameter">Optional page-specific payload.</param>
public sealed record NavigationTarget(Type ViewModelType, string? ElementId = null, object? Parameter = null)
{
    public Type ViewModelType { get; init; } = ViewModelType ?? throw new ArgumentNullException(nameof(ViewModelType));
}
