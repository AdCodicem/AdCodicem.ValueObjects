namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// Which operations <c>AddValueObjectFiltering()</c> gives the value objects of one schema.
/// </summary>
/// <remarks>
/// Every call of <c>AddValueObjectFiltering()</c> on one schema configures these same options, in the order of the
/// calls; they are read when the schema is built.
/// </remarks>
public sealed class ValueObjectFilteringOptions
{
    /// <summary>
    /// Gets whether the value objects over <see cref="string"/> get the string operations over their underlying text.
    /// </summary>
    internal bool TextOperations { get; private set; }

    /// <summary>
    /// Adds <c>contains</c>, <c>startsWith</c> and <c>endsWith</c>, and their negations, over the underlying text of every
    /// value object over <see cref="string"/>.
    /// </summary>
    /// <returns>These options, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The operand of a string operation is a <c>String</c>, never the value object: a fragment such as <c>FR</c> is no
    /// IBAN. It is compared as typed, never normalized, with the value object's underlying text, which the database
    /// compares under its column's collation. A <see langword="null"/> fragment is refused as HotChocolate refuses one for a
    /// <c>String</c> member.
    /// </para>
    /// <para>
    /// Entity Framework Core translates them only under <c>UseValueObjectQueries()</c> of
    /// AdCodicem.ValueObjects.EntityFrameworkCore, which translates the text of a value object to its column; without it,
    /// each fails at its first query, never at start. Their handlers reach the default filter convention, which
    /// <c>AddFiltering()</c> registers, and which <c>AddValueObjectFiltering()</c> must therefore follow: a named
    /// convention keeps the comparable operations alone.
    /// </para>
    /// </remarks>
    public ValueObjectFilteringOptions IncludeTextOperations()
    {
        TextOperations = true;
        return this;
    }
}
