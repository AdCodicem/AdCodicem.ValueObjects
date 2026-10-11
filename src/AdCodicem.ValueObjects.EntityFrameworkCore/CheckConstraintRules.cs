using System.Collections.Frozen;
using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Writes the rules of one value object as the SQL of the CHECK constraints of a column, each only where the database
/// refuses no value the value object accepts.
/// </summary>
/// <remarks>
/// <para>
/// A constraint may be laxer than the type, never stricter: a stricter one would fail a write the application validated.
/// So a length is counted in bytes where the database cannot count UTF-16 code units, which .NET counts, since a
/// character never takes fewer bytes than code units: exact for ASCII, laxer for other text. A bound or a known value is
/// written as the provider writes a literal of the underlying type, and left out where the column does not order or
/// compare values as .NET does: text that does not sort as numbers or instants, a column that rounds the bound away, a
/// value SQL Server's <c>time</c> cannot hold. A closed set of text is written only when the type stores no other
/// spelling of a known value, which a set looked up ignoring case, or as a culture compares, would.
/// </para>
/// <para>
/// The rules are <c>TSelf.Schema</c>, which <c>Validate</c> enforces, not the schema a registration made by hand may
/// give the descriptor, which only sizes the column.
/// </para>
/// </remarks>
/// <param name="engine">The database.</param>
/// <param name="rules">The rules written.</param>
/// <param name="mappings">The type mappings of the provider.</param>
/// <param name="property">The property whose column is constrained.</param>
/// <param name="column">The column, delimited.</param>
internal sealed class CheckConstraintRules(
    CheckEngine engine,
    ValueObjectChecks rules,
    IRelationalTypeMappingSource mappings,
    IReadOnlyProperty property,
    string column) : IValueObjectVisitor<IReadOnlyList<(string Rule, string Sql)>>
{
    /// <summary>
    /// The underlying types a bound is read into, from the invariant text the schema holds it in, each as its own parser
    /// reads it.
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
        [typeof(DateOnly)] = Parse<DateOnly>,
        [typeof(TimeOnly)] = Parse<TimeOnly>,
        [typeof(DateTimeOffset)] = Parse<DateTimeOffset>,
        [typeof(TimeSpan)] = Parse<TimeSpan>,
        [typeof(DateTime)] = static text
            => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) ? instant : null,
    }.ToFrozenDictionary();

    /// <summary>
    /// Writes the checks of the column.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <returns>Each rule written, with its SQL, the column not yet allowed its <c>NULL</c>.</returns>
    public IReadOnlyList<(string Rule, string Sql)> Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var schema = TSelf.Schema;
        var checks = new List<(string Rule, string Sql)>();
        if (mappings.FindMapping(
                typeof(TValue),
                property.GetColumnType(),
                keyOrIndex: false,
                unicode: property.IsUnicode(),
                size: property.GetMaxLength(),
                rowVersion: null,
                fixedLength: property.IsFixedLength(),
                precision: property.GetPrecision(),
                scale: property.GetScale()) is not { } mapping)
        {
            return checks;
        }

        if ((rules & ValueObjectChecks.Lengths) != 0 && typeof(TValue) == typeof(string) && schema.MinLength is > 0 and var minimum)
        {
            var length = engine switch
            {
                CheckEngine.SqlServer => mapping.IsUnicode ? $"DATALENGTH({column}) / 2" : $"DATALENGTH({column})",
                CheckEngine.PostgreSql => $"octet_length({column})",
                _ => $"length(CAST({column} AS BLOB))",
            };
            checks.Add(("MinLength", string.Create(CultureInfo.InvariantCulture, $"{length} >= {minimum}")));
        }

        if ((rules & ValueObjectChecks.Bounds) != 0 && BoundParsers.TryGetValue(typeof(TValue), out var parse))
        {
            if (schema.Minimum is { } low && parse(low) is { } lower && Bound(lower, minimum: true, mapping) is { } literal)
            {
                checks.Add(("Minimum", $"{Compared<TValue>()} >= {literal}"));
            }

            if (schema.Maximum is { } high && parse(high) is { } upper && Bound(upper, minimum: false, mapping) is { } bound)
            {
                checks.Add(("Maximum", $"{Compared<TValue>()} <= {bound}"));
            }
        }

        if ((rules & ValueObjectChecks.KnownValues) != 0
            && schema.IsClosedValueSet
            && !schema.KnownValues.IsDefaultOrEmpty
            && (typeof(TValue) != typeof(string) || KeepsSpelling<TSelf, TValue>(schema))
            && Literals<TValue>(schema, mapping) is { } values)
        {
            checks.Add(("KnownValues", $"{column} IN ({values})"));
        }

        return checks;
    }

    /// <summary>
    /// Reads a bound as the type's own parser reads its invariant text.
    /// </summary>
    private static object? Parse<T>(string text)
        where T : IParsable<T>
        => T.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Tells whether a closed set of text stores no other spelling of a known value: neither its upper nor its lower case,
    /// nor the value with one of its characters replaced by another that shares its upper case, which a set looked up
    /// ignoring case accepts, nor the value followed by a soft hyphen, which a culture's comparison ignores.
    /// </summary>
    private static bool KeepsSpelling<TSelf, TValue>(ValueObjectSchema schema)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var known = schema.KnownValues.OfType<string>().ToFrozenSet(StringComparer.Ordinal);
        foreach (var text in known)
        {
            foreach (var variant in Spellings(text))
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
    /// Spells a known value each other way a set looked up ignoring case, or as a culture compares, may take it.
    /// </summary>
    private static IEnumerable<string> Spellings(string text)
    {
        yield return text.ToUpperInvariant();
        yield return text.ToLowerInvariant();
        yield return text + (char)0x00AD;
        for (var index = 0; index < text.Length; index++)
        {
            if (!SharedCases.Others.TryGetValue(text[index], out var others))
            {
                continue;
            }

            foreach (var other in others)
            {
                yield return string.Create(text.Length, (text, index, other), static (span, state) =>
                {
                    state.text.CopyTo(span);
                    span[state.index] = state.other;
                });
            }
        }
    }

    /// <summary>
    /// The characters of the Basic Multilingual Plane that share their upper case with another, each with the others,
    /// which a comparison ignoring case takes for it: the micro sign <c>µ</c> for the Greek <c>μ</c>, the final
    /// <c>ς</c> for <c>σ</c>, which the upper and the lower case of a whole value never reach. A character outside the
    /// plane has one other case at most, which they reach.
    /// </summary>
    private static class SharedCases
    {
        public static readonly FrozenDictionary<char, char[]> Others = Build();

        private static FrozenDictionary<char, char[]> Build()
        {
            var groups = new Dictionary<char, List<char>>();
            for (var code = 0; code <= char.MaxValue; code++)
            {
                var character = (char)code;
                if (char.IsSurrogate(character))
                {
                    continue;
                }

                var upper = char.ToUpperInvariant(character);
                if (!groups.TryGetValue(upper, out var group))
                {
                    groups.Add(upper, group = []);
                }

                group.Add(character);
            }

            return groups.Values
                .Where(static group => group.Count > 1)
                .SelectMany(static group => group.Select(character => (character, others: group.Where(other => other != character).ToArray())))
                .ToFrozenDictionary(static pair => pair.character, static pair => pair.others);
        }
    }

    /// <summary>
    /// Writes the column a bound compares with: a character under a binary collation, so that characters compare by
    /// code point, as .NET compares them, and not in the order a language sorts them.
    /// </summary>
    private string Compared<TValue>()
        => typeof(TValue) != typeof(char)
            ? column
            : engine switch
            {
                CheckEngine.SqlServer => $"{column} COLLATE Latin1_General_BIN2",
                CheckEngine.PostgreSql => $"{column} COLLATE \"C\"",
                _ => $"{column} COLLATE BINARY",
            };

    /// <summary>
    /// Writes a bound, where the column orders values as .NET does.
    /// </summary>
    /// <returns>The literal, or <see langword="null"/> when the bound is left out.</returns>
    private string? Bound(object bound, bool minimum, RelationalTypeMapping mapping)
        => bound switch
        {
            // SQLite stores a decimal, a duration and an instant with its offset as text, which does not sort as they do,
            // and an unsigned 64-bit integer past long.MaxValue as a negative one.
            decimal or TimeSpan or DateTimeOffset or ulong when engine == CheckEngine.Sqlite => null,

            // PostgreSQL's character(1) ignores a trailing space, so a space compares as nothing, below any minimum.
            char character when minimum && engine == CheckEngine.PostgreSql && character <= ' ' => null,
            _ => Literal(bound, mapping),
        };

    /// <summary>
    /// Writes every known value of a closed set, or none.
    /// </summary>
    /// <returns>The literals, separated by commas, or <see langword="null"/> when one cannot be written.</returns>
    private string? Literals<TValue>(ValueObjectSchema schema, RelationalTypeMapping mapping)
    {
        var literals = new List<string>(schema.KnownValues.Length);
        foreach (var known in schema.KnownValues)
        {
            if (known is not TValue || Literal(known, mapping) is not { } literal)
            {
                return null;
            }

            literals.Add(literal);
        }

        return string.Join(", ", literals);
    }

    /// <summary>
    /// Writes a value as the provider writes a literal of the column, when the column keeps it as it is.
    /// </summary>
    /// <returns>The literal, or <see langword="null"/> when the column could store an equal value apart from it.</returns>
    private string? Literal(object value, RelationalTypeMapping mapping)
    {
        if (!Kept(value, mapping))
        {
            return null;
        }

        return value switch
        {
            // A float column holds the double nearest the float, which the float's own shortest text is not.
            float single => mappings.FindMapping(typeof(double))!.GenerateSqlLiteral((double)single),

            // PostgreSQL's timestamp with time zone holds an instant, which Npgsql writes from a UTC DateTime only, and the
            // application writes in UTC: the clock reading of the bound, read as UTC, is the instant it compares with.
            DateTime instant when engine == CheckEngine.PostgreSql && mapping.StoreTypeNameBase == "timestamp with time zone"
                => mapping.GenerateSqlLiteral(DateTime.SpecifyKind(instant, DateTimeKind.Utc)),
            DateTimeOffset instant when engine == CheckEngine.PostgreSql => mapping.GenerateSqlLiteral(instant.ToUniversalTime()),
            _ => mapping.GenerateSqlLiteral(value),
        };
    }

    /// <summary>
    /// Tells whether the column stores a value as .NET holds it, so that a check against it compares what .NET does.
    /// </summary>
    private bool Kept(object value, RelationalTypeMapping mapping)
        => value switch
        {
            float or double => double.IsFinite(Convert.ToDouble(value, CultureInfo.InvariantCulture)),

            // A decimal column rounds a value to its scale: a bound or a value finer than it would be compared with what
            // the column made of the application's own write.
            decimal number => mapping.Scale is not { } scale || decimal.Round(number, scale) == number,

            // SQLite stores an instant with its offset as text, which two equal instants of two offsets do not share.
            DateTimeOffset when engine == CheckEngine.Sqlite => false,

            // SQL Server's time holds a duration of at least nothing and less than a day, and writes the literal of any
            // other without its days.
            TimeSpan duration when engine == CheckEngine.SqlServer && (duration < TimeSpan.Zero || duration >= TimeSpan.FromDays(1)) => false,
            TimeOnly or DateTime or DateTimeOffset or TimeSpan => KeepsTicks(value, mapping),
            _ => true,
        };

    /// <summary>
    /// Tells whether the column keeps every fraction of a second of a date or a time: 100 nanoseconds in .NET,
    /// <c>datetime2</c>, <c>datetimeoffset</c> and <c>time</c> on SQL Server unless their precision keeps less, a
    /// microsecond on PostgreSQL, all of them in SQLite's text. SQL Server's <c>datetime</c> and <c>smalldatetime</c>
    /// round to fractions no tick count states.
    /// </summary>
    private bool KeepsTicks(object value, RelationalTypeMapping mapping)
    {
        int? digits = engine switch
        {
            CheckEngine.SqlServer when mapping.StoreTypeNameBase is "datetime" or "smalldatetime" => null,
            CheckEngine.SqlServer => mapping.Precision ?? 7,
            CheckEngine.PostgreSql => Math.Min(mapping.Precision ?? 6, 6),
            _ => 7,
        };
        if (digits is not { } kept)
        {
            return false;
        }

        var ticks = value switch
        {
            TimeOnly time => time.Ticks,
            DateTime instant => instant.Ticks,
            DateTimeOffset instant => instant.Ticks,
            _ => ((TimeSpan)value).Ticks,
        };

        return ticks % (long)Math.Pow(10, 7 - Math.Clamp(kept, 0, 7)) == 0;
    }
}
