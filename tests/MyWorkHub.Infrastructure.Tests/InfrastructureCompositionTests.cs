using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Infrastructure.Tests;

public sealed class InfrastructureCompositionTests
{
    private static IConfiguration CreateConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Sample:Greeting"] = "hello" })
            .Build();

    [Fact]
    public void Should_register_the_credential_store_when_no_feature_modules_are_installed()
    {
        var services = new ServiceCollection().AddInfrastructure(CreateConfiguration());

        Assert.Contains(services, d => d.ServiceType == typeof(ICredentialStore));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(SampleMarker));
    }

    [Theory]
    [InlineData(typeof(Core.Features.GraphAuth.IGraphConnectionService))]
    [InlineData(typeof(Microsoft.Graph.GraphServiceClient))]
    [InlineData(typeof(Core.Features.Email.IEmailService))]
    [InlineData(typeof(Core.Features.Calendar.ICalendarService))]
    [InlineData(typeof(Core.Features.Teams.ITeamsService))]
    public void Should_register_the_microsoft_365_services_through_their_modules(Type serviceType)
    {
        var services = new ServiceCollection().AddInfrastructure(CreateConfiguration());

        Assert.Contains(services, d => d.ServiceType == serviceType);
    }

    [Theory]
    [InlineData(typeof(Core.Features.AzureDevOps.IAzureDevOpsService))]
    [InlineData(typeof(Core.Features.AzureDevOps.IAzureDevOpsConnectionService))]
    [InlineData(typeof(Core.Features.AzureDevOps.ISeenMentionRepository))]
    public void Should_resolve_the_azure_devops_services_through_their_module(Type serviceType)
    {
        var services = new ServiceCollection().AddInfrastructure(CreateConfiguration());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Theory]
    [InlineData(typeof(Core.Features.CliAgent.ICliAgentRunner))]
    [InlineData(typeof(Core.Features.EmailSummary.IEmailSummaryService))]
    [InlineData(typeof(Core.Features.PrReview.IPrReviewService))]
    public void Should_resolve_the_ai_services_through_their_modules(Type serviceType)
    {
        var services = new ServiceCollection().AddInfrastructure(CreateConfiguration());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Theory]
    [InlineData(typeof(Core.Features.Automations.IAutomationLogger))]
    [InlineData(typeof(Core.Features.Automations.IAutomationCredentialService))]
    [InlineData(typeof(Core.Features.Automations.IRemoteWorkPlanRepository))]
    [InlineData(typeof(Core.Features.Automations.IRemoteWorkSyncAutomation))]
    [InlineData(typeof(Core.Features.Automations.ITransportReimbursementAutomation))]
    public void Should_resolve_the_automation_services_through_their_module(Type serviceType)
    {
        using var provider = new ServiceCollection().AddInfrastructure(CreateConfiguration()).BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Fact]
    public void Should_invoke_each_discovered_module_with_the_configuration()
    {
        var provider = new ServiceCollection()
            .AddInfrastructure(CreateConfiguration(), [typeof(InfrastructureCompositionTests).Assembly])
            .BuildServiceProvider();

        Assert.Equal("hello", provider.GetRequiredService<SampleMarker>().Greeting);
    }

    [Fact]
    public void Should_throw_when_configuration_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddInfrastructure(null!));
    }
}

public sealed record SampleMarker(string Greeting);

/// <summary>Discovered only when a test passes this test assembly to <c>AddInfrastructure</c>.</summary>
public sealed class SampleInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(new SampleMarker(configuration["Sample:Greeting"] ?? ""));
    }
}
