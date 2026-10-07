using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Writes the rules of one value object as the keywords of a <c>$jsonSchema</c>, each only where the server refuses no
/// value the value object accepts.
/// </summary>
/// <remarks>
/// The rules are those the type declares, <c>TSelf.Schema</c>, which its <c>Validate</c> enforces, applied to what the
/// serializer of the underlying value stores: the BSON type it writes, a known value and a bound as it writes them.
/// </remarks>
internal static class ValueObjectBsonRules
{
    /// <summary>The options a pattern keeps its meaning under once read with none.</summary>
    private const RegexOptions PortableOptions =
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking;

    /// <summary>
    /// The underlying types whose equality is the identity of the value: two that compare equal are written alike.
    /// </summary>
    private static readonly FrozenSet<Type> ExactlyEqual = new[]
    {
        typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(Guid), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan),
    }.ToFrozenSet();

    /// <summary>
    /// The underlying types a bound is read into, from the invariant text the schema holds it in, each as its own
    /// parser reads it.
    /// </summary>
    private static readonly FrozenDictionary<Type, Func<string, object?>> BoundParsers = new Dictionary<Type, Func<string, object?>>
    {
        [typeof(char)] = Parse<char>,
        [typeof(sbyte)] = Parse<sbyte>,
        [typeof(byte)] = Parse<byte>,
        [typeof(short)] = Parse<short>,
        [typeof(ushort)] = Parse<ushort>,
        [typeof(int)] = Parse<int>,
        [typeof(uint)] = Parse<uint>,
        [typeof(long)] = Parse<long>,
        [typeof(ulong)] = Parse<ulong>,
        [typeof(float)] = Parse<float>,
        [typeof(double)] = Parse<double>,
        [typeof(decimal)] = Parse<decimal>,
        [typeof(TimeOnly)] = Parse<TimeOnly>,
        [typeof(TimeSpan)] = Parse<TimeSpan>,
    }.ToFrozenDictionary();

    /// <summary>
    /// The names <c>bsonType</c> gives the BSON types a value object's serializer may write.
    /// </summary>
    private static readonly FrozenDictionary<BsonType, string> Aliases = new Dictionary<BsonType, string>
    {
        [BsonType.Double] = "double",
        [BsonType.String] = "string",
        [BsonType.Document] = "object",
        [BsonType.Array] = "array",
        [BsonType.Binary] = "binData",
        [BsonType.ObjectId] = "objectId",
        [BsonType.Boolean] = "bool",
        [BsonType.DateTime] = "date",
        [BsonType.Int32] = "int",
        [BsonType.Int64] = "long",
        [BsonType.Decimal128] = "decimal",
    }.ToFrozenDictionary();

    /// <summary>
    /// Writes the rules of a value object as <c>$jsonSchema</c> keywords.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="schema">The rules, <c>TSelf.Schema</c>.</param>
    /// <param name="valueSerializer">The serializer of the underlying value, which stores it.</param>
    /// <returns>
    /// The keywords: <c>bsonType</c>, the lengths and the pattern of text, the bounds of a number, the values of a closed
    /// set, the description.
    /// </returns>
    public static BsonDocument Describe<TSelf, TValue>(ValueObjectSchema schema, IBsonSerializer<TValue> valueSerializer)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var representation = valueSerializer is IHasRepresentationSerializer represented
            ? represented.Representation
            : BsonType.Undefined;

        var rules = new BsonDocument();
        if (Aliases.TryGetValue(representation, out var alias))
        {
            rules["bsonType"] = alias;
        }

        if (representation == BsonType.String && typeof(TValue) == typeof(string))
        {
            DescribeText<TSelf>(schema, rules);
        }

        if (Bound(schema.Minimum, valueSerializer) is { } minimum)
        {
            rules["minimum"] = minimum;
        }

        if (Bound(schema.Maximum, valueSerializer) is { } maximum)
        {
            rules["maximum"] = maximum;
        }

        if (Enumeration<TSelf, TValue>(schema, valueSerializer, representation) is { } values)
        {
            rules["enum"] = values;
        }

        if (schema.Description is { } description)
        {
            rules["description"] = description;
        }

        return rules;
    }

    /// <summary>
    /// Writes the lengths and the pattern of a value object over text, stored as text.
    /// </summary>
    /// <remarks>
    /// The server counts a length in code points, .NET in UTF-16 code units, two for a character outside the Basic
    /// Multilingual Plane, such as an emoji. Its <c>maxLength</c> is therefore never stricter than .NET's, and its
    /// <c>minLength</c> is the declared one only when the published pattern keeps every value in that plane; otherwise
    /// it is half the declared one, rounded up, the fewest code points a value of that many code units holds.
    /// </remarks>
    private static void DescribeText<TSelf>(ValueObjectSchema schema, BsonDocument rules)
    {
        string? pcre = null;
        var basicPlaneOnly = false;
        var published = schema.Pattern is { } pattern
            && HasPortableOptions<TSelf>()
            && PcrePattern.TryWrite(pattern, out pcre, out basicPlaneOnly);

        if (schema.MinLength is { } minLength)
        {
            rules["minLength"] = published && basicPlaneOnly ? minLength : (minLength / 2) + (minLength % 2);
        }

        if (schema.MaxLength is { } maxLength)
        {
            rules["maxLength"] = maxLength;
        }

        if (published)
        {
            rules["pattern"] = pcre;
        }
    }

    /// <summary>
    /// Tells whether the regular expression behind <see cref="IValueObjectPatternValidator"/>, if the type has one, sets
    /// no option that changes what its text matches, as <c>IgnoreCase</c> does, which a pattern published alone drops.
    /// </summary>
    private static bool HasPortableOptions<TSelf>()
    {
        if (!typeof(TSelf).IsAssignableTo(typeof(IValueObjectPatternValidator)))
        {
            return true;
        }

        // The hook is a static abstract member, which only a type parameter constrained to the interface reaches; the
        // interface map finds it, implicit or explicit.
        var getter = typeof(TSelf).GetInterfaceMap(typeof(IValueObjectPatternValidator)).TargetMethods.Single();
        var regex = (Regex)getter.Invoke(null, null)!;

        return (regex.Options & ~PortableOptions) == RegexOptions.None;
    }

    /// <summary>
    /// Writes a bound as the serializer of the underlying value writes the bound itself, when that is a number the server
    /// compares stored numbers with in the same order.
    /// </summary>
    /// <remarks>
    /// Only MongoDB.Bson's own serializers are known to keep the order, and only when they refuse to overflow, as they do
    /// unless <c>AllowOverflow</c> is set: an overflow wraps a value around. A <see cref="TimeOnly"/> or a
    /// <see cref="TimeSpan"/> written as an Int32 is wrapped around too. A bound that does not fit what the serializer
    /// writes, or written as text or a date, which <c>minimum</c> and <c>maximum</c> do not compare, is left out.
    /// </remarks>
    private static BsonValue? Bound<TValue>(string? text, IBsonSerializer<TValue> valueSerializer)
    {
        if (text is null
            || valueSerializer.GetType().Assembly != typeof(BsonSerializer).Assembly
            || valueSerializer is IRepresentationConverterConfigurable { Converter.AllowOverflow: true }
            || !BoundParsers.TryGetValue(typeof(TValue), out var parse)
            || parse(text) is not TValue bound)
        {
            return null;
        }

        var written = Write(valueSerializer, bound);
        var wrapsAsInt32 = typeof(TValue) == typeof(TimeOnly) || typeof(TValue) == typeof(TimeSpan);

        return written is BsonInt64 or BsonDouble || (written is BsonInt32 or BsonDecimal128 && !wrapsAsInt32) ? written : null;
    }

    /// <summary>
    /// Writes the values of a closed set as the serializer of the underlying value writes them, when every value the
    /// type accepts is stored as one of them.
    /// </summary>
    /// <remarks>
    /// The server compares an <c>enum</c> exactly, which holds the type's accepted values only when two values the type
    /// holds equal are written alike: for text, when the set is not looked up ignoring case or as a culture compares, which
    /// the type would store as spelled; for a real or a decimal, when it is stored as a number, since <c>-0</c> and
    /// <c>0</c>, or <c>1.0</c> and <c>1.00</c>, are written apart as text; never for a date, whose kind or offset
    /// equality ignores, or for a type the table does not know. Every value has to be written, or none is.
    /// </remarks>
    private static BsonArray? Enumeration<TSelf, TValue>(ValueObjectSchema schema, IBsonSerializer<TValue> valueSerializer, BsonType representation)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (!schema.IsClosedValueSet
            || schema.KnownValues.IsDefaultOrEmpty
            || !WrittenAlikeWhenEqual<TSelf, TValue>(schema, representation))
        {
            return null;
        }

        var values = new BsonArray();
        foreach (var known in schema.KnownValues)
        {
            if (known is not TValue value || Write(valueSerializer, value) is not { } written)
            {
                return null;
            }

            values.Add(written);
        }

        return values;
    }

    /// <summary>
    /// Tells whether two values the type holds equal are written alike, so that every value it accepts is stored as one
    /// of its known values.
    /// </summary>
    private static bool WrittenAlikeWhenEqual<TSelf, TValue>(ValueObjectSchema schema, BsonType representation)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (typeof(TValue) == typeof(float) || typeof(TValue) == typeof(double) || typeof(TValue) == typeof(decimal))
        {
            return representation is BsonType.Int32 or BsonType.Int64 or BsonType.Double or BsonType.Decimal128;
        }

        return typeof(TValue) == typeof(string) ? KeepsSpelling<TSelf, TValue>(schema) : ExactlyEqual.Contains(typeof(TValue));
    }

    /// <summary>
    /// Tells whether a closed set of text stores no other spelling of a known value: neither its upper nor its lower case,
    /// which a set looked up ignoring case accepts, nor the value followed by a soft hyphen, which a culture's comparison
    /// ignores.
    /// </summary>
    private static bool KeepsSpelling<TSelf, TValue>(ValueObjectSchema schema)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var known = schema.KnownValues.OfType<string>().ToFrozenSet(StringComparer.Ordinal);
        foreach (var text in known)
        {
            foreach (var variant in (string[])[text.ToUpperInvariant(), text.ToLowerInvariant(), text + (char)0x00AD])
            {
                if (TSelf.TryCreate((TValue)(object)variant, out var created) && !known.Contains((string)(object)created.Value!))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Writes a value through a serializer, or tells that it cannot.
    /// </summary>
    /// <returns>The BSON value written, or <see langword="null"/> when the serializer refuses the value.</returns>
    private static BsonValue? Write<TValue>(IBsonSerializer<TValue> serializer, TValue value)
    {
        var document = new BsonDocument();
        try
        {
            using var writer = new BsonDocumentWriter(document);
            writer.WriteStartDocument();
            writer.WriteName("v");
            serializer.Serialize(BsonSerializationContext.CreateRoot(writer), value);
            writer.WriteEndDocument();
        }
        catch (Exception exception) when (exception is OverflowException or TruncationException or BsonSerializationException)
        {
            return null;
        }

        return document["v"];
    }

    /// <summary>
    /// Reads a bound as the type's own parser reads its invariant text.
    /// </summary>
    private static object? Parse<T>(string text)
        where T : IParsable<T>
        => T.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : null;
}
