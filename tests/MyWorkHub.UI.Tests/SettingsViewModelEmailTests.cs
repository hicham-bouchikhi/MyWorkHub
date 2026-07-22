using MyWorkHub.Core.Models;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class SettingsViewModelEmailTests
{
    private static MailFolder Folder(string id, string name, bool watched, int unread = 0) =>
        new(id, name, unread, watched);

    [Fact]
    public void Loads_mail_folders_from_the_service_on_construction()
    {
        var email = new FakeEmailService(folders:
        [
            Folder("inbox-id", "Inbox", watched: true, unread: 2),
            Folder("clients-id", "Clients", watched: false),
        ]);

        var vm = new SettingsViewModel(emailService: email);

        Assert.Equal(2, vm.EmailFolders.Count);
        var inbox = vm.EmailFolders.Single(f => f.Id == "inbox-id");
        Assert.Equal("Inbox", inbox.DisplayName);
        Assert.Equal(2, inbox.UnreadItemCount);
        Assert.True(inbox.IsWatched);
        Assert.False(vm.EmailFolders.Single(f => f.Id == "clients-id").IsWatched);
    }

    [Fact]
    public void Save_email_folders_persists_only_the_watched_folder_ids()
    {
        var settings = new FakeEmailSettingsService();
        var email = new FakeEmailService(folders:
        [
            Folder("inbox-id", "Inbox", watched: true),
            Folder("clients-id", "Clients", watched: false),
        ]);
        var vm = new SettingsViewModel(emailService: email, emailSettings: settings);

        // The user also ticks the "Clients" folder.
        vm.EmailFolders.Single(f => f.Id == "clients-id").IsWatched = true;

        vm.SaveEmailFoldersCommand.Execute(null);

        Assert.Equal(["inbox-id", "clients-id"], settings.SavedFolderIds);
    }

    [Fact]
    public void Save_email_folders_includes_partially_watched_ancestors()
    {
        var settings = new FakeEmailSettingsService();
        // "Clients" has two children; only one is watched, so Clients loads as the
        // partial (indeterminate) state — which still counts it as watched. Inbox in
        // turn is partial (Clients partial, its own state derived), so it too is watched.
        var tree = new MailFolder("inbox-id", "Inbox", 0, IsWatched: true)
        {
            Children =
            [
                new MailFolder("clients-id", "Clients", 0, IsWatched: false)
                {
                    Children =
                    [
                        new MailFolder("proj-id", "ProjectX", 0, IsWatched: true),
                        new MailFolder("proj2-id", "ProjectY", 0, IsWatched: false),
                    ],
                },
            ],
        };
        var vm = new SettingsViewModel(emailService: new FakeEmailService(folders: [tree]), emailSettings: settings);

        vm.SaveEmailFoldersCommand.Execute(null);

        // proj2 is the only fully-unchecked node, so it is excluded; everything above it
        // is partial (or checked) and therefore watched.
        Assert.Equal(["inbox-id", "clients-id", "proj-id"], settings.SavedFolderIds);
    }

    [Fact]
    public async Task Load_email_folders_surfaces_a_message_when_the_service_fails()
    {
        var email = new FakeEmailService(throwOnCall: true);
        var vm = new SettingsViewModel(emailService: email);

        await vm.LoadEmailFoldersCommand.ExecuteAsync(null);

        Assert.Empty(vm.EmailFolders);
        Assert.NotNull(vm.EmailStatusMessage);
    }
}
