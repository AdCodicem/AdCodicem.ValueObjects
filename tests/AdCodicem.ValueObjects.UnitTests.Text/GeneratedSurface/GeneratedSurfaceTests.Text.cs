namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The text family's part of GeneratedSurfaceTests: every sample held to Blazor.

public partial class GeneratedSurfaceTests
{
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_binds_through_a_Blazor_input(string type)
        => Samples.All[type].BindsThroughABlazorInput();
}
