namespace AdCodicem.ValueObjects.Fixtures.WithoutGenerator;

/// <summary>
/// A generic attribute of the consumer's own, which a reader of the annotations must look past to find
/// <c>[ValueObject&lt;T&gt;]</c> among the attributes of a type.
/// </summary>
/// <typeparam name="T">The type that was reviewed.</typeparam>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class ReviewedAttribute<T> : Attribute;
