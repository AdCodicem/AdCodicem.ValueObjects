using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AdCodicem.ValueObjects.Generators.Model;

/// <summary>
/// The underlying types a value object may wrap.
/// </summary>
internal enum UnderlyingKind
{
    String,
    Guid,
    Boolean,
    Char,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Int128,
    UInt128,
    Decimal,
    Double,
    Single,
    DateOnly,
    TimeOnly,
    DateTime,
    DateTimeOffset,
    TimeSpan,
}

/// <summary>
/// Everything the emitters need to know about an underlying type: how to compare it, how to move it in and out
/// of JSON, and how to describe it in an OpenAPI schema.
/// </summary>
/// <remarks>
/// Keeping this as a closed table rather than probing capabilities through interfaces is deliberate: each entry
/// picks the most direct API for its type, so the generated code never goes through a comparer lookup, a boxed
/// <c>IConvertible</c> call, or a reflection-backed JSON converter.
/// </remarks>
internal sealed class UnderlyingType
{
    private UnderlyingType(UnderlyingKind kind, string fullName, string keyword)
    {
        Kind = kind;
        FullName = fullName;
        Keyword = keyword;
    }

    public UnderlyingKind Kind { get; }

    /// <summary>Gets the globally qualified type name, for example <c>global::System.Guid</c>.</summary>
    public string FullName { get; }

    /// <summary>Gets the C# keyword or short name used in generated signatures.</summary>
    public string Keyword { get; }

    public bool IsString => Kind == UnderlyingKind.String;

    /// <summary>
    /// Gets a value indicating whether the type formats and parses itself through spans.
    /// </summary>
    /// <remarks>
    /// Only <see cref="string"/>, <c>bool</c> and <c>char</c> are left out: they implement neither
    /// <c>ISpanFormattable</c> nor <c>ISpanParsable</c>, and the emitters handle those three explicitly.
    /// </remarks>
    public bool IsSpanFormattable => Kind is not (UnderlyingKind.String or UnderlyingKind.Boolean or UnderlyingKind.Char);

    /// <summary>
    /// Gets a value indicating whether the type supports the relational operators used by generated bounds checks.
    /// </summary>
    public bool SupportsBounds => Kind is not (UnderlyingKind.String or UnderlyingKind.Guid or UnderlyingKind.Boolean);

    public bool IsReferenceType => IsString;

    public bool IsNumeric { get; private init; }

    public bool IsSigned { get; private init; }

    /// <summary>Gets a value indicating whether the JSON representation is a number rather than a string.</summary>
    public bool IsJsonNumber { get; private init; }

    /// <summary>Gets a value indicating whether the JSON representation is a boolean literal.</summary>
    public bool IsJsonBoolean => Kind == UnderlyingKind.Boolean;

    /// <summary>Gets the expression reading the value from a <c>Utf8JsonReader</c> named <c>reader</c>.</summary>
    public string JsonReadExpression { get; private init; } = "reader.GetString()!";

    /// <summary>Gets the round-trip format string used when the value is written as JSON text.</summary>
    public string? RoundTripFormat { get; private init; }

    /// <summary>Gets the size of the stack buffer used to format the value without allocating.</summary>
    public int FormatBufferSize { get; private init; } = 32;

    /// <summary>Gets the OpenAPI <c>type</c> keyword.</summary>
    public string SchemaType { get; private init; } = "string";

    /// <summary>Gets the default OpenAPI <c>format</c> keyword.</summary>
    public string? SchemaFormat { get; private init; }

    public static readonly UnderlyingType String =
        new(UnderlyingKind.String, "global::System.String", "string");

