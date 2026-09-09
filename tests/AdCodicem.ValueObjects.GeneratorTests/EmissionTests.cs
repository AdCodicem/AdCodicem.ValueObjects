namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What the generator emits for a well-formed value object.
/// </summary>
/// <remarks>
/// The unit tests exercise generated code by using it, which proves it behaves but not that it exists: a member
/// silently not emitted looks identical to one that was never asked for. These tests read the output.
/// </remarks>
public sealed class EmissionTests
{
    [Fact]
    public void A_string_value_object_compiles_with_no_diagnostics()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.Files.Should().HaveCount(2, "the value object and the assembly registration");
    }

    [Fact]
    public void The_struct_holds_exactly_its_underlying_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        // The whole memory argument for a struct value object rests on this single field.
        run.SingleValueObject.Should().Contain("private readonly global::System.String? _value;");
    }

    [Fact]
    public void The_contract_members_are_all_emitted()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var generated = run.SingleValueObject;

        foreach (var member in new[]
        {
            "public static global::Test.Code Create(",
            "public static bool TryCreate(",
            "public static global::Test.Code CreateUnchecked(",
            "public static global::Test.Code Parse(",
            "public static bool TryParse(",
            "public bool Equals(global::Test.Code",
            "public override int GetHashCode()",
            "public int CompareTo(global::Test.Code",
            "public bool TryFormat(",
            "public override string ToString()",
            "public bool IsDefault",
        })
        {
            generated.Should().Contain(member);
        }
    }

    [Fact]
    public void A_value_object_serializes_as_its_underlying_value()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var generated = run.SingleValueObject;

        // Writing the bare value, rather than an object wrapper, is the point of the whole library.
        generated.Should().Contain("writer.WriteStringValue(value.Value);");
        generated.Should().Contain("public sealed class ValueJsonConverter");
    }

    [Fact]
    public void Arithmetic_is_emitted_only_when_asked_for()
    {
        var without = GeneratorHarness.Run("""
            [ValueObject<decimal>]
            public readonly partial struct Money;
            """);

        var with = GeneratorHarness.Run("""
            [ValueObject<decimal>(Arithmetic = true)]
            public readonly partial struct Money;
            """);

        without.SingleValueObject.Should().NotContain("operator +");
        with.SingleValueObject.Should().Contain("operator +");
        with.SingleValueObject.Should().Contain("public static global::Test.Money Zero");
    }

    [Fact]
    public void Integral_arithmetic_is_checked_so_an_overflow_throws_rather_than_wraps()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>(Arithmetic = true)]
            public readonly partial struct Count;
            """);

        run.SingleValueObject.Should().Contain("checked(");
    }

    [Fact]
    public void A_closed_value_set_emits_named_constants_and_a_frozen_lookup()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
            [KnownValue("France", "FR")]
            [KnownValue("Belgium", "BE")]
            public readonly partial struct Country;
            """);

        var generated = run.SingleValueObject;

        run.CompilationDiagnostics.Should().BeEmpty();
        generated.Should().Contain("public static global::Test.Country France { get; } = Create(\"FR\");");
        generated.Should().Contain("public static global::Test.Country Belgium { get; } = Create(\"BE\");");
        generated.Should().Contain("FrozenSet");
    }

    [Fact]
    public void A_nested_value_object_reopens_every_containing_type()
    {
        var run = GeneratorHarness.Run("""
            public static partial class Outer
            {
                public static partial class Inner
                {
                    [ValueObject<string>]
                    public readonly partial struct Code;
                }
            }
            """);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain("partial class Outer").And.Contain("partial class Inner");
    }

    [Fact]
    public void Conversions_are_opt_in()
    {
        var without = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code;
            """);

        var with = GeneratorHarness.Run("""
            [ValueObject<string>(ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
            public readonly partial struct Code;
            """);

        without.SingleValueObject.Should().NotContain("operator global::System.String");
        with.SingleValueObject.Should().Contain("implicit operator global::System.String");
        with.SingleValueObject.Should().Contain("explicit operator global::Test.Code");
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("Guid")]
    [InlineData("DateOnly")]
    [InlineData("TimeOnly")]
    [InlineData("DateTimeOffset")]
    [InlineData("TimeSpan")]
    [InlineData("bool")]
    [InlineData("char")]
    [InlineData("Int128")]
    public void Every_supported_underlying_type_produces_compiling_code(string underlying)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty("'{0}' must generate code that compiles", underlying);
    }
}
