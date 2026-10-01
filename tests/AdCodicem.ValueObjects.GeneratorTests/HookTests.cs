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

    /// <summary>
    /// The hook answers every formatting member, and the JSON converter alone formats the underlying value itself,
    /// for a dictionary key, which a formatting hook never writes.
    /// </summary>
    [Fact]
    public void A_declared_string_formatter_takes_over_formatting()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Floor : IValueObjectStringFormatter<int>
            {
                public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
                    => "floor " + value.ToString(provider);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should()
            .Contain("return FormatValue(in current, global::System.MemoryExtensions.AsSpan(format), formatProvider ?? ")
            .And.Contain("var text = FormatValue(in current, format, provider ?? ")
            .And.NotContain("UnderlyingValue.TryFormat(in current, destination,")
            .And.Contain("UnderlyingValue.TryFormat(in current, buffer, out var written, default, ");
    }

    /// <summary>
    /// A formatting hook takes over the default format too, so ToString() goes through it. A hook that no buffer
    /// satisfies, up to the bound, makes it throw rather than write the plain value, which is not what was asked for.
    /// </summary>
    [Fact]
    public void With_a_formatting_hook_ToString_goes_through_it_and_never_writes_another_text()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Celsius : IValueObjectFormatter<int>
            {
                public static bool TryFormatValue(
                    in int value,
                    Span<char> destination,
                    out int charsWritten,
                    ReadOnlySpan<char> format,
                    IFormatProvider? provider)
                    => destination.TryWrite(provider, $"{value} °C", out charsWritten);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should()
            .Contain("public override string ToString() => ToString(null, null);")
            .And.Contain("global::System.Buffers.ArrayPool<char>.Shared.Rent(length)")
            .And.Contain("throw new global::System.FormatException(")
            .And.NotContain("Value.ToString(")
            .And.NotContain(": ToString();");
    }

    /// <summary>
    /// The string formatter takes precedence when a type declares both hooks, in span formatting as in
    /// <c>ToString</c>, so that interpolation and <c>ToString(format, provider)</c> write the same text.
    /// </summary>
    [Fact]
    public void With_both_formatting_hooks_the_string_formatter_answers()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct Floor : IValueObjectFormatter<int>, IValueObjectStringFormatter<int>
            {
                public static string FormatValue(in int value, ReadOnlySpan<char> format, IFormatProvider? provider)
                    => "floor " + value.ToString(provider);

                public static bool TryFormatValue(
                    in int value,
                    Span<char> destination,
                    out int charsWritten,
                    ReadOnlySpan<char> format,
                    IFormatProvider? provider)
                    => value.TryFormat(destination, out charsWritten, format, provider);
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should()
            .Contain("var text = FormatValue(in current, format, provider ?? ")
            .And.NotContain("TryFormatValue(");
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

    /// <summary>
    /// The analyzer ships inside the package, but nothing stops a project from loading it without the contracts:
    /// a member shaped like a hook is then just a member, since no type can be a value object.
    /// </summary>
    [Fact]
    public async Task The_analyzer_says_nothing_where_the_library_is_not_referenced()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>(
            """
            namespace Plain;

            public readonly struct Point
            {
                public static int NormalizeValue(int value) => value;
            }
            """,
            GeneratorHarness.FrameworkReferences);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which
    /// it tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new ValueObjectHookAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }
}
