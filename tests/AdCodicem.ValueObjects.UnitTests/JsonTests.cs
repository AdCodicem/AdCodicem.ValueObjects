using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.UnitTests;

public partial class JsonTests
{
    private static readonly JsonSerializerOptions Default = new();

    private static readonly JsonSerializerOptions NumbersFromStrings =
        new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    private sealed record Payment(Iban Account, Amount Total, CustomerId Customer, BirthDate? Birth, Quantity Lines);

    [Fact]
    public void A_value_object_serializes_as_its_bare_underlying_value()
    {
        var payment = new Payment(
            Iban.Create("FR7630006000011234567890189"),
            Amount.Create(1250m),
            CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")),
            BirthDate.Create(new DateOnly(1980, 5, 17)),
            Quantity.Create(3));

        var json = JsonSerializer.Serialize(payment);

        json.Should().Be(
            """
            {"Account":"FR7630006000011234567890189","Total":1250.00,"Customer":"0192f4a0-0000-7000-8000-000000000001","Birth":"1980-05-17","Lines":3}
            """);
    }

    [Fact]
    public void A_value_object_deserializes_from_its_bare_underlying_value()
    {
        const string json =
            """
            {"Account":"fr76 3000 6000 0112 3456 7890 189","Total":1250,"Customer":"0192f4a0-0000-7000-8000-000000000001","Birth":null,"Lines":3}
            """;

        var payment = JsonSerializer.Deserialize<Payment>(json)!;

        payment.Account.Value.Should().Be("FR7630006000011234567890189");
        payment.Total.Should().Be(Amount.Create(1250m));
        payment.Birth.Should().BeNull();
        payment.Lines.Value.Should().Be(3);
    }

    [Fact]
    public void No_registration_is_required_for_the_converter_to_apply()
    {
        // The converter is attached to the type itself, so a bare JsonSerializerOptions is enough.
        JsonSerializer.Serialize(Amount.Create(9.99m), Default).Should().Be("9.99");
    }

    [Fact]
    public void A_rejected_value_surfaces_as_a_JsonException_carrying_the_rule()
    {
        var act = () => JsonSerializer.Deserialize<Iban>("\"FR7630006000011234567890188\"");

        act.Should().Throw<JsonException>().WithMessage("*check digits*");
    }

    [Fact]
    public void A_null_is_rejected_for_a_non_optional_value_object()
    {
        var act = () => JsonSerializer.Deserialize<Iban>("null");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void A_null_is_accepted_for_an_optional_value_object()
    {
        JsonSerializer.Deserialize<Iban?>("null").Should().BeNull();
    }

    [Fact]
    public void A_wrong_token_type_is_reported_rather_than_coerced()
    {
        var act = () => JsonSerializer.Deserialize<Amount>("\"1250\"");

        act.Should().Throw<JsonException>().WithMessage("*number*");
    }

    [Fact]
    public void Reading_a_number_from_a_string_is_honoured_when_the_options_allow_it()
    {
        JsonSerializer.Deserialize<Amount>("\"1250.50\"", NumbersFromStrings).Should().Be(Amount.Create(1250.50m));
    }

    [Fact]
    public void A_value_object_works_as_a_dictionary_key()
    {
        var balances = new Dictionary<CountryCode, Amount>
        {
            [CountryCode.France] = Amount.Create(10m),
            [CountryCode.Belgium] = Amount.Create(20m),
        };

        var json = JsonSerializer.Serialize(balances);
        json.Should().Be("""{"FR":10.00,"BE":20.00}""");

        JsonSerializer.Deserialize<Dictionary<CountryCode, Amount>>(json)!
            .Should().ContainKey(CountryCode.Belgium);
    }

    [Fact]
    public void A_guid_keyed_dictionary_round_trips_through_the_property_name_converter()
    {
        var customer = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));
        var source = new Dictionary<CustomerId, Amount> { [customer] = Amount.Create(1m) };

        var json = JsonSerializer.Serialize(source);

        json.Should().Be("""{"0192f4a0-0000-7000-8000-000000000001":1.00}""");
        JsonSerializer.Deserialize<Dictionary<CustomerId, Amount>>(json)!.Should().ContainKey(customer);
    }

}
