using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.Features.Settings;
using MyWorkHub.UI.Tests.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class AzureDevOpsSettingsViewModelTests
{
    private readonly FakeSettingsService _settings = new();
    private readonly FakeAzureDevOpsConnection _connection = new() { HasToken = false };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AzureDevOpsSettingsViewModel Loaded(IAzureDevOpsConnectionService? connection = null)
    {
        var vm = new AzureDevOpsSettingsViewModel(_settings, connection ?? _connection);
        vm.Load();
        return vm;
    }

    [Fact]
    public void Should_show_the_stored_organization_projects_and_token_status_when_loading()
    {
        _settings.AzureDevOps = new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Alpha", "Beta"]);
        _connection.HasToken = true;

        var vm = Loaded();

        Assert.Equal("https://dev.azure.com/acme/", vm.OrganizationUrl);
        Assert.Equal(["Alpha", "Beta"], vm.Projects);
        Assert.True(vm.HasStoredToken);
        Assert.Contains("A token is stored", vm.TokenStatus, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "Enter your organization URL")]
    [InlineData("   ", "Enter your organization URL")]
    [InlineData("cegid", "not a valid organization URL")]
    [InlineData("ftp://dev.azure.com/cegid/", "not a valid organization URL")]
    [InlineData("https://dev.azure.com/cegid/?view=all", "not a valid organization URL")]
    [InlineData("https://dev.azure.com/", "Include your organization")]
    [InlineData("https://dev.azure.com", "Include your organization")]
    public async Task Should_reject_a_malformed_organization_url_and_save_nothing(string url, string expected)
    {
        var vm = Loaded();
        vm.OrganizationUrl = url;

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Contains(expected, vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(_settings.Saves);
    }

    [Theory]
    [InlineData("https://dev.azure.com/acme", "https://dev.azure.com/acme/")]
    [InlineData("  https://dev.azure.com/acme/  ", "https://dev.azure.com/acme/")]
    [InlineData("https://acme.visualstudio.com", "https://acme.visualstudio.com/")]
    [InlineData("http://tfs.local:8080/tfs/DefaultCollection", "http://tfs.local:8080/tfs/DefaultCollection/")]
    public async Task Should_save_the_normalized_organization_url(string url, string expected)
    {
        var vm = Loaded();
        vm.OrganizationUrl = url;

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(expected, _settings.AzureDevOps.OrganizationUrl);
        Assert.Equal(expected, vm.OrganizationUrl);
    }

    [Fact]
    public void Should_trim_and_ignore_duplicate_or_blank_project_names_when_adding()
    {
        var vm = Loaded();

        foreach (var name in new[] { "  Alpha ", "alpha", "   ", "Beta" })
        {
            vm.NewProject = name;
            if (vm.AddProjectCommand.CanExecute(null))
            {
                vm.AddProjectCommand.Execute(null);
            }
        }

        Assert.Equal(["Alpha", "Beta"], vm.Projects);
        Assert.Equal("", vm.NewProject);
    }

    [Fact]
    public void Should_clean_project_names_by_trimming_and_deduplicating_case_insensitively()
    {
        Assert.Equal(["Alpha", "Beta"], AzureDevOpsSettingsViewModel.CleanProjects([" Alpha ", "", "ALPHA", "Beta", "beta "]));
    }

    [Fact]
    public async Task Should_save_the_projects_including_one_typed_but_not_yet_added()
    {
        var vm = Loaded();
        vm.NewProject = "Alpha";
        vm.AddProjectCommand.Execute(null);
        vm.NewProject = " Beta ";

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Equal(new AzureDevOpsSettings("https://dev.azure.com/cegid/", _settings.AzureDevOps.Projects), Assert.Single(_settings.Saves));
        Assert.Equal(["Alpha", "Beta"], _settings.AzureDevOps.Projects);
        Assert.Equal("", vm.NewProject);
        Assert.Contains("next load", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_remove_a_project_and_warn_when_saving_without_any()
    {
        _settings.AzureDevOps = new AzureDevOpsSettings("https://dev.azure.com/cegid/", ["Alpha"]);
        var vm = Loaded();

        vm.RemoveProjectCommand.Execute("Alpha");
        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Empty(_settings.AzureDevOps.Projects);
        Assert.Contains("at least one project", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_save_the_organization_before_verifying_the_token_against_it()
    {
        var log = new List<string>();
        _settings.Log = log;
        var connection = new OrderRecordingConnection(log);
        var vm = Loaded(connection);
        vm.OrganizationUrl = "https://dev.azure.com/acme";
        vm.PersonalAccessToken = "good-pat";

        await vm.ConnectCommand.ExecuteAsync(Ct);

        Assert.Equal(["save AzureDevOpsSettings", "connect good-pat"], log);
        Assert.Equal("", vm.PersonalAccessToken);
        Assert.True(vm.HasStoredToken);
        Assert.Contains("Connected as Jane Doe", vm.TokenStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_not_verify_the_token_when_the_organization_url_is_invalid()
    {
        var vm = Loaded();
        vm.OrganizationUrl = "https://dev.azure.com/";
        vm.PersonalAccessToken = FakeAzureDevOpsConnection.VALID_TOKEN;

        await vm.ConnectCommand.ExecuteAsync(Ct);

        Assert.Empty(_connection.ConnectAttempts);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Should_share_the_token_with_the_azure_devops_pages_through_the_connection_service()
    {
        var vm = Loaded();
        vm.PersonalAccessToken = FakeAzureDevOpsConnection.VALID_TOKEN;

        await vm.ConnectCommand.ExecuteAsync(Ct);

        Assert.Equal([FakeAzureDevOpsConnection.VALID_TOKEN], _connection.ConnectAttempts);
        Assert.True(_connection.HasStoredToken);
    }

    [Fact]
    public async Task Should_keep_the_token_text_and_explain_when_the_token_is_rejected()
    {
        var vm = Loaded();
        vm.PersonalAccessToken = "bad-pat";

        await vm.ConnectCommand.ExecuteAsync(Ct);

        Assert.Contains("rejected", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("bad-pat", vm.PersonalAccessToken);
        Assert.False(vm.HasStoredToken);
    }

    [Fact]
    public void Should_forget_the_stored_token()
    {
        _connection.HasToken = true;
        var vm = Loaded();

        vm.DisconnectCommand.Execute(null);

        Assert.False(_connection.HasStoredToken);
        Assert.False(vm.HasStoredToken);
        Assert.False(vm.DisconnectCommand.CanExecute(null));
    }

    [Fact]
    public void Should_disable_saving_and_hide_the_token_panel_when_their_services_are_missing()
    {
        var vm = new AzureDevOpsSettingsViewModel();
        vm.Load();
        vm.PersonalAccessToken = "pat";

        Assert.False(vm.IsAvailable);
        Assert.False(vm.CanManageToken);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.ConnectCommand.CanExecute(null));
    }

    private sealed class OrderRecordingConnection(List<string> log) : IAzureDevOpsConnectionService
    {
        public bool HasStoredToken { get; private set; }

        public Task<AzureDevOpsConnectResult> ConnectAsync(string personalAccessToken, CancellationToken ct = default)
        {
            log.Add("connect " + personalAccessToken);
            HasStoredToken = true;
            return Task.FromResult(AzureDevOpsConnectResult.Success("Jane Doe"));
        }

        public void Disconnect() => HasStoredToken = false;
    }
}
