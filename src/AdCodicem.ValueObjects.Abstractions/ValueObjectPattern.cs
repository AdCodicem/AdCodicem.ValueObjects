using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AdCodicem.ValueObjects;

/// <summary>
/// A generic bridge to the pattern a value object declares through <see cref="IValueObjectPatternValidator"/>,
/// used by generated code.
/// </summary>
/// <remarks>
/// A static abstract member is reached through a type parameter, which finds it however the value object implements
/// it. Named on the type, <c>Iban.Pattern</c> would miss an explicit implementation and fail to compile in a file the
/// consumer cannot edit. The JIT specializes the call for the struct, so the indirection costs nothing.
/// </remarks>
public static class ValueObjectPattern
{
    /// <summary>
    /// Gets the regular expression a value object declares.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <returns>Its pattern.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Regex Of<TSelf>()
        where TSelf : IValueObjectPatternValidator
        => TSelf.Pattern;
}
