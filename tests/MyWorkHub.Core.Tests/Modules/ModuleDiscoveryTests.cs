using System.Reflection;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Core.Tests.Modules;

public sealed class ModuleDiscoveryTests
{
    private static readonly Assembly _thisAssembly = typeof(ModuleDiscoveryTests).Assembly;

    [Fact]
    public void Should_instantiate_every_concrete_implementation_when_scanning_an_assembly()
    {
        var found = ModuleDiscovery.Find<IDiscoveryContract>([_thisAssembly]);

        Assert.Equal([typeof(AlphaModule), typeof(BetaModule)], found.Select(m => m.GetType()));
    }

    [Fact]
    public void Should_skip_abstract_types_interfaces_and_types_without_a_parameterless_constructor()
    {
        var found = ModuleDiscovery.Find<IDiscoveryContract>([_thisAssembly]).Select(m => m.GetType()).ToList();

        Assert.DoesNotContain(typeof(AbstractModule), found);
        Assert.DoesNotContain(typeof(IDerivedDiscoveryContract), found);
        Assert.DoesNotContain(typeof(NoDefaultConstructorModule), found);
    }

    [Fact]
    public void Should_return_new_instances_on_each_call()
    {
        var first = ModuleDiscovery.Find<IDiscoveryContract>([_thisAssembly])[0];
        var second = ModuleDiscovery.Find<IDiscoveryContract>([_thisAssembly])[0];

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Should_return_empty_when_no_type_implements_the_contract()
    {
        Assert.Empty(ModuleDiscovery.Find<IUnimplementedContract>([_thisAssembly]));
    }

    [Fact]
    public void Should_return_empty_when_no_assemblies_are_given()
    {
        Assert.Empty(ModuleDiscovery.Find<IDiscoveryContract>([]));
    }

    [Fact]
    public void Should_scan_each_assembly_once_when_it_is_listed_twice()
    {
        var found = ModuleDiscovery.Find<IDiscoveryContract>([_thisAssembly, _thisAssembly]);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Should_find_nothing_in_an_assembly_without_implementations()
    {
        Assert.Empty(ModuleDiscovery.Find<IDiscoveryContract>([typeof(ModuleDiscovery).Assembly]));
    }

    [Fact]
    public void Should_throw_when_assemblies_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => ModuleDiscovery.Find<IDiscoveryContract>(null!));
    }
}

public interface IDiscoveryContract
{
    string Name { get; }
}

public interface IDerivedDiscoveryContract : IDiscoveryContract
{
    int Extra { get; }
}

public interface IUnimplementedContract
{
    string Name { get; }
}

// Declared out of alphabetical order to prove results are sorted by full type name.
public sealed class BetaModule : IDiscoveryContract
{
    public string Name => "beta";
}

public sealed class AlphaModule : IDiscoveryContract
{
    public string Name => "alpha";
}

public abstract class AbstractModule : IDiscoveryContract
{
    public abstract string Name { get; }
}

public sealed class NoDefaultConstructorModule : IDiscoveryContract
{
    public NoDefaultConstructorModule(string name) => Name = name;

    public string Name { get; }
}
