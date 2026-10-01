using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The one form each underlying type reads a bound or a known value in, and the text it refuses.
/// </summary>
/// <remarks>
/// <para>
/// A bound and a known value written as text go through the same reading, so each form is tried as both. A form
/// is strict on purpose: no white space around it, no culture, no time zone, and nothing a parser would fill in
/// from the machine running the compiler, such as today's date for a time written alone. The same declaration
/// then compiles to the same literal everywhere, and anything else is refused with a message naming the form.
/// </para>
/// <para>
/// A string, a Guid and a bool take no bound; as known values they keep the forms they always read.
/// </para>
/// </remarks>
public sealed class LiteralFormTests
{
    private const string SignedInteger = "digits, with a leading '-' when negative, such as \"-42\"";
    private const string UnsignedInteger = "digits alone, such as \"42\"";
    private const string Real =
        "digits with an optional leading '-', an optional fraction after '.' and an optional exponent, such as \"-1.5e-3\"";

    private static readonly Dictionary<string, string> Forms = new(StringComparer.Ordinal)
    {
        ["string"] = "text",
        ["Guid"] = "a GUID, such as \"6f9619ff-8b86-d011-b42d-00c04fc964ff\"",
        ["bool"] = "true or false",
        ["char"] = "exactly one character",
        ["sbyte"] = SignedInteger,
        ["byte"] = UnsignedInteger,
        ["short"] = SignedInteger,
        ["ushort"] = UnsignedInteger,
        ["int"] = SignedInteger,
        ["uint"] = UnsignedInteger,
        ["long"] = SignedInteger,
        ["ulong"] = UnsignedInteger,
        ["Int128"] = SignedInteger,
        ["UInt128"] = UnsignedInteger,
        ["decimal"] = "digits with an optional leading '-' and an optional fraction after '.', such as \"-19.99\"",
        ["double"] = Real,
        ["float"] = Real,
        ["DateOnly"] = "yyyy-MM-dd, such as \"2024-01-31\"",
        ["TimeOnly"] = "HH:mm, HH:mm:ss or HH:mm:ss.fffffff, such as \"08:30\"",
        ["DateTime"] = "yyyy-MM-dd or yyyy-MM-ddTHH:mm[:ss[.fffffff]], without an offset, such as \"2024-01-31T08:30\"",
        ["DateTimeOffset"] =
            "yyyy-MM-ddTHH:mm[:ss[.fffffff]] followed by Z, +HH:mm or -HH:mm, such as \"2024-01-31T08:30+01:00\"",
        ["TimeSpan"] = "[-][d.]hh:mm:ss[.fffffff], such as \"1.12:00:00\"",
    };

