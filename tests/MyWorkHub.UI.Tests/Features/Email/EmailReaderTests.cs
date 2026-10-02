using MyWorkHub.Core.Features.Email;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.UI.Tests.Features.AzureDevOps;
using MyWorkHub.UI.Tests.Features.GraphAuth;
using MyWorkHub.UI.Tests.Features.Settings;

namespace MyWorkHub.UI.Tests.Features.Email;

/// <summary>The HTML reading pane, the remote-image opt-in, link handling and the folder tree.</summary>
public sealed class EmailReaderTests
{
    private readonly FakeGraphConnection _connection = new();
    private readonly FakeBrowserLauncher _browser = new();

    private readonly FakeSettingsService _settings = new();

    private async Task<EmailViewModel> LoadedAsync(FakeEmailService email)
    {
        var viewModel = new EmailViewModel(email, _connection, browser: _browser, settings: _settings);
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Should_block_remote_images_until_asked_and_again_for_the_next_message()
    {
        var email = new FakeEmailService(_connection, FakeEmailService.Item("a"), FakeEmailService.Item("b"));
        var viewModel = await LoadedAsync(email);
        viewModel.SelectedItem = viewModel.Items[0];
        viewModel.SelectedHtml = "<img src=\"https://cdn.example/logo.png\">";

        Assert.True(viewModel.HasBlockedImages);
        Assert.Contains("img-src data:;", viewModel.SelectedDocument, StringComparison.Ordinal);

        viewModel.LoadImagesCommand.Execute(null);

        Assert.False(viewModel.HasBlockedImages);
        Assert.Contains("img-src data: https: http:;", viewModel.SelectedDocument, StringComparison.Ordinal);

        viewModel.SelectedItem = viewModel.Items[1];

        Assert.False(viewModel.AreRemoteImagesAllowed);
    }

    [Theory]
    [InlineData("https://dev.azure.com/cegid/Retail", true)]
    [InlineData("mailto:someone@cegid.com", true)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task Should_open_only_web_and_mail_links_in_the_default_browser(string url, bool opened)
    {
        var viewModel = await LoadedAsync(new FakeEmailService(_connection));

        viewModel.OpenLink(new Uri(url));

        Assert.Equal(opened ? [url] : [], _browser.Opened);
    }

    [Fact]
    public async Task Should_hide_favorites_and_open_on_the_first_folder_by_default()
    {
        var email = new FakeEmailService(_connection, FakeEmailService.Item("w"));
        email.Folders.Add(new MailFolderNode("inbox-id", "Inbox", 0, []));
        email.Folders.Add(new MailFolderNode("sent-id", "Sent Items", 0, []));
        email.FolderEmails["inbox-id"] = [FakeEmailService.Item("i1")];

        var viewModel = await LoadedAsync(email);

        Assert.Equal(["Inbox", "Sent Items"], viewModel.Folders.Select(f => f.Name));
        Assert.Same(viewModel.Folders[0], viewModel.SelectedFolder);
        Assert.Equal(["i1"], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_still_list_the_favorites_when_hidden_but_the_folder_tree_is_empty()
    {
        var viewModel = await LoadedAsync(new FakeEmailService(_connection, FakeEmailService.Item("w")));

        Assert.Empty(viewModel.Folders);
        Assert.Equal(["w"], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_show_the_mailbox_tree_under_a_favorites_entry_that_is_selected_first()
    {
        _settings.Email = _settings.Email with { ShowFavorites = true };
        var email = new FakeEmailService(_connection, FakeEmailService.Item("w"));
        email.Folders.Add(new MailFolderNode("inbox-id", "Inbox", 3, [new MailFolderNode("azdo-id", "Azure DevOps", 0, [])]));

        var viewModel = await LoadedAsync(email);

        Assert.Equal(["Favorites", "Inbox"], viewModel.Folders.Select(f => f.Name));
        Assert.Equal("Azure DevOps", Assert.Single(viewModel.Folders[1].Children).Name);
        Assert.Equal("3", viewModel.Folders[1].UnreadText);
        Assert.Same(viewModel.Folders[0], viewModel.SelectedFolder);
        Assert.Equal(["w"], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_list_a_folder_when_it_is_selected_in_the_tree()
    {
        _settings.Email = _settings.Email with { ShowFavorites = true };
        var email = new FakeEmailService(_connection, FakeEmailService.Item("w"));
        email.Folders.Add(new MailFolderNode("azdo-id", "Azure DevOps", 0, []));
        email.FolderEmails["azdo-id"] = [FakeEmailService.Item("p1"), FakeEmailService.Item("p2")];
        var viewModel = await LoadedAsync(email);

        viewModel.SelectedFolder = viewModel.Folders[1];
        await viewModel.LoadFolderCommand.ExecutionTask!;

        Assert.Equal(["p1", "p2"], viewModel.Items.Select(r => r.Id));
    }
}
