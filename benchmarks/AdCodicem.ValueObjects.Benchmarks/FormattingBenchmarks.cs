using AdCodicem.ValueObjects.Benchmarks.Domain;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// What a formatting hook costs, written into a span or returned as a string.
/// </summary>
/// <remarks>
/// <see cref="Bban"/> and <see cref="StringFormattedBban"/> apply the same masking rule and must print the same
/// text, which the setup asserts, so any difference between their rows is the shape of the hook. The default
/// format writes the value as it is, which is where <c>ToString</c> can hand back the string the value object
/// already holds.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
public class FormattingBenchmarks
{
    private const string Account = "30006000011234567890189";

    // Pinned. With an unpinned buffer, identical machine code for a TryFormat row measured 4 ns in one build and
    // 17 ns in another; a pinned one measured the same in both, whatever the offset into it.
    private readonly char[] _destination = GC.AllocateUninitializedArray<char>(64, pinned: true);
    private readonly Bban _span = Bban.Create(Account);
    private readonly StringFormattedBban _string = StringFormattedBban.Create(Account);

    /// <summary>Formats with both hooks once and checks they agree.</summary>
    [GlobalSetup]
    public void Setup()
    {
        Agree(_span.ToString(), _string.ToString());
        Agree(_span.ToString("M", null), _string.ToString("M", null));
    }

    /// <summary>The default format through a span hook, which writes the value as it is.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "Span hook, ToString()")]
    public string Span_ToString() => _span.ToString();

    /// <summary>A named format through a span hook, which needs a new string.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "Span hook, ToString(\"M\")")]
    public string Span_ToStringMasked() => _span.ToString("M", null);

    /// <summary>The same format written into a caller's buffer, as interpolation does.</summary>
    /// <returns>The number of characters written.</returns>
    [Benchmark(Description = "Span hook, TryFormat(\"M\")")]
    public int Span_TryFormatMasked() => _span.TryFormat(_destination, out var written, "M", null) ? written : -1;

    /// <summary>The default format through a string hook, which returns the value as it is.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "String hook, ToString()")]
    public string String_ToString() => _string.ToString();

    /// <summary>A named format through a string hook.</summary>
    /// <returns>The text.</returns>
    [Benchmark(Description = "String hook, ToString(\"M\")")]
    public string String_ToStringMasked() => _string.ToString("M", null);

    /// <summary>The same format written into a caller's buffer, which copies the string the hook returns.</summary>
    /// <returns>The number of characters written.</returns>
    [Benchmark(Description = "String hook, TryFormat(\"M\")")]
    public int String_TryFormatMasked() => _string.TryFormat(_destination, out var written, "M", null) ? written : -1;

    private static void Agree(string span, string text)
    {
        if (!string.Equals(span, text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Hooks differ: '{span}' against '{text}'.");
        }
    }
}
