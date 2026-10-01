using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// <see cref="ValueObjectRegistry.TryResolve"/>, the lookup that falls back to reflection for what did not
/// register itself.
/// </summary>
public class RegistryResolutionTests
{
    /// <summary>
    /// Each of these implements the marker, so <see cref="ValueObjectRegistry.IsValueObject"/> claims it, and none
    /// is a struct implementing <see cref="IValueObject{TSelf, TValue}"/> over itself, which is what a descriptor
    /// is built from. A Try method answers that with <see langword="false"/>, not with an exception.
    /// </summary>
    [Theory]
    [InlineData(typeof(IValueObject))]
    [InlineData(typeof(IValueObject<string>))]
    [InlineData(typeof(IEntityId))]
    [InlineData(typeof(MarkerOnlyValue))]
    [InlineData(typeof(ClassBackedValue))]
    [InlineData(typeof(SelflessValue))]
    public void A_type_no_descriptor_can_describe_resolves_to_none(Type type)
    {
        ValueObjectRegistry.IsValueObject(type).Should().BeTrue();

        ValueObjectRegistry.TryResolve(type, out var descriptor).Should().BeFalse();
        descriptor.Should().BeNull();
    }
}
