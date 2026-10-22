namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The testdata family's part of GeneratedSurfaceTests: every sample held to the test-data sampler.

public partial class GeneratedSurfaceTests
{
    /// <summary>
    /// The test-data sampler draws a value of every underlying type, option and hook from the schema the generator writes,
    /// and every value it draws is one the type accepts.
    /// </summary>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_drawn_as_values_its_rules_accept(string type)
        => Samples.All[type].DrawsValuesItsRulesAccept();
}
