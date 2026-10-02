using AdCodicem.ValueObjects.Benchmarks.Domain;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;

namespace AdCodicem.ValueObjects.Benchmarks;

/// <summary>
/// What checking the shape of a value costs: the deprecated <c>Pattern</c> option, the pattern hook with a
/// source-generated regular expression, the same shape checked by hand, and no check at all.
/// </summary>
/// <remarks>
/// Run under the JIT and under native AOT (<c>--runtimes net10.0 nativeaot10.0</c>): the option compiles its
/// regular expression at run time, which native AOT interprets, while the hook's is compiled when the type is.
/// Every variant accepts the same input and rejects the same malformed one, which the setup asserts.
/// </remarks>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Declared)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PatternBenchmarks
{
    private const string Account = "FR76 3000 6000 0112 3456 7890 189";
    private const string Normalized = "FR7630006000011234567890189";
    private const string Code = "75008";

    // A digit where the country code goes, which the shape refuses whichever way it is checked.
    private const string Malformed = "1R7630006000011234567890189";

    /// <summary>Checks that every variant agrees on what it accepts and rejects.</summary>
    [GlobalSetup]
    public void Setup()
    {
        Agree(Iban.TryCreate(Account, out _), IbanByPattern.TryCreate(Account, out _), IbanByHand.TryCreate(Account, out _), true);
        Agree(Iban.TryCreate(Malformed, out _), IbanByPattern.TryCreate(Malformed, out _), IbanByHand.TryCreate(Malformed, out _), false);
        Agree(PostalCode.TryCreate(Code, out _), PostalCodeByPattern.TryCreate(Code, out _), PostalCodeByHand.TryCreate(Code, out _), true);
        Agree(PostalCode.TryCreate("7500A", out _), PostalCodeByPattern.TryCreate("7500A", out _), PostalCodeByHand.TryCreate("7500A", out _), false);
    }

    /// <summary>The IBAN through the deprecated option.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Baseline = true, Description = "IBAN, Pattern option"), BenchmarkCategory("IBAN")]
    public bool Iban_Option() => IbanByPattern.TryCreate(Account, out _);

    /// <summary>The IBAN through the hook.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Description = "IBAN, pattern hook"), BenchmarkCategory("IBAN")]
    public bool Iban_Hook() => Iban.TryCreate(Account, out _);

    /// <summary>The IBAN, its shape checked by hand.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Description = "IBAN, checked by hand"), BenchmarkCategory("IBAN")]
    public bool Iban_Hand() => IbanByHand.TryCreate(Account, out _);

    /// <summary>The IBAN, its shape not checked.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Description = "IBAN, check digits only"), BenchmarkCategory("IBAN")]
    public bool Iban_CheckDigitsOnly() => IbanCheckDigitsOnly.TryCreate(Account, out _);

    /// <summary>The postal code through the deprecated option.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Baseline = true, Description = "Postal code, Pattern option"), BenchmarkCategory("Postal")]
    public bool Postal_Option() => PostalCodeByPattern.TryCreate(Code, out _);

    /// <summary>The postal code through the hook.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Description = "Postal code, pattern hook"), BenchmarkCategory("Postal")]
    public bool Postal_Hook() => PostalCode.TryCreate(Code, out _);

    /// <summary>The postal code, its digits checked by hand.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Description = "Postal code, checked by hand"), BenchmarkCategory("Postal")]
    public bool Postal_Hand() => PostalCodeByHand.TryCreate(Code, out _);

    /// <summary>The postal code, its length only.</summary>
    /// <returns>Whether it was accepted.</returns>
    [Benchmark(Description = "Postal code, lengths only"), BenchmarkCategory("Postal")]
    public bool Postal_LengthsOnly() => PostalCodeLengthsOnly.TryCreate(Code, out _);

    /// <summary>The engine alone, as the deprecated option compiles it.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark(Baseline = true, Description = "IBAN shape, compiled Regex"), BenchmarkCategory("Engine")]
    public bool Engine_Compiled() => Shapes.CompiledIban.IsMatch(Normalized);

    /// <summary>The engine alone, as the hook's is generated.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark(Description = "IBAN shape, [GeneratedRegex]"), BenchmarkCategory("Engine")]
    public bool Engine_Generated() => Shapes.GeneratedIban.IsMatch(Normalized);

    /// <summary>The same shape checked by hand.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark(Description = "IBAN shape, by hand"), BenchmarkCategory("Engine")]
    public bool Engine_Hand() => Shapes.IsIban(Normalized);

    private static void Agree(bool hook, bool option, bool hand, bool expected)
    {
        if (hook != expected || option != expected || hand != expected)
        {
            throw new InvalidOperationException($"The variants disagree: hook {hook}, option {option}, hand {hand}.");
        }
    }
}