    /// <summary>
    /// Gets every form a type with an order reads, as text, with the literal it compiles to.
    /// </summary>
    public static TheoryData<string, string, string> OrderedForms => new()
    {
        { "char", "A", "'A'" },
        { "char", " ", "' '" },
        { "char", "'", "'\\''" },
        { "sbyte", "-128", "(sbyte)(-128)" },
        { "sbyte", "127", "(sbyte)(127)" },
        { "sbyte", "-0", "(sbyte)(0)" },
        { "sbyte", "007", "(sbyte)(7)" },
        { "byte", "0", "(byte)(0)" },
        { "byte", "255", "(byte)(255)" },
        { "short", "-32768", "(short)(-32768)" },
        { "ushort", "65535", "(ushort)(65535)" },
        { "int", "-2147483648", "(int)(-2147483648)" },
        { "int", "2147483647", "(int)(2147483647)" },
        { "uint", "4294967295", "4294967295U" },
        { "long", "-9223372036854775808", "-9223372036854775808L" },
        { "ulong", "18446744073709551615", "18446744073709551615UL" },
        {
            "Int128",
            "-170141183460469231731687303715884105728",
            "global::System.Int128.Parse(\"-170141183460469231731687303715884105728\", global::System.Globalization.CultureInfo.InvariantCulture)"
        },
        {
            "UInt128",
            "0340282366920938463463374607431768211455",
            "global::System.UInt128.Parse(\"340282366920938463463374607431768211455\", global::System.Globalization.CultureInfo.InvariantCulture)"
        },
        { "decimal", "-19.990", "-19.990m" },
        { "decimal", "0", "0m" },
        { "decimal", "-0.5", "-0.5m" },
        { "decimal", "79228162514264337593543950335", "79228162514264337593543950335m" },
        { "double", "0.25", "0.25d" },
        { "double", "-1.5e-3", "-0.0015d" },
        { "double", "1e3", "1000d" },
        { "double", "9.1E-31", "9.1E-31d" },
        { "double", "2e+32", "2E+32d" },
        { "double", "1.7976931348623157E+308", "1.7976931348623157E+308d" },
        { "double", "0", "0d" },
        { "double", "-0.0", "-0d" },
        { "double", "0.000e-400", "0d" },
        { "double", "5e-324", "5E-324d" },
        { "float", "1.5e3", "1500f" },
        { "float", "0e-50", "0f" },
        { "float", "1e-45", "1E-45f" },
        { "float", "-3.4028235E+38", "-3.4028235E+38f" },
        { "float", "0.1", "0.1f" },
        { "DateOnly", "2024-02-29", "new global::System.DateOnly(2024, 2, 29)" },
        { "DateOnly", "0001-01-01", "new global::System.DateOnly(1, 1, 1)" },
        { "DateOnly", "9999-12-31", "new global::System.DateOnly(9999, 12, 31)" },
        { "TimeOnly", "00:00", TimeOnlyLiteral(new TimeOnly(0, 0)) },
        { "TimeOnly", "08:30", TimeOnlyLiteral(new TimeOnly(8, 30)) },
        { "TimeOnly", "08:30:15", TimeOnlyLiteral(new TimeOnly(8, 30, 15)) },
        { "TimeOnly", "08:30:15.5", TimeOnlyLiteral(new TimeOnly(8, 30, 15, 500)) },
        { "TimeOnly", "23:59:59.9999999", TimeOnlyLiteral(TimeOnly.MaxValue) },
        { "DateTime", "2024-01-31", DateTimeLiteral(new DateTime(2024, 1, 31)) },
        { "DateTime", "2024-01-31T08:30", DateTimeLiteral(new DateTime(2024, 1, 31, 8, 30, 0)) },
        { "DateTime", "2024-01-31T08:30:15", DateTimeLiteral(new DateTime(2024, 1, 31, 8, 30, 15)) },
        { "DateTime", "2024-01-31T08:30:15.1234567", DateTimeLiteral(new DateTime(2024, 1, 31, 8, 30, 15).AddTicks(1234567)) },
        { "DateTime", "9999-12-31T23:59:59.9999999", DateTimeLiteral(DateTime.MaxValue) },
        { "DateTimeOffset", "2024-01-31T08:30Z", OffsetLiteral(new DateTime(2024, 1, 31, 8, 30, 0), TimeSpan.Zero) },
        { "DateTimeOffset", "2024-01-31T08:30:00+01:00", OffsetLiteral(new DateTime(2024, 1, 31, 8, 30, 0), TimeSpan.FromHours(1)) },
        {
            "DateTimeOffset",
            "2024-01-31T08:30:00.5-05:30",
            OffsetLiteral(new DateTime(2024, 1, 31, 8, 30, 0, 500), -new TimeSpan(5, 30, 0))
        },
        { "DateTimeOffset", "2024-01-31T08:30-00:00", OffsetLiteral(new DateTime(2024, 1, 31, 8, 30, 0), TimeSpan.Zero) },
        { "DateTimeOffset", "2024-01-31T00:00+14:00", OffsetLiteral(new DateTime(2024, 1, 31), TimeSpan.FromHours(14)) },
        { "DateTimeOffset", "0001-01-01T00:00Z", OffsetLiteral(DateTime.MinValue, TimeSpan.Zero) },
        { "DateTimeOffset", "9999-12-31T23:59:59.9999999Z", OffsetLiteral(DateTime.MaxValue, TimeSpan.Zero) },
        { "TimeSpan", "00:00:00", TimeSpanLiteral(TimeSpan.Zero) },
        { "TimeSpan", "-00:00:01", TimeSpanLiteral(TimeSpan.FromSeconds(-1)) },
        { "TimeSpan", "1.12:00:00", TimeSpanLiteral(new TimeSpan(1, 12, 0, 0)) },
        { "TimeSpan", "01.00:00:00", TimeSpanLiteral(TimeSpan.FromDays(1)) },
        { "TimeSpan", "-1.12:00:00.5", TimeSpanLiteral(-new TimeSpan(1, 12, 0, 0, 500)) },
        { "TimeSpan", "10675199.02:48:05.4775807", TimeSpanLiteral(TimeSpan.MaxValue) },
        { "TimeSpan", "-10675199.02:48:05.4775808", TimeSpanLiteral(TimeSpan.MinValue) },
    };

