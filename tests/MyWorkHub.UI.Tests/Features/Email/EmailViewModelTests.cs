using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.UI.Tests.Features.GraphAuth;

namespace MyWorkHub.UI.Tests.Features.Email;

public sealed class EmailViewModelTests
{
    private static async Task<EmailViewModel> LoadedAsync(FakeEmailService email, FakeGraphConnection connection)
    {
        var viewModel = new EmailViewModel(email, connection);
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    // --- Loading -----------------------------------------------------------------------

    [Fact]
    public async Task Should_show_the_messages_when_loaded()
    {
        var connection = new FakeGraphConnection();
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("a"), FakeEmailService.Item("b")), connection);

        Assert.Equal(["a", "b"], viewModel.Items.Select(r => r.Id));
        Assert.False(viewModel.NeedsSignIn);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Should_offer_sign_in_instead_of_an_error_when_not_connected()
    {
        var connection = new FakeGraphConnection { IsSignedIn = false };

        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("a")), connection);

        Assert.True(viewModel.NeedsSignIn);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Items);
        Assert.True(viewModel.SignInCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_show_the_messages_after_signing_in()
    {
        var connection = new FakeGraphConnection { IsSignedIn = false };
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("a")), connection);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal(1, connection.SignInCalls);
        Assert.False(viewModel.NeedsSignIn);
        Assert.Equal(["a"], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_keep_the_prompt_and_show_why_when_sign_in_fails()
    {
        var connection = new FakeGraphConnection { IsSignedIn = false, SignInError = "Sign-in was cancelled." };
        var viewModel = await LoadedAsync(new FakeEmailService(connection), connection);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.True(viewModel.NeedsSignIn);
        Assert.Equal("Sign-in was cancelled.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Should_show_an_error_when_loading_fails()
    {
        var connection = new FakeGraphConnection();
        var email = new FakeEmailService(connection) { Failure = new HttpRequestException("offline") };

        var viewModel = await LoadedAsync(email, connection);

        Assert.Contains("offline", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.NeedsSignIn);
    }

    [Fact]
    public async Task Should_degrade_gracefully_when_no_mail_service_is_registered()
    {
        var viewModel = new EmailViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsAvailable);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.SignInCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_load_only_once_but_pick_up_new_messages_on_refresh()
    {
        var connection = new FakeGraphConnection();
        var email = new FakeEmailService(connection, FakeEmailService.Item("a"));
        var viewModel = await LoadedAsync(email, connection);
        email.Emails.Insert(0, FakeEmailService.Item("new"));

        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Equal(["a"], viewModel.Items.Select(r => r.Id));

        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(["new", "a"], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_keep_the_selected_message_across_a_refresh()
    {
        var connection = new FakeGraphConnection();
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("a"), FakeEmailService.Item("b")), connection);
        viewModel.SelectedItem = viewModel.Items[1];

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("b", viewModel.SelectedItem?.Id);
    }

    // --- Reading pane ---------------------------------------------------------------------

    [Fact]
    public async Task Should_fetch_and_show_the_body_of_the_selected_message()
    {
        var connection = new FakeGraphConnection();
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("a")), connection);

        viewModel.SelectedItem = viewModel.Items[0];

        Assert.Equal("Body of a", viewModel.SelectedHtml);
        Assert.False(viewModel.IsBodyLoading);
    }

    [Fact]
    public async Task Should_ignore_a_slow_body_that_arrives_after_the_user_moved_on()
    {
        var connection = new FakeGraphConnection();
        var email = new FakeEmailService(connection, FakeEmailService.Item("a"), FakeEmailService.Item("b"));
        var viewModel = await LoadedAsync(email, connection);
        var slow = new TaskCompletionSource();
        email.BodyGate = slow;

        viewModel.SelectedItem = viewModel.Items[0];
        Assert.True(viewModel.IsBodyLoading);
        email.BodyGate = null;
        viewModel.SelectedItem = viewModel.Items[1];
        slow.SetResult();
        await Task.Yield();

        Assert.Equal("Body of b", viewModel.SelectedHtml);
        Assert.False(viewModel.IsBodyLoading);
    }

    [Fact]
    public async Task Should_offer_sign_in_when_the_session_expires_while_reading()
    {
        var connection = new FakeGraphConnection();
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("a")), connection);
        connection.IsSignedIn = false;

        viewModel.SelectedItem = viewModel.Items[0];

        Assert.True(viewModel.NeedsSignIn);
        Assert.Equal("", viewModel.SelectedHtml);
    }

    // --- Deep links (IDeepLinkTarget) --------------------------------------------------------

    [Fact]
    public async Task Should_select_highlight_and_open_the_message_when_focusing_a_loaded_item()
    {
        var connection = new FakeGraphConnection();
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("other"), FakeEmailService.Item("target")), connection);

        viewModel.FocusElement("target");

        Assert.Equal("target", viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
        Assert.Equal("Body of target", viewModel.SelectedHtml);
    }

    [Fact]
    public async Task Should_apply_a_focus_request_made_before_loading_once_the_list_loads()
    {
        var connection = new FakeGraphConnection();
        var viewModel = new EmailViewModel(new FakeEmailService(connection, FakeEmailService.Item("other"), FakeEmailService.Item("target")), connection);

        viewModel.FocusElement("target");
        Assert.Null(viewModel.SelectedItem);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal("target", viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Fact]
    public async Task Should_keep_a_focus_request_pending_through_sign_in()
    {
        // A mail notification clicked while signed out: the row is revealed once the user signs in.
        var connection = new FakeGraphConnection { IsSignedIn = false };
        var viewModel = new EmailViewModel(new FakeEmailService(connection, FakeEmailService.Item("target")), connection);
        viewModel.FocusElement("target");
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Null(viewModel.SelectedItem);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal("target", viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Fact]
    public async Task Should_ignore_a_focus_request_for_an_unknown_message()
    {
        var connection = new FakeGraphConnection();
        var viewModel = await LoadedAsync(new FakeEmailService(connection, FakeEmailService.Item("only")), connection);

        viewModel.FocusElement("moved-or-deleted");

        Assert.Null(viewModel.SelectedItem);
        Assert.Null(viewModel.HighlightedItem);
    }

    [Fact]
    public void Should_build_a_navigation_target_whose_element_id_is_the_message_id()
    {
        var target = EmailViewModel.TargetFor("AAMkAGI2");

        Assert.Equal(typeof(EmailViewModel), target.ViewModelType);
        Assert.Equal("AAMkAGI2", target.ElementId);
    }

    [Fact]
    public async Task Should_not_fault_when_a_running_load_is_superseded_by_a_new_one()
    {
        var connection = new FakeGraphConnection();
        var email = new FakeEmailService(connection, FakeEmailService.Item("a")) { ListGate = new TaskCompletionSource() };
        var viewModel = new EmailViewModel(email, connection);

        // The toolkit cancels the first run's token when the command executes again (e.g. the page is revisited).
        var first = viewModel.RefreshCommand.ExecuteAsync(null);
        email.ListGate = null;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        await first;

        Assert.Equal(["a"], viewModel.Items.Select(r => r.Id));
        Assert.Null(viewModel.ErrorMessage);
    }
}
