using System.Globalization;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Sites;

namespace MyWorkHub.Infrastructure.Features.Automations.RemoteWork;

/// <summary>
/// <see cref="IRemoteWorkSyncAutomation"/>: Outlook through Graph, PeopleNet and mwork through a scripted
/// browser. Each method is self-contained (its own Graph call or browser session), which is what lets the
/// job isolate them from one another.
/// <para>
/// The browser part is a generic "sign in, click each day, save" script driven entirely by configured
/// selectors (<see cref="SiteSelectorKeys"/>); the real PeopleNet/mwork page structure is not known to the
/// code. In particular the script cannot tell whether a day is already declared — on a site where clicking a
/// day toggles it, the configured <see cref="SiteSelectorKeys.REMOTE_DAY_TEMPLATE"/> must target only
/// not-yet-declared days (e.g. with a <c>:not(.selected)</c> clause) for re-runs to stay idempotent.
/// </para>
/// </summary>
internal sealed class RemoteWorkSyncAutomation : IRemoteWorkSyncAutomation
{
    private const string ISO_DATE_FORMAT = "yyyy-MM-dd";

    private readonly OutlookRemoteWorkCalendar _outlook;
    private readonly SiteScriptRunner _sites;
    private readonly ExternalSitesOptions _siteOptions;

    public RemoteWorkSyncAutomation(OutlookRemoteWorkCalendar outlook, SiteScriptRunner sites, ExternalSitesOptions siteOptions)
    {
        ArgumentNullException.ThrowIfNull(outlook);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(siteOptions);
        _outlook = outlook;
        _sites = sites;
        _siteOptions = siteOptions;
    }

    public Task SyncOutlookAsync(RemoteWorkPlan plan, CancellationToken ct = default)
        => _outlook.AddMissingDaysAsync(plan, ct);

    public Task SyncPeopleNetAsync(RemoteWorkPlan plan, CancellationToken ct = default)
        => DeclareDaysAsync(_siteOptions.PeopleNet, plan, ct);

    public Task SyncMWorkAsync(RemoteWorkPlan plan, CancellationToken ct = default)
        => DeclareDaysAsync(_siteOptions.MWork, plan, ct);

    private async Task DeclareDaysAsync(SiteOptions site, RemoteWorkPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Days.Count == 0)
        {
            return;
        }

        var dayTemplate = site.RequireSelector(SiteSelectorKeys.REMOTE_DAY_TEMPLATE);
        var saveButton = site.RequireSelector(SiteSelectorKeys.SAVE_BUTTON);
        if (!dayTemplate.Contains(SiteSelectorKeys.DATE_PLACEHOLDER, StringComparison.Ordinal))
        {
            throw new AutomationException(
                $"{site.DisplayName}: {site.ConfigurationPath}:Selectors:{SiteSelectorKeys.REMOTE_DAY_TEMPLATE} " +
                $"must contain {SiteSelectorKeys.DATE_PLACEHOLDER} where the day goes.");
        }

        await _sites.RunAsync(site, async session =>
        {
            foreach (var day in plan.Days)
            {
                var selector = dayTemplate.Replace(
                    SiteSelectorKeys.DATE_PLACEHOLDER,
                    day.ToString(ISO_DATE_FORMAT, CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
                await session.ClickAsync(selector, ct).ConfigureAwait(false);
            }

            await session.ClickAsync(saveButton, ct).ConfigureAwait(false);
            return plan.Days.Count;
        }, ct).ConfigureAwait(false);
    }
}
