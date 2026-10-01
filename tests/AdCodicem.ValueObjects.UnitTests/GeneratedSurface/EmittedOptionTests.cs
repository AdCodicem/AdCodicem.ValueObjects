using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.Generated;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

/// <summary>
/// What the generator emits for an option or a hook only some value objects declare, each tested on the one that
/// declares it (docs/adr/0006-coverage-is-a-signal-not-a-goal.md).
/// </summary>
public class EmittedOptionTests
{
    [Fact]
    public void An_implicit_conversion_reads_the_carried_value()
    {
        string text = Iban.Create("FR7630006000011234567890189");
        ulong count = ByteCount.Create(42);

        text.Should().Be("FR7630006000011234567890189");
        count.Should().Be(42UL);
    }

    [Fact]
    public void An_explicit_conversion_builds_the_value_object()
    {
        ((SequenceNumber)7u).Should().Be(SequenceNumber.Create(7));
    }

    [Fact]
    public void A_type_that_allows_its_default_uses_it_without_a_suppression()
    {
        // AllowDefault silences VO0010, so the expression below compiles without a pragma: that is the test.
        var start = default(SequenceNumber);

        start.IsDefault.Should().BeTrue();
        start.Should().Be(SequenceNumber.Zero);
    }

    [Fact]
    public void An_open_value_set_names_its_known_values_and_accepts_others()
    {
        PageNumber.First.Value.Should().Be(1);
        PageNumber.TryCreate(7, out var seventh).Should().BeTrue();
        seventh.Value.Should().Be(7);
    }

    [Fact]
    public void A_formatter_that_outgrows_the_stack_buffer_is_handed_a_larger_one()
    {
        var label = Label.Create(new string('x', 100));

        label.ToString(Label.Wide, null).Should().Be(string.Concat(Enumerable.Repeat("x ", 100)));
    }

    [Fact]
    public void A_formatter_is_handed_a_buffer_as_large_as_its_text_needs()
    {
        var label = Label.Create(new string('x', 200));

        label.ToString(Label.Wider, null).Should().Be(string.Concat(Enumerable.Repeat("x   ", 200)));
    }

    [Fact]
    public void A_formatter_over_a_number_is_handed_a_larger_buffer_too()
    {
        Celsius.Create(21).ToString("D100", null).Should().Be(new string('0', 98) + "21");
    }

    /// <summary>
    /// A hook returns false only when the destination is too small. One that still does past a million characters
    /// never succeeds, whatever it is handed, and writing some other text in its place would hide that.
    /// </summary>
    [Fact]
    public void A_formatter_that_never_fits_makes_ToString_throw_rather_than_write_another_text()
    {
        var label = Label.Create("urgent");
        var temperature = Celsius.Create(21);

        label.Invoking(value => value.ToString(Label.Unbounded, null)).Should().Throw<FormatException>().WithMessage(
            "The formatting hook of Label wrote no text for the format 'U' in 1048576 characters. "
            + "TryFormatValue returns false only when the destination is too small.");
        temperature.Invoking(value => value.ToString("D2000000", null)).Should().Throw<FormatException>()
            .WithMessage("The formatting hook of Celsius wrote no text for the format 'D2000000' in 1048576 characters.*");
    }

    /// <summary>
    /// An IBAN's default format is its electronic form, the value it holds: the hook writes it unchanged, and
    /// <c>ToString()</c> hands back the string the value object holds rather than a copy, from the stack buffer or
    /// from a pooled one alike.
    /// </summary>
    [Fact]
    public void A_formatter_writing_a_string_unchanged_returns_the_string_held_without_allocating()
    {
        var iban = Iban.Create("FR7630006000011234567890189");
        var label = Label.Create(new string('x', 200));

        iban.ToString().Should().BeSameAs(iban.Value);
        iban.ToString(Iban.Formats.Electronic, null).Should().BeSameAs(iban.Value);
        label.ToString().Should().BeSameAs(label.Value);
        iban.ToString(Iban.Formats.Print, null).Should().Be("FR76 3000 6000 0112 3456 7890 189");

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = iban.ToString();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }

