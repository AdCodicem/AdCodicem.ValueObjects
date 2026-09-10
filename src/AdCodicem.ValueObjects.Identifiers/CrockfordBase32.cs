namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The Crockford Base32 alphabet, and the decoding that makes an entity identifier canonical.
/// </summary>
/// <remarks>
/// <para>
/// The alphabet is <c>0123456789abcdefghjkmnpqrstvwxyz</c>: the digits, then the letters with <c>i</c>,
/// <c>l</c>, <c>o</c> and <c>u</c> removed. Two properties are load bearing here.
/// </para>
/// <para>
/// It is <b>strictly increasing in ASCII</b>, so an ordinal comparison of two encoded values reproduces the
/// comparison of the numbers they encode. That is what lets the time bucket at the head of an identifier order
/// the database index chronologically without any decoding, under the ordinal comparison this library already
/// uses by default.
/// </para>
/// <para>
/// It is <b>case insensitive on input</b> and folds the confusable characters, so normalization produces a
/// single canonical spelling and a case-insensitive column collation can no longer collapse two distinct
/// identifiers into one.
/// </para>
/// </remarks>
public static class CrockfordBase32
{
    /// <summary>
    /// The encoding alphabet, indexed by the value it encodes.
    /// </summary>
    /// <remarks>
    /// Lower case is the canonical spelling, so an identifier is one unbroken lowercase token wherever it
    /// travels — a URL, a JSON body, a log line. Upper case still decodes; normalization folds it down.
    /// </remarks>
    public const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// The number of bits one symbol carries.
    /// </summary>
    public const int BitsPerSymbol = 5;

    /// <summary>
    /// Value of every ASCII character, or -1 when the character is not a symbol.
    /// </summary>
    /// <remarks>
    /// Built once as a flat 128-entry table so that decoding is an array index rather than a search. The
    /// aliases follow Crockford: <c>I</c> and <c>L</c> decode as one, <c>O</c> decodes as zero.
    /// </remarks>
    private static readonly sbyte[] DecodingTable = BuildDecodingTable();

    /// <summary>
    /// Encodes a five-bit value.
    /// </summary>
    /// <param name="value">Value to encode, from 0 to 31.</param>
    /// <returns>The symbol carrying <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> does not fit in five bits.</exception>
    public static char Encode(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 31);

        return Alphabet[value];
    }

    /// <summary>
    /// Decodes a symbol, accepting either case and the Crockford aliases.
    /// </summary>
    /// <param name="symbol">Character to decode.</param>
    /// <returns>The value from 0 to 31, or -1 when <paramref name="symbol"/> is not a symbol.</returns>
    public static int Decode(char symbol)
        => symbol < DecodingTable.Length ? DecodingTable[symbol] : -1;

    /// <summary>
    /// Determines whether a character decodes to a value.
    /// </summary>
    /// <param name="symbol">Character to test.</param>
    /// <returns><see langword="true"/> when <paramref name="symbol"/> is a symbol or one of its aliases.</returns>
    public static bool IsSymbol(char symbol) => Decode(symbol) >= 0;

    /// <summary>
    /// Rewrites a character into its canonical spelling.
    /// </summary>
    /// <param name="symbol">Character to canonicalize.</param>
    /// <returns>
    /// The canonical symbol, or <paramref name="symbol"/> itself when it decodes to nothing — normalization
    /// must never reject, so an unusable character is carried through for validation to refuse.
    /// </returns>
    public static char Canonicalize(char symbol)
    {
        var value = Decode(symbol);

        return value < 0 ? symbol : Alphabet[value];
    }

    private static sbyte[] BuildDecodingTable()
    {
        var table = new sbyte[128];
        table.AsSpan().Fill(-1);

        // Both cases are registered explicitly rather than off the alphabet's own casing, so that flipping
        // which case is canonical stays a one-line change here.
        for (var value = 0; value < Alphabet.Length; value++)
        {
            var symbol = Alphabet[value];
            table[char.ToLowerInvariant(symbol)] = (sbyte)value;
            table[char.ToUpperInvariant(symbol)] = (sbyte)value;
        }

        // Crockford folds the characters a reader confuses with a digit, in both cases.
        foreach (var (alias, value) in new[] { ('i', 1), ('l', 1), ('o', 0) })
        {
            table[char.ToLowerInvariant(alias)] = (sbyte)value;
            table[char.ToUpperInvariant(alias)] = (sbyte)value;
        }

        return table;
    }
}
