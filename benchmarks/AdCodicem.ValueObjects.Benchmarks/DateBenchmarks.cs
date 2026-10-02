using System.Globalization;
using AdCodicem.ValueObjects.Benchmarks.Domain;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// What a date value object costs to format and parse in its round-trip form, against the bare underlying type.
/// </summary>
/// <remarks>
/// A date value object formats in the round-trip form <c>O</c> by default and parses that form back, so the raw
/// rows do exactly that by hand: a <see cref="DateTime"/> keeps its kind through
/// <see cref="DateTimeStyles.RoundtripKind"/>, a <see cref="DateTimeOffset"/> its offset with no style needed. The
/// value objects must print the same text as the raw rows, which the setup asserts. Neither declares a rule, so
/// the rows measure the wrapper and nothing it validates.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
public class DateBenchmarks
{
    private static readonly DateTime Moment = new(2026, 10, 1, 12, 37, 18, 123, DateTimeKind.Utc);
    private static readonly DateTimeOffset Instant = new(2026, 10, 1, 14, 37, 18, 123, TimeSpan.FromHours(2));

    // Pinned for the reason given in FormattingBenchmarks.
    private readonly char[] _destination = GC.AllocateUninitializedArray<char>(64, pinned: true);
    private readonly RecordedAt _recordedAt = RecordedAt.Create(Moment);
    private readonly OccurredAt _occurredAt = OccurredAt.Create(Instant);
    private string _momentText = string.Empty;
    private string _instantText = string.Empty;

    /// <summary>Formats both shapes of each type once and checks they agree.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _momentText = Moment.ToString("O", CultureInfo.InvariantCulture);
        _instantText = Instant.ToString("O", CultureInfo.InvariantCulture);

        Agree(_momentText, _recordedAt.ToString());
        Agree(_instantText, _occurredAt.ToString());
    }

    /// <summary>The baseline: the bare <see cref="DateTime"/>, in its round-trip form.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Baseline = true, Description = "Format raw DateTime")]
    public string FormatDateTime_Raw() => Moment.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>The <see cref="DateTime"/> value object's default format.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "Format DateTime value object")]
    public string FormatDateTime_Typed() => _recordedAt.ToString();

    /// <summary>The bare <see cref="DateTime"/> read back with its kind.</summary>
    /// <returns>The instant.</returns>
    [Benchmark(Description = "Parse raw DateTime")]
    public DateTime ParseDateTime_Raw()
        => DateTime.Parse(_momentText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>The <see cref="DateTime"/> value object read back from the same text.</summary>
    /// <returns>The value object.</returns>
    [Benchmark(Description = "Parse DateTime value object")]
    public RecordedAt ParseDateTime_Typed() => RecordedAt.Parse(_momentText, CultureInfo.InvariantCulture);

    /// <summary>The bare <see cref="DateTimeOffset"/>, in its round-trip form.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "Format raw DateTimeOffset")]
    public string FormatDateTimeOffset_Raw() => Instant.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>The <see cref="DateTimeOffset"/> value object's default format.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "Format DateTimeOffset value object")]
    public string FormatDateTimeOffset_Typed() => _occurredAt.ToString();

    /// <summary>The <see cref="DateTimeOffset"/> value object written into a caller's buffer.</summary>
    /// <returns>The number of characters written.</returns>
    [Benchmark(Description = "TryFormat DateTimeOffset value object")]
    public int TryFormatDateTimeOffset_Typed()
        => _occurredAt.TryFormat(_destination, out var written, default, null) ? written : -1;

    /// <summary>The bare <see cref="DateTimeOffset"/> read back, offset included.</summary>
    /// <returns>The instant.</returns>
    [Benchmark(Description = "Parse raw DateTimeOffset")]
    public DateTimeOffset ParseDateTimeOffset_Raw() => DateTimeOffset.Parse(_instantText, CultureInfo.InvariantCulture);

    /// <summary>The <see cref="DateTimeOffset"/> value object read back from the same text.</summary>
    /// <returns>The value object.</returns>
    [Benchmark(Description = "Parse DateTimeOffset value object")]
    public OccurredAt ParseDateTimeOffset_Typed() => OccurredAt.Parse(_instantText, CultureInfo.InvariantCulture);

    private static void Agree(string raw, string typed)
    {
        if (!string.Equals(raw, typed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Texts differ: '{raw}' against '{typed}'.");
        }
    }
}
