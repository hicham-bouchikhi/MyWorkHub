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
