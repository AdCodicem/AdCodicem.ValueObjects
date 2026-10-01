using System.Text.Json;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What the OpenAPI document says of each value object, read from a document ASP.NET Core built for the domain.
/// </summary>
/// <param name="document">The document.</param>
public class OpenApiDocumentTests(OpenApiDocument document) : IClassFixture<OpenApiDocument>
{
    [Fact]
    public void The_declared_bounds_of_a_number_are_published_as_numbers()
    {
        document.Schema(nameof(Percentage)).GetProperty("minimum").GetDecimal().Should().Be(0m);
        document.Schema(nameof(Percentage)).GetProperty("maximum").GetDecimal().Should().Be(100m);
        document.Schema(nameof(Quantity)).GetProperty("maximum").GetInt32().Should().Be(1000);
        document.Schema(nameof(Latitude)).GetProperty("minimum").GetDouble().Should().Be(-90d);
    }

    /// <summary>
    /// The attribute reads a decimal, double or float bound as a floating-point literal, exponent included, and the
    /// type enforces what it read. A double's bound may also lie beyond the range of decimal or below its
    /// precision, where reading it as a decimal would publish another value or none.
    /// </summary>
    [Fact]
    public void A_bound_written_with_an_exponent_is_published_as_the_number_the_type_enforces()
    {
        document.Schema(nameof(Mass)).GetProperty("minimum").GetDouble().Should().Be(9.1e-31);
        document.Schema(nameof(Mass)).GetProperty("maximum").GetDouble().Should().Be(2e32);
        document.Schema(nameof(TransferLimit)).GetProperty("maximum").GetDecimal().Should().Be(1_000_000m);
        document.Schema(nameof(Luminance)).GetProperty("maximum").GetDouble().Should().Be(1500d);
    }

    [Fact]
    public void A_bound_that_is_no_number_is_left_out()
    {
        var born = document.Schema(nameof(BirthDate));

        born.TryGetProperty("minimum", out _).Should().BeFalse("a date is a string on the wire");
        born.TryGetProperty("maximum", out _).Should().BeFalse("a date is a string on the wire");
    }
}
