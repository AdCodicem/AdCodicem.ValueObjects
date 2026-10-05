using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The one form each underlying type read a bound or a known value in, when an attribute took them as text, and the text
/// it refuses.
/// </summary>
/// <remarks>
/// <para>
/// The generator reads no value as text any more (docs/adr/0011-declare-known-values-and-examples-as-typed-members.md):
/// the code fix of <c>VO0034</c> reads this form, to rewrite a known value declared on the type as the expression of its
/// type a member takes, until the constructor taking text is removed. A form is strict on purpose: no white space around
/// it, no culture, no time zone, and nothing a parser would fill in from the machine running the compiler, such as
/// today's date for a time written alone. Text in any other form is left as it was written.
/// </para>
/// <para>
/// A string, a Guid and a bool keep the forms they always read.
/// </para>
/// </remarks>
public sealed class LiteralFormTests
{
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
            "new global::System.Int128(9223372036854775808UL, 0UL)"
        },
        {
            "UInt128",
            "0340282366920938463463374607431768211455",
            "new global::System.UInt128(18446744073709551615UL, 18446744073709551615UL)"
        },
        { "Int128", "-1", "new global::System.Int128(18446744073709551615UL, 18446744073709551615UL)" },
        { "Int128", "18446744073709551616", "new global::System.Int128(1UL, 0UL)" },
        { "UInt128", "42", "new global::System.UInt128(0UL, 42UL)" },
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
    [MemberData(nameof(UnorderedForms))]
    public void Text_in_the_form_of_its_type_reads_as_the_value_written(string underlying, string text, string literal)
    {
        LiteralFactory.TryCreate(Resolve(underlying), text, out var read).Should().BeTrue();

        read.Should().Be(literal);
    }

    [Theory]
    [MemberData(nameof(OrderedRefusals))]
    [MemberData(nameof(UnorderedRefusals))]
    public void Text_outside_the_form_of_its_type_does_not_read(string underlying, string text)
        => LiteralFactory.TryCreate(Resolve(underlying), text, out _).Should().BeFalse();

    private static UnderlyingType Resolve(string underlying)
    {
        var name = underlying switch
        {
            "string" => "String",
            "bool" => "Boolean",
            "char" => "Char",
            "sbyte" => "SByte",
            "byte" => "Byte",
            "short" => "Int16",
            "ushort" => "UInt16",
            "int" => "Int32",
            "uint" => "UInt32",
            "long" => "Int64",
            "ulong" => "UInt64",
            "decimal" => "Decimal",
            "double" => "Double",
            "float" => "Single",
            _ => underlying,
        };

        UnderlyingType.TryResolve("global::System." + name, out var resolved).Should().BeTrue();
        return resolved!;
    }

    private static string Guid(string canonical) => $"new global::System.Guid(\"{canonical}\")";

    private static string TimeOnlyLiteral(TimeOnly value) => $"new global::System.TimeOnly({value.Ticks}L)";

    private static string DateTimeLiteral(DateTime value)
        => $"new global::System.DateTime({value.Ticks}L, global::System.DateTimeKind.Unspecified)";

    private static string OffsetLiteral(DateTime clock, TimeSpan offset)
        => $"new global::System.DateTimeOffset({clock.Ticks}L, new global::System.TimeSpan({offset.Ticks}L))";

    private static string TimeSpanLiteral(TimeSpan value) => $"new global::System.TimeSpan({value.Ticks}L)";
}
