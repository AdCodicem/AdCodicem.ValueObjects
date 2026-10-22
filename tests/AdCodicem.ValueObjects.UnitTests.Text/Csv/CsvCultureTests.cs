extern alias csvhelper;

using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using GregorianCultures = csvhelper::AdCodicem.ValueObjects.Shared.GregorianCultures;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// The culture a field is read and written in: the configuration's, the type's or the member's, and a date always in the
/// Gregorian calendar, whatever the culture's own.
/// </summary>
public sealed class CsvCultureTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static TheoryData<string, bool> Calendars => new()
    {
        { "th-TH", true },
        { "th-TH", false },
        { "fa-IR", true },
        { "fa-IR", false },
        { "ar-SA", true },
        { "ar-SA", false },
    };

    [Fact]
    public void The_configuration_culture_reads_and_writes_every_field()
    {
        var configuration = CsvHarness.Configuration(French);

        CsvHarness.Write([CsvOrder.Valid], configuration).Should().Be(
            "Id;Iban;Quantity;Amount;Due;Country;Optional\n"
            + "0f8fad5b-d9cb-469f-a165-70867728950e;FR7630006000011234567890189;3;12,50;2026-10-03;FR;\n");

        var order = CsvHarness.Read<CsvOrder>(
            "Id;Iban;Quantity;Amount;Due;Country;Optional\n0f8fad5b-d9cb-469f-a165-70867728950e;FR7630006000011234567890189;3;12,5;03/10/2026;FR;\n",
            configuration).Single();
        order.Should().BeEquivalentTo(CsvOrder.Valid);
    }

    [Fact]
    public void A_culture_set_for_the_type_wins_over_the_configuration()
    {
        static void FrenchAmounts(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Amount>().CultureInfo = French;
        }

        CsvHarness.Read<CsvRow<Amount>>("Value\n\"12,5\"\n", setup: FrenchAmounts).Single().Value.Should().Be(Amount.Create(12.5m));
        CsvHarness.Write([new CsvRow<Amount> { Value = Amount.Create(12.5m) }], setup: FrenchAmounts).Should().Be("Value\n\"12,50\"\n");
    }

    [Fact]
    public void A_culture_set_for_the_member_wins_over_the_configuration_through_the_attribute_and_a_class_map()
    {
        var row = CsvHarness.Read<CsvCultured>("French,Other\n\"12,5\",3.5\n").Single();
        row.French.Should().Be(Amount.Create(12.5m));
        row.Other.Should().Be(Amount.Create(3.5m));
        CsvHarness.Write([row]).Should().Be("French,Other\n\"12,50\",3.50\n");

        var mapped = CsvHarness.Read<CsvCultured>("French,Other\n1.5,\"3,5\"\n", setup: static context =>
        {
            CsvHarness.Factory(context);
            var map = context.AutoMap<CsvCultured>();
            map.Map(cultured => cultured.French).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            map.Map(cultured => cultured.Other).TypeConverterOption.CultureInfo(French);
        }).Single();
        mapped.French.Should().Be(Amount.Create(1.5m));
        mapped.Other.Should().Be(Amount.Create(3.5m));
    }

    [Fact]
    public void Options_set_for_a_type_reach_a_member_of_that_exact_type_only_a_nullable_one_being_another()
    {
        const string Text = "Amount\n\"12,5\"\n";

        var refusal = CsvHarness.Refusal<CsvOptionalAmount>(Text, setup: static context =>
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Amount>().CultureInfo = French;
        });
        refusal.Code().Should().Be(ValueObjectErrorCodes.NotParsable);

        CsvHarness.Read<CsvOptionalAmount>(Text, setup: static context =>
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Amount?>().CultureInfo = French;
        }).Single().Amount.Should().Be(Amount.Create(12.5m));
    }

    [Theory]
    [MemberData(nameof(Calendars))]
    public void A_date_is_written_in_the_Gregorian_calendar_and_read_back_whatever_the_calendar_of_the_culture(string name, bool cached)
    {
        // A culture from GetCultureInfo is read-only, and its Gregorian copy kept; one built with new may still change, so
        // it is copied for each field.
        var culture = cached ? CultureInfo.GetCultureInfo(name) : new CultureInfo(name);
        culture.IsReadOnly.Should().Be(cached);
        culture.DateTimeFormat.Calendar.Should().NotBeOfType<GregorianCalendar>();
        var configuration = CsvHarness.Configuration(culture);
        var row = new CsvFamilies
        {
            Date = BirthDate.Create(new DateOnly(2026, 10, 3)),
            Recorded = RecordedAt.Create(new DateTime(2026, 10, 3, 14, 5, 6, 789, DateTimeKind.Utc)),
            Occurred = OccurredAt.Create(new DateTimeOffset(2026, 10, 3, 14, 5, 6, TimeSpan.FromHours(2))),
            Opening = OpeningTime.Create(new TimeOnly(8, 30)),
            Duration = Duration.Create(new TimeSpan(1, 30, 0)),
            Consent = Consent.Create(true),
            Latitude = Latitude.Create(48.85),
            Fingerprint = Fingerprint.Create(7),
            Celsius = Celsius.Create(21),
            Floor = Floor.Create(3),
            Label = Label.Create("Hello"),
        };

        var text = CsvHarness.Write([row], configuration);

        text.Should().Contain("2026-10-03").And.Contain("2026-10-03T14:05:06.7890000Z").And.Contain("2026-10-03T14:05:06.0000000+02:00");
        CsvHarness.Read<CsvFamilies>(text, configuration).Single().Should().BeEquivalentTo(row);
        CsvHarness.Read<CsvRow<BirthDate>>("Value\n2026-10-03\n", configuration).Single().Value.Should().Be(row.Date, "a year typed is a Gregorian one");
        CultureInfo.GetCultureInfo(name).DateTimeFormat.Calendar.Should().NotBeOfType<GregorianCalendar>("the culture itself is left as it was");
    }

    [Theory]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    public void The_Gregorian_copy_of_a_read_only_culture_is_kept_and_that_of_a_writable_one_made_each_time(string name)
    {
        var shared = CultureInfo.GetCultureInfo(name);
        var copy = GregorianCultures.Of(shared);

        copy.Should().BeSameAs(GregorianCultures.Of(shared), "one copy serves every field of every file");
        copy.Should().NotBeSameAs(shared);
        copy.IsReadOnly.Should().BeTrue("threads share it");
        copy.DateTimeFormat.Calendar.Should().BeOfType<GregorianCalendar>();
        copy.Name.Should().Be(name);

        var writable = new CultureInfo(name);
        GregorianCultures.Of(writable).Should().NotBeSameAs(GregorianCultures.Of(writable), "the application may still change it");
        GregorianCultures.Of(French).Should().BeSameAs(French, "its calendar is the Gregorian one already");
    }

    [Fact]
    public void Map_data_built_without_a_culture_reads_and_writes_in_the_invariant_culture()
    {
        var data = new MemberMapData(null);
        data.TypeConverterOptions.CultureInfo.Should().BeNull();
        var converter = new ValueObjectTypeConverter<Amount, decimal>();
        using var reader = new StringReader("12.5\n");
        using var csv = new CsvReader(reader, CsvHarness.Configuration(French) with { HasHeaderRecord = false });
        using var writer = new CsvWriter(new StringWriter(CultureInfo.InvariantCulture), CsvHarness.Configuration(French));
        csv.Read();

        converter.ConvertFromString("12.5", csv, data).Should().Be(Amount.Create(12.5m));
        converter.ConvertToString(Amount.Create(12.5m), writer, data).Should().Be("12.50");
    }
}
