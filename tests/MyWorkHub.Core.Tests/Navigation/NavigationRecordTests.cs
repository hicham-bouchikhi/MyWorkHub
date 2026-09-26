using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Core.Tests.Navigation;

public sealed class NavigationRecordTests
{
    [Fact]
    public void Should_default_element_and_parameter_to_null_when_only_a_page_type_is_given()
    {
        var target = new NavigationTarget(typeof(string));

        Assert.Equal(typeof(string), target.ViewModelType);
        Assert.Null(target.ElementId);
        Assert.Null(target.Parameter);
    }

    [Fact]
    public void Should_carry_element_id_and_parameter_when_given()
    {
        var target = new NavigationTarget(typeof(string), "work-item-1234", 42);

        Assert.Equal("work-item-1234", target.ElementId);
        Assert.Equal(42, target.Parameter);
    }

    [Fact]
    public void Should_compare_navigation_targets_by_value()
    {
        Assert.Equal(new NavigationTarget(typeof(string), "a"), new NavigationTarget(typeof(string), "a"));
        Assert.NotEqual(new NavigationTarget(typeof(string), "a"), new NavigationTarget(typeof(string), "b"));
    }

    [Fact]
    public void Should_throw_when_navigation_target_page_type_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new NavigationTarget(null!));
    }

    [Fact]
    public void Should_default_navigation_item_order_to_zero()
    {
        var item = new NavigationItem("Todo", "✅", typeof(string));

        Assert.Equal(0, item.Order);
        Assert.Equal("Todo", item.Label);
        Assert.Equal(typeof(string), item.ViewModelType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_throw_when_navigation_item_label_is_blank(string label)
    {
        Assert.Throws<ArgumentException>(() => new NavigationItem(label, "x", typeof(string)));
    }

    [Fact]
    public void Should_throw_when_navigation_item_page_type_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new NavigationItem("Todo", "x", null!));
    }
}
