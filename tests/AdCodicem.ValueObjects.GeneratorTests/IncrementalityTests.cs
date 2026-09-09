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

    [Fact]
    public void An_unrelated_edit_does_not_re_run_the_model()
    {
        var edited = ValueObject + """

            // A class the value object knows nothing about.
            public sealed class Unrelated
            {
                public int Value { get; set; }
            }
            """;

        var reasons = GeneratorHarness.RunTwice(ValueObject, edited, "ValueObjects");

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

    [Fact]
    public void Changing_a_declared_constraint_does_re_run_the_model()
    {
        var edited = ValueObject.Replace("MaxLength = 8", "MaxLength = 16", StringComparison.Ordinal);

        var reasons = GeneratorHarness.RunTwice(ValueObject, edited, "ValueObjects");

        // The complement of the tests above: caching must not be so eager that a real change is missed.
        reasons.Should().Contain(IncrementalStepRunReason.Modified);
    }
}
