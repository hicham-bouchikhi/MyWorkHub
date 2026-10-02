using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.Features.Settings;
using MyWorkHub.UI.Tests.Features.GraphAuth;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class EmailSettingsViewModelTests
{
    private readonly FakeGraphConnection _connection = new();
    private readonly FakeSettingsService _settings = new();
    private readonly FakeEmailService _email;

    public EmailSettingsViewModelTests()
    {
        _email = new FakeEmailService(_connection);
        _email.Folders.Add(new MailFolderNode("inbox-id", "Inbox", 0, [new MailFolderNode("azdo-id", "Azure DevOps", 0, [])]));
        _email.Folders.Add(new MailFolderNode("sent-id", "Sent Items", 0, []));
        _email.WellKnownFolders["inbox"] = "inbox-id";
    }

    private async Task<EmailSettingsViewModel> LoadedAsync()
    {
        var viewModel = new EmailSettingsViewModel(_settings, _email);
        viewModel.Load();
        await viewModel.LoadFoldersCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Should_show_the_mailbox_tree_with_configured_folders_ticked_including_well_known_names()
    {
        _settings.Email = new EmailSettings(["inbox", "azdo-id"], 40);

        var viewModel = await LoadedAsync();

        Assert.Equal(["Inbox", "Sent Items"], viewModel.Folders.Select(f => f.Name));
        Assert.True(viewModel.Folders[0].IsChecked);
        Assert.True(viewModel.Folders[0].Children[0].IsChecked);
        Assert.False(viewModel.Folders[1].IsChecked);
        Assert.Equal(40, viewModel.MaxPerFolder);
        Assert.False(viewModel.ShowFavorites);
    }

    [Fact]
    public async Task Should_save_the_ticked_folders_by_id_and_the_limit()
    {
        var viewModel = await LoadedAsync();
        viewModel.Folders[0].IsChecked = false;
        viewModel.Folders[0].Children[0].IsChecked = true;
        viewModel.Folders[1].IsChecked = true;
        viewModel.MaxPerFolder = 10;
        viewModel.ShowFavorites = true;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.IsType<EmailSettings>(Assert.Single(_settings.Saves));
        Assert.Equal(["azdo-id", "sent-id"], saved.FolderIds);
        Assert.Equal(10, saved.MaxPerFolder);
        Assert.True(saved.ShowFavorites);
        Assert.NotNull(viewModel.StatusMessage);
    }

    [Fact]
    public async Task Should_keep_the_configured_folders_and_ask_to_sign_in_when_the_mailbox_cannot_be_read()
    {
        _connection.IsSignedIn = false;
        _settings.Email = new EmailSettings(["inbox", "azdo-id"], 25);
        var viewModel = await LoadedAsync();

        Assert.Empty(viewModel.Folders);
        Assert.NotNull(viewModel.FolderNotice);

        viewModel.MaxPerFolder = 50;
        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.IsType<EmailSettings>(Assert.Single(_settings.Saves));
        Assert.Equal(["inbox", "azdo-id"], saved.FolderIds);
        Assert.Equal(50, saved.MaxPerFolder);
    }

    [Fact]
    public async Task Should_show_why_when_saving_fails()
    {
        var viewModel = await LoadedAsync();
        _settings.Failure = new IOException("disk full");

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Contains("disk full", viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_be_unavailable_without_a_settings_store()
    {
        Assert.False(new EmailSettingsViewModel().IsAvailable);
    }
}
