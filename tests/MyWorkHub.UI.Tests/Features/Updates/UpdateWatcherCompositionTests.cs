using MyWorkHub.Presentation.Features.Updates;
using MyWorkHub.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests.Features.Updates;

public sealed class UpdateWatcherCompositionTests
{
    [Fact]
    public void Should_provide_one_shared_watcher_in_the_default_composition()
    {
        using var provider = new ServiceCollection().AddUi().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<UpdateWatcher>(), provider.GetRequiredService<UpdateWatcher>());
    }
}