    /// <summary>
    /// Gets the forms a known value of a type without an order is read in, with the literal each compiles to.
    /// </summary>
    public static TheoryData<string, string, string> UnorderedForms => new()
    {
        { "string", " padded ", "\" padded \"" },
        { "string", "", "\"\"" },
        { "Guid", "6F9619FF-8B86-D011-B42D-00C04FC964FF", Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff") },
        { "Guid", "6f9619ff8b86d011b42d00c04fc964ff", Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff") },
        { "Guid", "{6f9619ff-8b86-d011-b42d-00c04fc964ff}", Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff") },
        { "Guid", "(6f9619ff-8b86-d011-b42d-00c04fc964ff)", Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff") },
        { "bool", "true", "true" },
        { "bool", "TRUE", "true" },
        { "bool", "False", "false" },
    };

    /// <summary>
    /// Gets text outside the form of a type with an order, which a bound and a known value alike refuse.
    /// </summary>
    public static TheoryData<string, string> OrderedRefusals => new()
    {
        { "char", "" },
        { "char", "ab" },
        { "int", "" },
        { "int", " " },
        { "int", " 1" },
        { "int", "1 " },
        { "int", "+1" },
        { "int", "-" },
        { "int", "--1" },
        { "int", "1_000" },
        { "int", "1,000" },
        { "int", "1e3" },
        { "int", "1.0" },
        { "int", "0x10" },
        { "int", "\u0661" },
        { "int", "2147483648" },
        { "sbyte", "128" },
        { "uint", "-1" },
        { "ulong", "-0" },
        { "Int128", "+1" },
        { "Int128", "170141183460469231731687303715884105728" },
        { "UInt128", "-0" },
        { "decimal", "1e6" },
        { "decimal", "1E6" },
        { "decimal", ".5" },
        { "decimal", "1." },
        { "decimal", "+1" },
        { "decimal", "1,5" },
        { "decimal", " 1" },
        { "decimal", "-" },
        { "decimal", "79228162514264337593543950336" },
        { "double", "NaN" },
        { "double", "Infinity" },
        { "double", "-Infinity" },
        { "double", "1e400" },
        { "double", ".5" },
        { "double", "1." },
        { "double", "1e" },
        { "double", "1e+" },
        { "double", "1e-" },
        { "double", "+1" },
        { "double", "0x1" },
        { "double", "1d" },
        { "double", "1,5" },
        { "double", "1 " },
        { "double", "\uFF11" },
        { "double", "1e-400" },
        { "double", "-1e-400" },
        { "double", "0.001e-325" },
        { "double", "2e-324" },
        { "float", "3.5e38" },
        { "float", "1e-46" },
        { "float", "-0.5e-45" },
        { "float", "1f" },
        { "float", "NaN" },
        { "DateOnly", "2024-1-31" },
        { "DateOnly", "24-01-31" },
        { "DateOnly", "2024/01/31" },
        { "DateOnly", "2024-01/31" },
        { "DateOnly", "2024-13-01" },
        { "DateOnly", "2024-01-32" },
        { "DateOnly", "2023-02-29" },
        { "DateOnly", "0000-01-01" },
        { "DateOnly", "2024-00-10" },
        { "DateOnly", "2024-01-00" },
        { "DateOnly", "2024-01-31T00:00" },
        { "DateOnly", " 2024-01-31" },
        { "DateOnly", "01/31/2024" },
        { "TimeOnly", "8:30" },
        { "TimeOnly", "08:3" },
        { "TimeOnly", "08" },
        { "TimeOnly", "0830" },
        { "TimeOnly", "24:00" },
        { "TimeOnly", "08:60" },
        { "TimeOnly", "08:30:60" },
        { "TimeOnly", "08:30:5" },
        { "TimeOnly", "08:30:00." },
        { "TimeOnly", "08:30:00.12345678" },
        { "TimeOnly", "08:30:00Z" },
        { "TimeOnly", "08:30 " },
        { "TimeOnly", "-01:00" },
        { "TimeOnly", "1.00:00:00" },
        { "TimeOnly", "noon" },
        { "DateTime", "08:00" },
        { "DateTime", "2024-01-31 08:30" },
        { "DateTime", "2024-01-31T" },
        { "DateTime", "2024-01-31T8:30" },
        { "DateTime", "2024-01-31t08:30" },
        { "DateTime", "2024-01-31T08:30Z" },
        { "DateTime", "2024-01-31T08:30+01:00" },
        { "DateTime", "2024-01-31T08:30:00-05:00" },
        { "DateTime", "not a date" },
        { "DateTimeOffset", "08:00Z" },
        { "DateTimeOffset", "2024-01-31" },
        { "DateTimeOffset", "2024-01-31Z" },
        { "DateTimeOffset", "2024-01-31T08:30" },
        { "DateTimeOffset", "2024-01-31T25:30Z" },
        { "DateTimeOffset", "2024-01-31T08:30z" },
        { "DateTimeOffset", "2024-01-31T08:30+1:00" },
        { "DateTimeOffset", "2024-01-31T08:30+0100" },
        { "DateTimeOffset", "2024-01-31T08:30+01" },
        { "DateTimeOffset", "2024-01-31T08:30+15:00" },
        { "DateTimeOffset", "2024-01-31T08:30+14:30" },
        { "DateTimeOffset", "2024-01-31T08:30+01:60" },
        { "DateTimeOffset", "2024-01-31T08:30 +01:00" },
        { "DateTimeOffset", "2024-01-31T08:30+01:00 " },
        { "DateTimeOffset", "0001-01-01T00:30:00+02:00" },
        { "DateTimeOffset", "9999-12-31T23:00:00-02:00" },
        { "TimeSpan", "1" },
        { "TimeSpan", "-" },
        { "TimeSpan", "1." },
        { "TimeSpan", "00:00" },
        { "TimeSpan", "1.00:00" },
        { "TimeSpan", "1:00:00" },
        { "TimeSpan", "1.1:00:00" },
        { "TimeSpan", "24:00:00" },
        { "TimeSpan", "+00:00:01" },
        { "TimeSpan", "00:00:00." },
        { "TimeSpan", "00:00:00.12345678" },
        { "TimeSpan", " 00:00:00" },
        { "TimeSpan", "10675199.02:48:05.4775808" },
        { "TimeSpan", "-10675199.02:48:05.4775809" },
        { "TimeSpan", "P1D" },
        { "TimeSpan", "forever" },
    };

    /// <summary>
    /// Gets text a known value of a type without an order refuses: what its type does not read, and the white
    /// space and padding its parser would have trimmed.
    /// </summary>
    public static TheoryData<string, string> UnorderedRefusals => new()
    {
        { "Guid", "" },
        { "Guid", "not a guid" },
        { "Guid", " 6f9619ff-8b86-d011-b42d-00c04fc964ff" },
        { "Guid", "6f9619ff-8b86-d011-b42d-00c04fc964ff " },
        { "bool", "" },
        { "bool", "yes" },
        { "bool", "1" },
        { "bool", " true" },
        { "bool", "false " },
        { "bool", "true\0" },
    };

    [Theory]
    [MemberData(nameof(OrderedForms))]
    public void A_bound_written_in_the_form_of_its_type_compiles_to_the_value_written(
        string underlying,
        string text,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Minimum = {{Quote(text)}})]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain(literal);
    }

    [Theory]
    [MemberData(nameof(OrderedForms))]
    [MemberData(nameof(UnorderedForms))]
    public void A_known_value_written_in_the_form_of_its_type_compiles_to_the_value_written(
        string underlying,
        string text,
        string literal)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>{{(underlying == "string" ? "(AllowEmpty = true)" : string.Empty)}}]
            [KnownValue("Named", {{Quote(text)}})]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
        run.SingleValueObject.Should().Contain($"Named {{ get; }} = Create({literal});");
    }

