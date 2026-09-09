namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// How the generator finds the rules a value object declares.
/// </summary>
/// <remarks>
/// Hooks are declared by implementing an interface, so the compiler checks their signature. What it cannot
/// check is that the author remembered to declare the interface at all, which is what the analyzer covers and
/// what these tests pin down.
/// </remarks>
public sealed class HookTests
{
    [Fact]
    public void Without_a_normalizer_the_generated_normalize_is_the_identity()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        // Identity matters for more than tidiness: it removes an allocation on every parse.
        run.SingleValueObject.Should().Contain("Normalize(global::System.String value) => value;");
    }

    [Fact]
    public void A_declared_normalizer_is_called_and_guarded_against_null()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectNormalizer<string>
            {
                public static string NormalizeValue(string value) => value.Trim();
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("value is null ? value : NormalizeValue(value)");
    }

    [Fact]
    public void A_span_normalizer_makes_parsing_skip_the_intermediate_string()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectNormalizer<string>, IValueObjectSpanNormalizer
            {
                public static string NormalizeValue(string value) => NormalizeValue(value.AsSpan());

                public static string NormalizeValue(ReadOnlySpan<char> value) => value.Trim().ToString();
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("TryCreateFrom(s, out result, out validation)");
        run.SingleValueObject.Should().NotContain("TryCreate(s.ToString(), out result, out validation)");
    }

    [Fact]
    public void Without_a_span_normalizer_parsing_materializes_the_text()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectNormalizer<string>
            {
                public static string NormalizeValue(string value) => value.Trim();
            }
            """);

        run.SingleValueObject.Should().Contain("TryCreate(s.ToString(), out result, out validation)");
        run.SingleValueObject.Should().NotContain("TryCreateFrom");
    }

    [Fact]
    public void A_declared_validator_is_called()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectValidator<string>
            {
                public static ValidationResult ValidateValue(in string value)
                    => value.Length > 2 ? ValidationResult.Success : ValidationResult.InvalidFormat("Too short.");
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("return ValidateValue(in value);");
    }

    [Fact]
    public void A_declared_formatter_takes_over_formatting()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectFormatter<string>
            {
                public static bool TryFormatValue(
                    in string value,
                    Span<char> destination,
                    out int charsWritten,
                    ReadOnlySpan<char> format,
                    IFormatProvider? provider)
                {
                    charsWritten = 0;
                    return true;
                }
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("TryFormatValue(in current,");
    }

    [Fact]
    public void A_rule_written_without_its_interface_is_ignored_by_the_generator()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code
            {
                public static string NormalizeValue(string value) => value.Trim();
            }
            """);

        // The member compiles and looks right, and the generator never calls it. That silence is the whole
        // reason the analyzer below exists.
        run.SingleValueObject.Should().Contain("Normalize(global::System.String value) => value;");
    }

    [Fact]
    public async Task The_analyzer_reports_a_rule_written_without_its_interface()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>("""
            [ValueObject<string>]
            public readonly partial struct Code
            {
                public static string NormalizeValue(string value) => value.Trim();
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("VO0011");
    }

    [Fact]
    public async Task The_analyzer_still_recognizes_the_names_hooks_used_to_carry()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>("""
            [ValueObject<string>]
            public readonly partial struct Code
            {
                private static string NormalizeCore(string value) => value.Trim();
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("VO0011");
    }

    [Fact]
    public async Task The_analyzer_says_nothing_when_the_interface_is_declared()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>("""
            [ValueObject<string>]
            public readonly partial struct Code : IValueObjectNormalizer<string>
            {
                public static string NormalizeValue(string value) => value.Trim();
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
