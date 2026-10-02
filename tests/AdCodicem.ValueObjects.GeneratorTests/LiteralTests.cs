using System.Globalization;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// How a bound or a known value written on the declaration becomes a literal of the underlying type.
/// </summary>
/// <remarks>
/// Attribute arguments carry only a handful of constant types, so most bounds and known values are text the
/// generator parses at compile time. Each underlying type reads its own form, and each fails its own way: these
/// cover one declaration per type, accepted and refused, and <see cref="LiteralFormTests"/> every form.
/// </remarks>
public sealed class LiteralTests
{
    [Theory]
    [InlineData("Guid", "\"6F9619FF-8B86-D011-B42D-00C04FC964FF\"", "Create(new global::System.Guid(\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"))")]
    [InlineData("bool", "true", "Create(true)")]
    [InlineData("bool", "\"false\"", "Create(false)")]
    [InlineData("char", "'\\''", "Create('\\'')")]
    [InlineData("int", "0", "Create((int)(0))")]
    [InlineData("long", "\"-42\"", "Create(-42L)")]
    [InlineData("UInt128", "7", "Create(new global::System.UInt128(0UL, 7UL))")]
    [InlineData("decimal", "\"19.990\"", "Create(19.990m)")]
    [InlineData("double", "0.25", "Create(0.25d)")]
    [InlineData("float", "1.5f", "Create(1.5f)")]
    [InlineData("DateOnly", "\"2020-02-29\"", "Create(new global::System.DateOnly(2020, 2, 29))")]
    [InlineData("TimeOnly", "\"08:30\"", "Create(new global::System.TimeOnly(306000000000L))")]
    [InlineData("TimeSpan", "\"1.00:00:00\"", "Create(new global::System.TimeSpan(864000000000L))")]
    [InlineData("DateTime", "\"2020-01-01\"", "Create(new global::System.DateTime(637134336000000000L, global::System.DateTimeKind.Unspecified))")]
    [InlineData(
        "DateTimeOffset",
        "\"2020-01-01T00:00:00-05:00\"",
        "Create(new global::System.DateTimeOffset(637134336000000000L, new global::System.TimeSpan(-180000000000L)))")]
    public void A_known_value_of_every_underlying_type_becomes_a_literal_of_that_type(
        string underlying,
        string value,
        string initializer)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{value}})]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"Named {{ get; }} = {initializer};");
    }

    /// <summary>
    /// A double or a float given as a constant becomes text in its round-trip form before it becomes a literal.
    /// Its default form keeps 15 significant digits for a double and 7 for a float when the compiler runs on .NET
    /// Framework, as in Visual Studio, and there names a neighbouring value.
    /// </summary>
    [Theory]
    [InlineData("double", "1d / 3d", "0.3333333333333333d")]
    [InlineData("double", "0.1 + 0.2", "0.30000000000000004d")]
    [InlineData("float", "1f / 3f", "0.33333334f")]
    [InlineData("float", "1d / 3d", "0.33333334f")]
    public void A_floating_point_known_value_given_as_a_constant_keeps_every_digit_of_its_value(
        string underlying,
        string value,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{value}})]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"Named {{ get; }} = Create({literal});");
    }

    [Theory]
    [InlineData("Guid", "\"not a guid\"")]
    [InlineData("bool", "\"maybe\"")]
    [InlineData("char", "\"ab\"")]
    [InlineData("int", "1.5")]
    [InlineData("TimeOnly", "\"25:00\"")]
    public void A_known_value_that_does_not_convert_to_its_underlying_type_is_reported(string underlying, string value)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{value}})]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Equal("VO0013");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_null_known_value_is_reported_as_null()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("None", null)]
            [KnownValue("France", "FR")]
            public readonly partial struct Country;
            """);

        run.Ids.Should().Equal("VO0013");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain("'null'");
        run.SingleValueObject.Should().Contain("France { get; } = Create(\"FR\");").And.NotContain(" None ");
    }

    [Fact]
    public void A_known_value_holding_characters_a_literal_must_escape_compiles()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(AllowEmpty = true)]
            [KnownValue("Tricky", "q\"a'b\r\n\t\0\u0001\\")]
            [KnownValue("Empty", "")]
            public readonly partial struct Token;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should()
            .Contain("""Tricky { get; } = Create("q\"a\'b\r\n\t\0\u0001\\");""")
            .And.Contain("""Empty { get; } = Create("");""");
    }

    [Theory]
    [InlineData("char", "a", "z", "if (value > 'z')")]
    [InlineData("decimal", "-0.5", "99.99", "if (value > 99.99m)")]
    [InlineData("DateOnly", "2020-01-01", "2030-12-31", "if (value > new global::System.DateOnly(2030, 12, 31))")]
    [InlineData("TimeOnly", "08:00", "18:30:15", "if (value > new global::System.TimeOnly(666150000000L))")]
    [InlineData("TimeSpan", "00:00:01", "1.00:00:00", "if (value > new global::System.TimeSpan(864000000000L))")]
    public void A_bound_of_every_ordered_underlying_type_compiles(
        string underlying,
        string minimum,
        string maximum,
        string check)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{minimum}}", Maximum = "{{maximum}}")]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(check);
    }

    /// <summary>
    /// Text that does not have the form of the underlying type is refused on the declaration.
    /// <see cref="LiteralFormTests"/> goes through every form, accepted and refused.
    /// </summary>
    [Theory]
    [InlineData("long", "12x")]
    [InlineData("decimal", "1,5")]
    [InlineData("double", "abc")]
    [InlineData("float", "abc")]
    [InlineData("char", "ab")]
    [InlineData("DateOnly", "01/02/2020")]
    [InlineData("TimeOnly", "noon")]
    [InlineData("TimeOnly", "-01:00")]
    [InlineData("TimeOnly", "1.00:00:00")]
    [InlineData("TimeSpan", "forever")]
    [InlineData("DateTime", "not a date")]
    [InlineData("DateTimeOffset", "not a date")]
    [InlineData("DateTimeOffset", "0001-01-01T00:30:00+02:00")]
    public void A_bound_that_does_not_parse_as_its_underlying_type_is_reported(string underlying, string bound)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = "{{bound}}")]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Equal("VO0004");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Contain($"'{bound}'");
        run.CompilationDiagnostics.Should().BeEmpty();
    }
}
