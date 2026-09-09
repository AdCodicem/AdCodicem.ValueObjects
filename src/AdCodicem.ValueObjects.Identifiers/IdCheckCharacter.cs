namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The trailing check character of an entity identifier.
/// </summary>
/// <remarks>
/// <para>
/// The character is <c>(seed(prefix) + Σᵢ wᵢ · vᵢ) mod 32</c>, where <c>vᵢ</c> is the Crockford value of the
/// i-th data character of the body and <c>wᵢ = 2·(i mod 16) + 1</c>. Its purpose is to turn a mistyped or
/// truncated identifier into a rejection at the boundary — offline, without a database round trip — rather than
/// into a lookup that misses, or worse, one that hits something else.
/// </para>
/// <para>
/// What it guarantees, stated exactly, because a checksum that promises more than it delivers is worse than
/// none at all:
/// </para>
/// <list type="bullet">
/// <item>
/// every single-character substitution in the body is detected: the weights are odd, hence invertible modulo
/// 32, and a non-zero difference of Crockford values can never be congruent to zero modulo 32;
/// </item>
/// <item>
/// every adjacent transposition is detected unless the two characters' values differ by exactly 16 — the
/// weight difference between adjacent positions is congruent to 2 modulo 32 everywhere, the wrap included;
/// </item>
/// <item>
/// the prefix enters through the seed, so the same body under two different prefixes yields a different check
/// character for 31 prefixes out of 32. A body copied between two identifier types is therefore caught even by
/// a validator that does not yet know which prefix to expect, which is what <c>AnyEntityId</c> needs
/// before it has resolved anything;
/// </item>
/// <item>
/// random corruption slips through with probability 1/32. That is the information-theoretic limit of one check
/// character over a 32-symbol alphabet, and no scheme does better.
/// </item>
/// </list>
/// </remarks>
public static class IdCheckCharacter
{
    /// <summary>
    /// Computes the check character of a body.
    /// </summary>
    /// <param name="prefix">Declared prefix of the identifier type, without its trailing separator.</param>
    /// <param name="bodyData">
    /// The data characters of the body — the time bucket and the random part — with the check character
    /// excluded. Every character must decode; a character that does not contributes nothing, which is
    /// harmless because validation refuses it before the check character is ever consulted.
    /// </param>
    /// <returns>The check character.</returns>
    public static char Compute(ReadOnlySpan<char> prefix, ReadOnlySpan<char> bodyData)
    {
        var sum = SeedOf(prefix);

        for (var i = 0; i < bodyData.Length; i++)
        {
            var value = CrockfordBase32.Decode(bodyData[i]);
            if (value < 0)
            {
                continue;
            }

            sum += WeightAt(i) * value;
        }

        return CrockfordBase32.Encode(sum & 31);
    }

    /// <summary>
    /// The weight of a position.
    /// </summary>
    /// <remarks>
    /// Odd, so that it is invertible modulo 32 and no substitution can cancel itself out. The 16-position cycle
    /// keeps every adjacent step at a difference of 2 modulo 32, the wrap from 31 back to 1 included, so the
    /// transposition guarantee holds uniformly along the whole body.
    /// </remarks>
    /// <param name="index">Zero-based position in the body data.</param>
    /// <returns>The weight applied to that position.</returns>
    private static int WeightAt(int index) => (2 * (index % 16)) + 1;

    /// <summary>
    /// Folds the prefix into five bits.
    /// </summary>
    /// <remarks>
    /// FNV-1a, with the high half xored down before the truncation: the low bits of a raw FNV-1a hash carry
    /// little of the input, and the whole point of the seed is that two prefixes disagree.
    /// </remarks>
    /// <param name="prefix">Declared prefix.</param>
    /// <returns>A value from 0 to 31.</returns>
    private static int SeedOf(ReadOnlySpan<char> prefix)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var character in prefix)
        {
            hash ^= character;
            hash *= prime;
        }

        return (int)((hash ^ (hash >> 16)) & 31);
    }
}
