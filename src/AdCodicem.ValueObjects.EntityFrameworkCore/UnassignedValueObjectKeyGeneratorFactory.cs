using System.ComponentModel;
using AdCodicem.ValueObjects.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Creates the generator <c>ConfigureValueObjects</c> gives a primary key over a <see cref="Guid"/> value object that
/// nothing generates, which refuses an entity added without its key rather than let it be stored with
/// <see cref="Guid.Empty"/>.
/// </summary>
/// <remarks>
/// <para>
/// The convention gives it to the primary key of an entity type when that key is a single property, no foreign key, of
/// a value object over <see cref="Guid"/>, and nothing generates it: no value generation configured, no database
/// default, no generator of the application's own. An owned collection item with a key of its own is such an entity
/// type; an owned reference type, whose key is its owner's, is not. Entity Framework Core calls a key's generator when
/// an entity whose key is still at its default, <c>default(TSelf)</c>, which a value object holding
/// <see cref="Guid.Empty"/> equals, starts being tracked as added: in <c>Add</c>, and in <c>Attach</c>, <c>Update</c> or
/// <c>Remove</c>, which take such an entity for a new one, or as changes are detected, for one found through a
/// navigation. The generator throws an <see cref="InvalidOperationException"/> there, before anything is written, which
/// carries <see cref="ValueObjectErrorCodes.Required"/> in its <see cref="Exception.Data"/> under
/// <see cref="ValueObjectErrors.ErrorCodeKey"/>, where <see cref="ValueObjectErrors.TryGetCode"/> reads it. The key stays
/// <see cref="ValueGenerated.Never"/>, so neither the column nor a migration changes.
/// </para>
/// <para>
/// Entity Framework Core also takes a dependent whose key holds a foreign key to such a key, a dependent sharing its
/// principal's key or one keyed by its principal's key and a number, for a new one when that key is left at its default
/// for the navigation to fill: <c>Attach</c> or <c>Update</c> of an aggregate already stored then tracks the dependent as
/// added, as for a <see cref="Guid"/> key, and does not refuse it. Set its foreign key, or its state, by hand.
/// </para>
/// <para>
/// A key configured with <c>ValueGeneratedNever()</c>, or annotated
/// <c>[DatabaseGenerated(DatabaseGeneratedOption.None)]</c>, keeps no generator, and is written as it stands; a key
/// generated with <c>HasValueObjectKeyGeneration()</c> gets the provider's GUIDs instead.
/// </para>
/// <para>
/// Public only because a compiled model writes it as <c>new UnassignedValueObjectKeyGeneratorFactory().Create</c>.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UnassignedValueObjectKeyGeneratorFactory : ValueGeneratorFactory
{
    /// <summary>
    /// Creates the generator of the key, which throws when Entity Framework Core asks it for a value.
    /// </summary>
    /// <param name="property">The key.</param>
    /// <param name="typeBase">The entity type the generator is used for.</param>
    /// <returns>The generator.</returns>
    public override ValueGenerator Create(IProperty property, ITypeBase typeBase)
        => new UnassignedValueObjectKeyGenerator(property);
}

/// <summary>
/// Refuses an entity added without its key.
/// </summary>
/// <param name="property">The key.</param>
internal sealed class UnassignedValueObjectKeyGenerator(IProperty property) : ValueGenerator
{
    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Always: the entity was added without its key. It carries <see cref="ValueObjectErrorCodes.Required"/> under
    /// <see cref="ValueObjectErrors.ErrorCodeKey"/>, as every refusal of an integration carries its code.
    /// </exception>
    protected override object? NextValue(EntityEntry entry)
    {
        var refusal = new InvalidOperationException(
            $"'{entry.Metadata.DisplayName()}' was added without its key: "
            + $"'{property.DeclaringType.DisplayName()}.{property.Name}' holds default({TypeNames.Of(property.ClrType)}), "
            + "Guid.Empty, and nothing generates it. Assign the key in the domain, or have it generated with "
            + "HasValueObjectKeyGeneration().");
        refusal.Data[ValueObjectErrors.ErrorCodeKey] = ValueObjectErrorCodes.Required;

        throw refusal;
    }
}