    [Theory]
    [MemberData(nameof(OrderedRefusals))]
    public void A_bound_outside_the_form_of_its_type_is_reported_with_that_form(string underlying, string text)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>(Maximum = {{Quote(text)}})]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Equal("VO0004");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'{text}' is not a valid Maximum for underlying type '{Keyword(underlying)}': "
            + $"write a value of that type as {Forms[underlying]}");
        run.SingleValueObject.Should().NotContain("Maximum = ", "a refused bound is neither enforced nor published");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(OrderedRefusals))]
    [MemberData(nameof(UnorderedRefusals))]
    public void A_known_value_outside_the_form_of_its_type_is_reported_with_that_form(string underlying, string text)
    {
        var run = GeneratorHarness.Run($$"""
            [ValueObject<{{underlying}}>]
            [KnownValue("Named", {{Quote(text)}})]
            public readonly partial struct Wrapper;
            """);

        run.Ids.Should().Equal("VO0013");
        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"The known value '{text}' declared on 'Wrapper' cannot be converted to the underlying type "
            + $"'{Keyword(underlying)}': write a value of that type as {Forms[underlying]}");
        run.SingleValueObject.Should().NotContain(" Named ");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_known_value_the_type_cannot_hold_names_the_form_of_a_string_too()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            [KnownValue("Named", typeof(int))]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Single().GetMessage(CultureInfo.InvariantCulture).Should().EndWith(
            "'string': write a value of that type as " + Forms["string"]);
    }

    private static string Quote(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    private static string Keyword(string underlying) => underlying switch
    {
        "Guid" or "Int128" or "UInt128" or "DateOnly" or "TimeOnly" or "DateTime" or "DateTimeOffset" or "TimeSpan"
            => "System." + underlying,
        _ => underlying,
    };

    private static string Guid(string canonical) => $"new global::System.Guid(\"{canonical}\")";

    private static string TimeOnlyLiteral(TimeOnly value) => $"new global::System.TimeOnly({value.Ticks}L)";

    private static string DateTimeLiteral(DateTime value)
        => $"new global::System.DateTime({value.Ticks}L, global::System.DateTimeKind.Unspecified)";

    private static string OffsetLiteral(DateTime clock, TimeSpan offset)
        => $"new global::System.DateTimeOffset({clock.Ticks}L, new global::System.TimeSpan({offset.Ticks}L))";

    private static string TimeSpanLiteral(TimeSpan value) => $"new global::System.TimeSpan({value.Ticks}L)";
}
