using System.Collections.Frozen;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Tells whether a closed set of text stores its known values as they are spelled, which a check written from the known
/// values alone and compared exactly needs: the MongoDB package links this file for the <c>enum</c> of its
/// <c>$jsonSchema</c> validator, and the Entity Framework Core package for the <c>IN</c> of its CHECK constraints.
/// </summary>
internal static class KnownValueSpellings
{
    /// <summary>
    /// Tells whether a closed set of text stores no other spelling of a known value, by the comparison its schema states:
    /// <see cref="StringComparison.Ordinal"/> stores none; a culture's comparison may store one of any length, and is
    /// never trusted; <see cref="StringComparison.OrdinalIgnoreCase"/>, and a schema stating no comparison, are tried
    /// with the other spellings of each known value: neither its upper nor its lower case, nor the value with one of its
    /// characters replaced by another that shares its upper case, which a set looked up ignoring case accepts, nor the
    /// value followed by a soft hyphen, which a culture's comparison ignores, may be stored as it is.
    /// </summary>
    /// <typeparam name="TSelf">Value object type, over text.</typeparam>
    /// <typeparam name="TValue">Underlying value type, <see cref="string"/>.</typeparam>
    /// <param name="schema">The rules, whose comparison and known values are read.</param>
    /// <returns>
    /// <see langword="true"/> when every other spelling the type accepts is stored as one of its known values.
    /// </returns>
    public static bool KeepsSpelling<TSelf, TValue>(ValueObjectSchema schema)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        switch (schema.Comparison)
        {
            case StringComparison.Ordinal:
                // The lookup finds a normalized value only spelled as a known value, and the type stores that value.
                return true;
            case StringComparison.OrdinalIgnoreCase or null:
                break;
            default:
                // A culture's comparison takes spellings of any length for one another, an ignorable character inserted,
                // a modifier letter for its letter, the Kelvin sign for K, which no list of spellings exhausts.
                return false;
        }

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
    /// Spells a known value each other way a set looked up ignoring case may take it, and followed by a soft hyphen,
    /// which a culture's comparison ignores, for a schema that states no comparison.
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
}
