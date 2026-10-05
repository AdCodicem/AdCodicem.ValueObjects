using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// That the incremental generator is actually incremental.
/// </summary>
/// <remarks>
/// Nothing fails when a generator loses its caching: the output stays correct and every keystroke in the IDE
/// re-runs the whole pipeline. The only way to notice is to assert on the run reasons, which is what these do.
/// </remarks>
public sealed class IncrementalityTests
{
    private const string ValueObject = """
        using System;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;

        namespace Test;

        [ValueObject<string>(MaxLength = 8)]
        public readonly partial struct Code;
        """;

    private const string Unrelated = """

        // A class the value object knows nothing about.
        public sealed class Unrelated
        {
            public int Value { get; set; }
        }
        """;

    private const string KnownValues = """
        using System;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;

        namespace Test;

        [ValueObject<string>(ValueSet = ValueSetKind.Closed, MaxLength = 2)]
        public readonly partial struct Country
        {
            [KnownValue]
            public static readonly Country France = Known("FR");

            [KnownValue]
            public static readonly Country Belgium = Known("BE");
        }
        """;

    [Fact]
    public void An_unrelated_edit_does_not_re_run_the_model()
    {
        var reasons = GeneratorHarness.RunTwice(ValueObject, ValueObject + Unrelated, "ValueObjects");

        // Cached or Unchanged both mean the emitter did not run again, which is the property that matters.
        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Recompiling_identical_source_does_not_re_run_the_model()
    {
        var reasons = GeneratorHarness.RunTwice(ValueObject, ValueObject, "ValueObjects");

        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    /// <summary>
    /// The model of a value object that carries containing types, known values or diagnostics holds arrays of
    /// them, which must compare by content: compared by reference, every edit would re-run the emitter.
    /// </summary>
    [Theory]
    [InlineData(KnownValues)]
    [InlineData("""
        using System;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;

        namespace Test;

        public static partial class Outer
        {
            public static partial class Inner
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
        }
        """)]
    [InlineData("""
        using System;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;

        namespace Test;

        [ValueObject<string>(Arithmetic = true)]
        public readonly partial struct Code;
        """)]
    public void An_unrelated_edit_does_not_re_run_a_model_holding_arrays(string source)
    {
        var reasons = GeneratorHarness.RunTwice(source, source + Unrelated, "ValueObjects");

        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Theory]
    [InlineData("""Belgium = Known("BE");""", """Belgium = Known("BE"); [KnownValue] public static readonly Country Spain = Known("ES");""")]
    [InlineData("""Belgium = Known("BE");""", """Kingdom = Known("BE");""")]
    [InlineData(
        "[KnownValue]\n    public static readonly Country Belgium",
        "[KnownValue(Description = \"The Kingdom of Belgium.\")]\n    public static readonly Country Belgium")]
    [InlineData(
        "[KnownValue]\n    public static readonly Country Belgium",
        "/// <summary>The Kingdom of Belgium.</summary>\n    [KnownValue]\n    public static readonly Country Belgium")]
    [InlineData("""Belgium = Known("BE");""", """Belgium = Known("BELGIUM");""")]
    public void Adding_or_changing_a_known_value_does_re_run_the_model(string declared, string edited)
    {
        var source = KnownValues.Replace("\r\n", "\n", StringComparison.Ordinal);
        source.Should().Contain(declared);

        var reasons = GeneratorHarness.RunTwice(source, source.Replace(declared, edited, StringComparison.Ordinal), "ValueObjects");

        reasons.Should().Contain(IncrementalStepRunReason.Modified);
    }

    /// <summary>
    /// The generated code reads the value of a known value at run time, off the member, so changing it changes nothing
    /// the generator writes, and the model is not re-run, unless the generator now refuses the value at compile time.
    /// </summary>
    [Fact]
    public void Changing_the_value_of_a_known_value_does_not_re_run_the_model()
    {
        var reasons = GeneratorHarness.RunTwice(
            KnownValues,
            KnownValues.Replace("""Known("BE")""", """Known("BX")""", StringComparison.Ordinal),
            "ValueObjects");

        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Changing_a_declared_constraint_does_re_run_the_model()
    {
        var edited = ValueObject.Replace("MaxLength = 8", "MaxLength = 16", StringComparison.Ordinal);

        var reasons = GeneratorHarness.RunTwice(ValueObject, edited, "ValueObjects");

        // The complement of the tests above: caching must not be so eager that a real change is missed.
        reasons.Should().Contain(IncrementalStepRunReason.Modified);
    }
}
