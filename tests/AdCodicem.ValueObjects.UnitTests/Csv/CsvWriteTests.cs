using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using AdCodicem.ValueObjects.UnitTests.TestData;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// Value objects written as one CSV column each, in their own text, which their parser reads back; an uninitialized
/// instance their rules refuse is never written.
/// </summary>
public sealed class CsvWriteTests
{
    private static readonly CsvFamilies Families = new()
    {
        Date = BirthDate.Create(new DateOnly(2026, 10, 3)),
        Recorded = RecordedAt.Create(new DateTime(2026, 10, 3, 14, 5, 6, 789, DateTimeKind.Utc)),
        Occurred = OccurredAt.Create(new DateTimeOffset(2026, 10, 3, 14, 5, 6, TimeSpan.FromHours(2))),
        Opening = OpeningTime.Create(new TimeOnly(8, 30)),
        Duration = Duration.Create(new TimeSpan(1, 30, 0)),
        Consent = Consent.Create(true),
        Latitude = Latitude.Create(48.85),
        Fingerprint = Fingerprint.Create(UInt128.MaxValue),
        Celsius = Celsius.Create(21),
        Floor = Floor.Create(3),
        Label = Label.Create("Hello"),
    };

    [Fact]
    public void An_order_is_written_with_its_members_names_as_the_header_and_one_column_per_value_object()
        => CsvHarness.Write([CsvOrder.Valid]).Should().Be(CsvOrder.Header + "\n" + CsvOrder.ValidRow + "\n");

    [Fact]
    public void Each_family_is_written_as_its_own_round_trip_text_and_read_back()
    {
        var text = CsvHarness.Write([Families]);

        text.Should().Be(
            "Date,Recorded,Occurred,Opening,Duration,Consent,Latitude,Fingerprint,Celsius,Floor,Label\n"
            + "2026-10-03,2026-10-03T14:05:06.7890000Z,2026-10-03T14:05:06.0000000+02:00,08:30:00.0000000,01:30:00,True,48.85,"
            + "340282366920938463463374607431768211455,21,3,Hello\n");
        var read = CsvHarness.Read<CsvFamilies>(text).Single();
        read.Should().BeEquivalentTo(Families);
        read.Recorded.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void A_value_object_whose_formatting_hook_writes_text_its_parser_refuses_is_written_as_its_underlying_value()
    {
        Celsius.Create(21).ToString().Should().Be("21 °C");
        Floor.Create(3).ToString().Should().Be("floor 3");

        CsvHarness.Write([new CsvRow<Celsius> { Value = Celsius.Create(21) }]).Should().Be("Value\n21\n");
        CsvHarness.Write([new CsvRow<Floor> { Value = Floor.Create(3) }]).Should().Be("Value\n3\n");
    }

    [Fact]
    public void A_nullable_member_holding_nothing_keeps_its_column_as_empty_text_or_its_first_null_value()
    {
        var text = CsvHarness.Write([CsvOrder.Valid]);
        CsvHarness.Read<CsvOrder>(text).Single().Optional.Should().BeNull("the column stays, so the row reads back");

        static void Nulls(CsvContext context)
        {
            CsvHarness.Factory(context);
            context.TypeConverterOptionsCache.GetOptions<Quantity?>().NullValues.Add("NULL");
        }

        var nulls = CsvHarness.Write([CsvOrder.Valid], setup: Nulls);
        nulls.Should().Be(CsvOrder.Header + "\n" + CsvOrder.ValidRow + "NULL\n");
        CsvHarness.Read<CsvOrder>(nulls, setup: Nulls).Single().Optional.Should().BeNull();
    }

    [Fact]
    public void An_uninitialized_instance_its_rules_refuse_is_not_written()
    {
        var order = CsvOrder.Valid;
#pragma warning disable VO0010 // An account nobody filled in, as a record built by hand may hold.
        order.Iban = default;
#pragma warning restore VO0010
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        var refusal = FluentActions.Invoking(() =>
            {
                using var csv = new CsvWriter(writer, CsvHarness.Configuration());
                csv.Context.TypeConverterCache.AddValueObjects();
                csv.WriteRecords([order]);
            })
            .Should().Throw<TypeConverterException>().Which;

        refusal.Code().Should().Be(ValueObjectErrorCodes.Required);
        refusal.FirstLine().Should().Be("The value to write is not a valid Iban: The value must not be empty.");
        refusal.Value.Should().BeOfType<Iban>().Which.IsUninitialized().Should().BeTrue();
        refusal.MemberMapData.Member!.Name.Should().Be(nameof(CsvOrder.Iban));
        writer.ToString().Should().Be(
            CsvOrder.Header + "\n0f8fad5b-d9cb-469f-a165-70867728950e",
            "CsvHelper writes field by field into a buffer its writer flushes when disposed");
    }

    [Fact]
    public void An_uninitialized_instance_its_rules_accept_is_written_as_its_value()
    {
        CsvHarness.Write([new CsvRow<Latitude>()]).Should().Be("Value\n0\n");
        CsvHarness.Write([new CsvRow<Consent>()]).Should().Be("Value\nFalse\n");
        CsvHarness.Write([new CsvRow<Quantity>()]).Should().Be("Value\n0\n");
    }

    [Fact]
    public void An_uninitialized_instance_refused_with_an_empty_message_is_named_without_one()
    {
        var refusal = FluentActions.Invoking(static () => CsvHarness.Write([new CsvRow<Declared<CsvSilent, string>>()]))
            .Should().Throw<TypeConverterException>().Which;

        refusal.Code().Should().Be(CsvSilent.Refused);
        refusal.FirstLine().Should().Be("The value to write is not a valid Declared<CsvSilent, string>.");
    }

    [Fact]
    public void WriteField_writes_a_value_object_through_the_factory_and_refuses_an_uninitialized_one()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, CsvHarness.Configuration()))
        {
            csv.Context.TypeConverterCache.AddValueObjects();
            csv.WriteField(Iban.Create("FR7630006000011234567890189"));
            csv.WriteField<Quantity?>(Quantity.Create(3));
            csv.WriteField(PageNumber.Create(2));
#pragma warning disable VO0010 // A page number nobody filled in, which its minimum refuses.
            FluentActions.Invoking(() => csv.WriteField(default(PageNumber))).Should().Throw<TypeConverterException>()
#pragma warning restore VO0010
                .Which.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
            csv.NextRecord();
        }

        writer.ToString().Should().Be("FR7630006000011234567890189,3,2\n");
    }

    [Fact]
    public async Task WriteRecordsAsync_writes_and_refuses_as_WriteRecords_does()
    {
        await using var writer = new StringWriter(CultureInfo.InvariantCulture);
        await using (var csv = new CsvWriter(writer, CsvHarness.Configuration()))
        {
            csv.Context.TypeConverterCache.AddValueObjects();
            await csv.WriteRecordsAsync([CsvOrder.Valid], TestContext.Current.CancellationToken);
        }

        writer.ToString().Should().Be(CsvOrder.Header + "\n" + CsvOrder.ValidRow + "\n");

        await using var refusedWriter = new StringWriter(CultureInfo.InvariantCulture);
        await using var refused = new CsvWriter(refusedWriter, CsvHarness.Configuration());
        refused.Context.TypeConverterCache.AddValueObjects();
        await FluentActions.Awaiting(async () => await refused.WriteRecordsAsync([new CsvOrder()], TestContext.Current.CancellationToken))
            .Should().ThrowAsync<TypeConverterException>().Where(static refusal => refusal.Code() == ValueObjectErrorCodes.Required);
    }
}