    private static readonly UnderlyingType[] All =
    [
        String,
        new(UnderlyingKind.Guid, "global::System.Guid", "System.Guid")
        {
            JsonReadExpression = "reader.GetGuid()", SchemaFormat = "uuid", FormatBufferSize = 36,
        },
        new(UnderlyingKind.Boolean, "global::System.Boolean", "bool")
        {
            JsonReadExpression = "reader.GetBoolean()", SchemaType = "boolean", FormatBufferSize = 5,
        },
        new(UnderlyingKind.Char, "global::System.Char", "char")
        {
            JsonReadExpression = "ReadChar(ref reader)", FormatBufferSize = 1,
        },
        Integral(UnderlyingKind.SByte, "global::System.SByte", "sbyte", "reader.GetSByte()", signed: true, "int32"),
        Integral(UnderlyingKind.Byte, "global::System.Byte", "byte", "reader.GetByte()", signed: false, "int32"),
        Integral(UnderlyingKind.Int16, "global::System.Int16", "short", "reader.GetInt16()", signed: true, "int32"),
        Integral(UnderlyingKind.UInt16, "global::System.UInt16", "ushort", "reader.GetUInt16()", signed: false, "int32"),
        Integral(UnderlyingKind.Int32, "global::System.Int32", "int", "reader.GetInt32()", signed: true, "int32"),
        Integral(UnderlyingKind.UInt32, "global::System.UInt32", "uint", "reader.GetUInt32()", signed: false, "int64"),
        Integral(UnderlyingKind.Int64, "global::System.Int64", "long", "reader.GetInt64()", signed: true, "int64"),
        Integral(UnderlyingKind.UInt64, "global::System.UInt64", "ulong", "reader.GetUInt64()", signed: false, "int64"),
        // 128-bit integers travel as JSON strings: no JSON consumer can hold them in a number without losing
        // precision, and every JSON number reader in System.Text.Json tops out at 64 bits.
        new(UnderlyingKind.Int128, "global::System.Int128", "System.Int128")
        {
            IsNumeric = true, IsSigned = true, JsonReadExpression = "ReadInt128(ref reader)", FormatBufferSize = 40,
        },
        new(UnderlyingKind.UInt128, "global::System.UInt128", "System.UInt128")
        {
            IsNumeric = true, JsonReadExpression = "ReadUInt128(ref reader)", FormatBufferSize = 40,
        },
        new(UnderlyingKind.Decimal, "global::System.Decimal", "decimal")
        {
            IsNumeric = true, IsSigned = true, IsJsonNumber = true,
            JsonReadExpression = "reader.GetDecimal()", SchemaType = "number", SchemaFormat = "decimal",
        },
        new(UnderlyingKind.Double, "global::System.Double", "double")
        {
            IsNumeric = true, IsSigned = true, IsJsonNumber = true,
            JsonReadExpression = "reader.GetDouble()", SchemaType = "number", SchemaFormat = "double",
        },
        new(UnderlyingKind.Single, "global::System.Single", "float")
        {
            IsNumeric = true, IsSigned = true, IsJsonNumber = true,
            JsonReadExpression = "reader.GetSingle()", SchemaType = "number", SchemaFormat = "float",
        },
        new(UnderlyingKind.DateOnly, "global::System.DateOnly", "System.DateOnly")
        {
            JsonReadExpression = "ReadDateOnly(ref reader)", RoundTripFormat = "O", SchemaFormat = "date", FormatBufferSize = 10,
        },
        new(UnderlyingKind.TimeOnly, "global::System.TimeOnly", "System.TimeOnly")
        {
            JsonReadExpression = "ReadTimeOnly(ref reader)", RoundTripFormat = "O", SchemaFormat = "time", FormatBufferSize = 16,
        },
        new(UnderlyingKind.DateTime, "global::System.DateTime", "System.DateTime")
        {
            JsonReadExpression = "reader.GetDateTime()", RoundTripFormat = "O", SchemaFormat = "date-time", FormatBufferSize = 33,
        },
        new(UnderlyingKind.DateTimeOffset, "global::System.DateTimeOffset", "System.DateTimeOffset")
        {
            JsonReadExpression = "reader.GetDateTimeOffset()", RoundTripFormat = "O", SchemaFormat = "date-time", FormatBufferSize = 33,
        },
        new(UnderlyingKind.TimeSpan, "global::System.TimeSpan", "System.TimeSpan")
        {
            JsonReadExpression = "ReadTimeSpan(ref reader)", RoundTripFormat = "c", SchemaFormat = "duration", FormatBufferSize = 26,
        },
    ];

    private static readonly Dictionary<string, UnderlyingType> ByFullName = BuildIndex();

    /// <summary>
    /// Resolves the descriptor of a supported underlying type.
    /// </summary>
    /// <param name="globallyQualifiedName">Globally qualified name of the candidate type.</param>
    /// <param name="underlyingType">The descriptor when the type is supported.</param>
    /// <returns><see langword="true"/> when the type is supported.</returns>
    public static bool TryResolve(string globallyQualifiedName, [NotNullWhen(true)] out UnderlyingType? underlyingType)
        => ByFullName.TryGetValue(globallyQualifiedName, out underlyingType);

    /// <summary>
    /// Gets the names of every supported underlying type, for diagnostic messages.
    /// </summary>
    public static IEnumerable<string> SupportedNames
    {
        get
        {
            foreach (var type in All)
            {
                yield return type.Keyword;
            }
        }
    }

    private static UnderlyingType Integral(
        UnderlyingKind kind,
        string fullName,
        string keyword,
        string jsonReadExpression,
        bool signed,
        string? schemaFormat)
        => new(kind, fullName, keyword)
        {
            IsNumeric = true,
            IsSigned = signed,
            IsJsonNumber = true,
            JsonReadExpression = jsonReadExpression,
            SchemaType = "integer",
            SchemaFormat = schemaFormat,
            FormatBufferSize = 40,
        };

    private static Dictionary<string, UnderlyingType> BuildIndex()
    {
        var index = new Dictionary<string, UnderlyingType>(All.Length);
        foreach (var type in All)
        {
            index[type.FullName] = type;
        }

        return index;
    }
}