    [Fact]
    public void A_JSON_string_longer_than_the_stack_buffer_is_read_from_the_heap()
    {
        // Separators the normalizer strips, enough of them to leave the 512-character stack buffer behind.
        var json = JsonSerializer.Serialize("FR76" + new string(' ', 600) + "30006000011234567890189");

        JsonSerializer.Deserialize<Iban>(json).Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    [Fact]
    public void A_JSON_string_split_across_buffer_segments_is_read_whole()
    {
        // A reader fed from a pipe or a stream sees a string cut between two segments as a value sequence.
        var json = Encoding.UTF8.GetBytes("\"FR7630006000011234567890189\"");
        var reader = new Utf8JsonReader(Segments.Of(json.AsMemory(0, 10), json.AsMemory(10)));
        reader.Read();

        var converter = new Iban.ValueJsonConverter();

        converter.Read(ref reader, typeof(Iban), JsonSerializerOptions.Default)
            .Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    [Fact]
    public void An_explicit_format_still_wins_over_the_round_trip_form()
    {
        BirthDate.Create(new DateOnly(1980, 5, 17)).ToString("yyyy", CultureInfo.InvariantCulture).Should().Be("1980");
        OpeningTime.Create(new TimeOnly(9, 30)).ToString("HH", CultureInfo.InvariantCulture).Should().Be("09");
        RecordedAt.Create(new DateTime(2024, 6, 1)).ToString("yyyy", CultureInfo.InvariantCulture).Should().Be("2024");
        OccurredAt.Create(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero))
            .ToString("yyyy", CultureInfo.InvariantCulture).Should().Be("2024");
        Duration.Create(TimeSpan.FromMinutes(90)).ToString("g", CultureInfo.InvariantCulture).Should().Be("1:30:00");
    }

    [Fact]
    public void A_JSON_string_longer_than_the_stack_buffer_is_still_validated()
    {
        var json = JsonSerializer.Serialize(new string('A', 600));

        FluentActions.Invoking(() => JsonSerializer.Deserialize<Iban>(json))
            .Should().Throw<JsonException>().WithMessage("*not a valid Iban*");
    }

    [Fact]
    public void A_declared_maximum_length_is_checked_before_the_pattern()
    {
        var address = new string('a', 250) + "@example.com";

        EmailAddress.TryCreate(address, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);
    }

    [Fact]
    public void Arithmetic_that_overflows_the_underlying_type_throws_rather_than_wrapping()
    {
        var largest = ByteCount.Create(ulong.MaxValue);

        FluentActions.Invoking(() => largest + ByteCount.Create(1)).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => ByteCount.Sum([largest, ByteCount.Create(1)])).Should().Throw<OverflowException>();
        FluentActions.Invoking(() => FileSize.Create(long.MaxValue) * 2L).Should().Throw<OverflowException>();
    }

    [Fact]
    public void Registering_the_assembly_again_changes_nothing()
    {
        ValueObjectRegistration.RegisterAll();

        ValueObjectRegistry.TryGet(typeof(Label), out var descriptor).Should().BeTrue();
        descriptor!.Schema.MaxLength.Should().Be(200);
    }
}

/// <summary>Builds a <see cref="ReadOnlySequence{T}"/> out of separate segments, as a pipe hands them over.</summary>
internal sealed class Segments : ReadOnlySequenceSegment<byte>
{
    private Segments(ReadOnlyMemory<byte> memory, long runningIndex)
    {
        Memory = memory;
        RunningIndex = runningIndex;
    }

    public static ReadOnlySequence<byte> Of(ReadOnlyMemory<byte> first, ReadOnlyMemory<byte> second)
    {
        var head = new Segments(first, 0);
        var tail = new Segments(second, first.Length);
        head.Next = tail;

        return new ReadOnlySequence<byte>(head, 0, tail, second.Length);
    }
}
