using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The CsvHelper package as packed, on CsvHelper 33.1 and the next major of .NET: each value object one column, written
/// as its own text and read through its rules, a refused field carrying the rule's code on CsvHelper's exception, and an
/// uninitialized instance its rules refuse never written.
/// </summary>
public sealed class CsvHelperTests
{
    private const string Header = "Iban,Quantity,Optional,Due,Country,Reference";

    private const string Row = "FR7630006000011234567890189,7,,2026-10-03,FR,PO-1042";

    [Fact]
    public void The_package_runs_on_CsvHelper_33()
        => typeof(CsvReader).Assembly.GetName().Version!.Major.Should().Be(33);

    [Fact]
    public void Value_objects_are_written_as_one_column_each_and_read_back_through_their_rules()
    {
        var shipment = new CsvShipment
        {
            Iban = Iban.Create("FR7630006000011234567890189"),
            Quantity = Quantity.Create(7),
            Due = BirthDate.Create(new DateOnly(2026, 10, 3)),
            Country = CountryCode.France,
            Reference = Reference<PurchaseOrder>.Create("PO-1042"),
        };

        Write(shipment).Should().Be($"{Header}\n{Row}\n");
        Read($"{Header}\n{Row}\n").Should().BeEquivalentTo(shipment);
        Read($"{Header}\nfr76 3000 6000 0112 3456 7890 189,7,3,2026-10-03,lu, po-1042 \n").Should().BeEquivalentTo(new CsvShipment
        {
            Iban = shipment.Iban,
            Quantity = shipment.Quantity,
            Optional = Quantity.Create(3),
            Due = shipment.Due,
            Country = CountryCode.Luxembourg,
            Reference = shipment.Reference,
        });
    }

    [Fact]
    public void A_refused_field_is_CsvHelper_exception_carrying_the_rule_code()
    {
        var refused = FluentActions.Invoking(static () => Read($"{Header}\nFR7630006000011234567890189,500,,2026-10-03,FR,PO-1042\n"))
            .Should().Throw<TypeConverterException>().Which;
        refused.Message.Should().StartWith("The field is not a valid Quantity: The value must be less than or equal to 100.");
        ValueObjectErrors.TryGetCode(refused, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        refused.Text.Should().Be("500");

        var empty = FluentActions.Invoking(static () => Read($"{Header}\n,7,,2026-10-03,FR,PO-1042\n"))
            .Should().Throw<TypeConverterException>().Which;
        empty.Message.Should().StartWith("An empty field cannot be read as Iban; declare the member as a nullable Iban? instead.");
        ValueObjectErrors.TryGetCode(empty, out var required).Should().BeTrue();
        required.Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void A_French_file_reads_and_writes_its_amounts_in_French()
    {
        var configuration = new CsvConfiguration(CultureInfo.GetCultureInfo("fr-FR")) { NewLine = "\n" };
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, configuration))
        {
            csv.Context.TypeConverterCache.AddValueObjects();
            csv.WriteRecords([new CsvPrice { Amount = Amount.Create(12.5m), Due = BirthDate.Create(new DateOnly(2026, 10, 3)) }]);
        }

        writer.ToString().Should().Be("Amount;Due\n12,50;2026-10-03\n");
        using var reader = new CsvReader(new StringReader("Amount;Due\n12,5;03/10/2026\n"), configuration);
        reader.Context.TypeConverterCache.AddValueObjects();
        reader.GetRecords<CsvPrice>().Single().Should().BeEquivalentTo(
            new CsvPrice { Amount = Amount.Create(12.5m), Due = BirthDate.Create(new DateOnly(2026, 10, 3)) });
    }

    [Fact]
    public void An_uninitialized_member_its_rules_refuse_is_not_written()
    {
        var refused = FluentActions.Invoking(static () => Write(new CsvShipment())).Should().Throw<TypeConverterException>().Which;

        refused.Message.Should().StartWith("The value to write is not a valid Iban: ");
        ValueObjectErrors.TryGetCode(refused, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.Required);
    }

    private static string Write(CsvShipment shipment)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { NewLine = "\n" }))
        {
            csv.Context.TypeConverterCache.AddValueObjects();
            csv.WriteRecords([shipment]);
        }

        return writer.ToString();
    }

    private static CsvShipment Read(string text)
    {
        using var csv = new CsvReader(new StringReader(text), CultureInfo.InvariantCulture);
        csv.Context.TypeConverterCache.AddValueObjects();
        return csv.GetRecords<CsvShipment>().Single();
    }

    /// <summary>A shipment, each value object in the column of its member's name.</summary>
    public sealed class CsvShipment
    {
        public Iban Iban { get; set; }

        public Quantity Quantity { get; set; }

        public Quantity? Optional { get; set; }

        public BirthDate Due { get; set; }

        public CountryCode Country { get; set; }

        public Reference<PurchaseOrder> Reference { get; set; }
    }

    /// <summary>A price and the day it is due.</summary>
    public sealed class CsvPrice
    {
        public Amount Amount { get; set; }

        public BirthDate Due { get; set; }
    }
}
