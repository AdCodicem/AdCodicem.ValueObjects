using System.Runtime.CompilerServices;

namespace AdCodicem.ValueObjects;

/// <summary>
/// A generic bridge to the example a value object declares through <see cref="IValueObjectExample{TSelf}"/>, used by
/// generated code.
/// </summary>
/// <remarks>
/// A static abstract member is reached through a type parameter, which finds it however the value object implements
/// it. Named on the type, <c>CountryCode.Example</c> would miss an explicit implementation and fail to compile in a file
/// the consumer cannot edit.
/// </remarks>
public static class ValueObjectExample
{
    /// <summary>
    /// Gets the example a value object declares.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <returns>Its example.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSelf Of<TSelf>()
        where TSelf : IValueObjectExample<TSelf>
        => TSelf.Example;
}
