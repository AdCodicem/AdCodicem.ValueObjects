using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Hands back the type arguments a descriptor gives it, which is all a visitor receives.
/// </summary>
public sealed class TypeArgumentsVisitor : IValueObjectVisitor<(Type Self, Type Value)>
{
    public static TypeArgumentsVisitor Instance { get; } = new();

    public (Type Self, Type Value) Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => (typeof(TSelf), typeof(TValue));
}

/// <summary>
/// Reads the schema off the type a descriptor hands over, as an adapter closed over it does.
/// </summary>
public sealed class SchemaVisitor : IValueObjectVisitor<ValueObjectSchema>
{
    public static SchemaVisitor Instance { get; } = new();

    public ValueObjectSchema Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => TSelf.Schema;
}

/// <summary>
/// Closes an adapter over the type a descriptor hands over, as an integration does where it would otherwise call
/// <see cref="Type.MakeGenericType(Type[])"/>; covariance lets it answer where a visitor of a base type of
/// <see cref="TypedAdapter"/> is asked for.
/// </summary>
public sealed class AdapterVisitor : IValueObjectVisitor<TypedAdapter>
{
    public static AdapterVisitor Instance { get; } = new();

    public TypedAdapter Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => new TypedAdapter<TSelf, TValue>();
}

/// <summary>
/// What an integration closes over a value object: here, something that parses text and reads the declared length.
/// </summary>
public abstract class TypedAdapter
{
    public abstract int? MaxLength { get; }

    public abstract object? Parse(string text);
}

/// <inheritdoc />
public sealed class TypedAdapter<TSelf, TValue> : TypedAdapter
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>Gets the length the value object declares, read off its type parameter, with no registry.</summary>
    public override int? MaxLength => TSelf.Schema.MaxLength;

    public override object? Parse(string text)
        => TSelf.TryParse(text, null, out var parsed, out _) ? parsed : null;
}
