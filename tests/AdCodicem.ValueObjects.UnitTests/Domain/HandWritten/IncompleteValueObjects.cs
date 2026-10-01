namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

// Types that claim to be value objects without carrying the contract that describes one: each implements the
// marker, so IsValueObject answers yes, and none implements IValueObject<TSelf, TValue> over itself, so no
// descriptor, converter or binder can be built for it.

/// <summary>
/// A struct carrying only the non-generic marker.
/// </summary>
public readonly struct MarkerOnlyValue : IValueObject
{
    public object? GetBoxedValue() => null;
}

/// <summary>
/// A class carrying a value, which the self-referencing contract rules out: a value object is a struct.
/// </summary>
public sealed class ClassBackedValue : IValueObject<string>
{
    public string Value => "class";
}

/// <summary>
/// A struct carrying a value, without the self-referencing contract that makes it constructible.
/// </summary>
public readonly struct SelflessValue : IValueObject<string>
{
    public string Value => "selfless";
}
