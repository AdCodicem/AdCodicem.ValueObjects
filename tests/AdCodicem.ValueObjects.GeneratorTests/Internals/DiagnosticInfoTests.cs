using System.Globalization;
using AdCodicem.ValueObjects.Generators.Diagnostics;
using AdCodicem.ValueObjects.Generators.Internal;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests.Internals;

/// <summary>
/// The cacheable stand-ins for a diagnostic and its location.
/// </summary>
/// <remarks>
/// The generator reports every diagnostic at the identifier of a declaration, which always has a source tree,
/// so a diagnostic without a location is something only a direct call produces.
/// </remarks>
public sealed class DiagnosticInfoTests
{
    [Fact]
    public void A_diagnostic_without_a_source_location_is_reported_at_no_location()
    {
        var diagnostic = DiagnosticInfo.Create(DiagnosticDescriptors.MustBePartial, location: null, "Code").ToDiagnostic();

        diagnostic.Location.Should().Be(Location.None);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Contain("'Code'");
    }

    [Fact]
    public void A_location_outside_any_source_file_is_not_captured()
    {
        LocationInfo.From(null).Should().BeNull();
        LocationInfo.From(Location.None).Should().BeNull();
    }
}
