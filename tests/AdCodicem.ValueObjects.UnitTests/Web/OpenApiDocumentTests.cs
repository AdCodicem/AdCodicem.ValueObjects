using System.Text.Json;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What the OpenAPI document says of each value object, read from a document ASP.NET Core built for the domain.
/// </summary>
/// <param name="document">The document.</param>
public class OpenApiDocumentTests(OpenApiDocument document) : IClassFixture<OpenApiDocument>
{
    /// <summary>The members of each closed value set of the domain, keyed by the name of the type.</summary>
    private static readonly Dictionary<string, object[]> ClosedSets = new()
    {
        [nameof(CountryCode)] = [CountryCode.France, CountryCode.Belgium, CountryCode.Luxembourg],
        [nameof(DocumentStatus)] = [DocumentStatus.Draft, DocumentStatus.Final],
        [nameof(Priority)] = [Priority.Low, Priority.High],
        [nameof(StorageQuota)] = [StorageQuota.Large],
        [nameof(VatRate)] = [VatRate.Standard, VatRate.Reduced],
        [nameof(VoteWeight)] = [VoteWeight.Half, VoteWeight.Full],
        [nameof(Opacity)] = [Opacity.Translucent],
        [nameof(TermsAccepted)] = [TermsAccepted.Accepted],
        [nameof(HttpStatus)] = [HttpStatus.Ok, HttpStatus.NotFound],
        [nameof(BlockSize)] = [BlockSize.Small, BlockSize.Large],
        [nameof(CutOffDate)] = [CutOffDate.Epoch],
        [nameof(ShiftStart)] = [ShiftStart.Early, ShiftStart.Late],
        [nameof(LaunchMoment)] = [LaunchMoment.Launch],
        [nameof(Answer)] = [Answer.Yes, Answer.No],
    };

    public static TheoryData<string> EveryClosedSet => [.. ClosedSets.Keys];

    /// <summary>
    /// A client validates a payload against the listed values, so each one must be the very JSON the type writes:
    /// a number of any width as a number, a date or a time in the round-trip form its converter writes.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryClosedSet))]
    public void A_closed_value_set_lists_its_values_as_the_type_writes_them(string name)
    {
        var listed = document.Schema(name).GetProperty("enum").EnumerateArray().Select(value => value.GetRawText());

        listed.Should().Equal(ClosedSets[name].Select(value => JsonSerializer.Serialize(value, value.GetType())));
    }

    [Fact]
    public void A_closed_set_of_narrow_integers_lists_numbers_and_one_of_dates_lists_their_ISO_form()
    {
        document.Schema(nameof(HttpStatus)).GetProperty("enum").EnumerateArray().Select(value => value.ValueKind)
            .Should().AllBeEquivalentTo(JsonValueKind.Number);
        document.Schema(nameof(HttpStatus)).GetProperty("enum")[1].GetInt16().Should().Be(404);
        document.Schema(nameof(CutOffDate)).GetProperty("enum")[0].GetString().Should().Be("2000-01-01");
    }

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
