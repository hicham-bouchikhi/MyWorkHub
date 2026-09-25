using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Core.Features.Teams;

namespace MyWorkHub.UI.Tests.Features.GraphAuth;

/// <summary>Scriptable Microsoft 365 sign-in. Signing in flips <see cref="IsSignedIn"/> for every fake service sharing it.</summary>
internal sealed class FakeGraphConnection : IGraphConnectionService
{
    public bool IsSignedIn { get; set; } = true;

    /// <summary>When set, sign-in fails with this message.</summary>
    public string? SignInError { get; set; }

    public int SignInCalls { get; private set; }

    public Task<string?> GetSignedInAccountAsync(CancellationToken ct = default)
        => Task.FromResult(IsSignedIn ? "me@cegid.com" : null);

    public Task<GraphSignInResult> SignInAsync(CancellationToken ct = default)
    {
        SignInCalls++;
        if (SignInError is { } error)
        {
            return Task.FromResult(GraphSignInResult.Failure(error));
        }

        IsSignedIn = true;
        return Task.FromResult(GraphSignInResult.Success("me@cegid.com"));
    }

    public Task SignOutAsync(CancellationToken ct = default)
    {
        IsSignedIn = false;
        return Task.CompletedTask;
    }

    /// <summary>Throws like the real Graph services do when no token is available.</summary>
    public void ThrowIfSignedOut()
    {
        if (!IsSignedIn)
        {
            throw new GraphNotConnectedException();
        }
    }
}

internal sealed class FakeEmailService(FakeGraphConnection connection, params EmailItem[] emails) : IEmailService
{
    public List<EmailItem> Emails { get; } = [.. emails];

    public Exception? Failure { get; set; }

    /// <summary>When set, body fetches wait for this gate (to simulate a slow fetch).</summary>
    public TaskCompletionSource? BodyGate { get; set; }

    public List<string> BodyRequests { get; } = [];

    public static EmailItem Item(string id, int minutesAgo = 0)
        => new(id, "Alice", "Subject " + id, "Preview", new DateTime(2026, 9, 25, 9, 0, 0, DateTimeKind.Utc).AddMinutes(-minutesAgo),
            IsFlagged: false, IsRead: false, "Inbox");

    public Task<IReadOnlyList<EmailItem>> GetRecentEmailsAsync(CancellationToken ct = default)
    {
        connection.ThrowIfSignedOut();
        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult<IReadOnlyList<EmailItem>>([.. Emails]);
    }

    public async Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default)
    {
        connection.ThrowIfSignedOut();
        BodyRequests.Add(id);
        if (BodyGate is { } gate)
        {
            await gate.Task;
        }

        return "Body of " + id;
    }
}

internal sealed class FakeCalendarService(FakeGraphConnection connection, params CalendarEvent[] events) : ICalendarService
{
    public int? RequestedDays { get; private set; }

    public Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(int days, CancellationToken ct = default)
    {
        connection.ThrowIfSignedOut();
        RequestedDays = days;
        return Task.FromResult<IReadOnlyList<CalendarEvent>>(events);
    }
}

internal sealed class FakeTeamsService(FakeGraphConnection connection, params TeamsChatItem[] chats) : ITeamsService
{
    public Task<IReadOnlyList<TeamsChatItem>> GetRecentChatsAsync(CancellationToken ct = default)
    {
        connection.ThrowIfSignedOut();
        return Task.FromResult<IReadOnlyList<TeamsChatItem>>(chats);
    }
}
