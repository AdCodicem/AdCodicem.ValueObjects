using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Creates the generator <c>HasValueObjectKeyGeneration</c> gives a key, by a type a compiled model can instantiate:
/// GUIDs a database keeps in order for a key over <see cref="Guid"/>, and the temporary values Entity Framework Core
/// holds until the database generates a key over an integer.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">
/// Underlying value type: <see cref="Guid"/>, <see cref="short"/>, <see cref="int"/> or <see cref="long"/>, the types
/// <c>HasValueObjectKeyGeneration</c> is declared for.
/// </typeparam>
/// <remarks>
/// <para>
/// A GUID is SQL Server's sequential one on SQL Server (<see cref="SequentialGuidValueGenerator"/>), as Entity
/// Framework Core generates for a <see cref="Guid"/> key there, which SQL Server sorts in the order it was generated,
/// and a version 7 one on every other provider, as Npgsql generates for PostgreSQL, where Entity Framework Core would
/// generate random ones on SQLite and elsewhere. The value object is created from it through <c>Create</c>, so a rule
/// the GUID breaks fails the add with the value object's <see cref="ValueObjectException"/> rather than reach the
/// column.
/// </para>
/// <para>
/// An integer is a temporary value, negative, which the change tracker holds until <c>SaveChanges</c>, when the
/// database's identity takes its place. It is never written, so it is created unchecked, and a type counted from one
/// accepts it, a strict context included; the key the database hands back is read through the converter. Only a
/// relational database generates it: on another provider, Entity Framework Core's in-memory database for one, the add
/// fails with an <see cref="InvalidOperationException"/> rather than store the temporary value as the key.
/// </para>
/// <para>
/// <c>HasValueObjectKeyGeneration</c> sets it on the property, and a compiled model writes it as
/// <c>new ValueObjectKeyGeneratorFactory&lt;TSelf, TValue&gt;().Create</c>.
/// </para>
/// </remarks>
public sealed class ValueObjectKeyGeneratorFactory<TSelf, TValue> : ValueGeneratorFactory
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override ValueGenerator Create(IProperty property, ITypeBase typeBase)
        => new ValueObjectKeyGenerator<TSelf, TValue>(
            typeof(TValue) == typeof(Guid)
                ? (ValueGenerator<TValue>)(object)new ProviderGuidValueGenerator()
                : (ValueGenerator<TValue>)new TemporaryNumberValueGeneratorFactory().Create(property, typeBase),
            property);
}

/// <summary>
/// Makes a key generated when an entity is added, through the factory a compiled model can write.
/// </summary>
internal static class KeyGeneration
{
    /// <summary>
    /// Makes the key generated on add, by <see cref="ValueObjectKeyGeneratorFactory{TSelf, TValue}"/>.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="builder">Property builder of the key.</param>
    /// <returns>The same builder.</returns>
    public static PropertyBuilder<TSelf> Configure<TSelf, TValue>(PropertyBuilder<TSelf> builder)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ValueGeneratedOnAdd().HasValueGeneratorFactory<ValueObjectKeyGeneratorFactory<TSelf, TValue>>();
    }
}

/// <summary>
/// Generates a value object from the value another generator produces.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <param name="inner">The generator of the underlying value.</param>
/// <param name="property">The key.</param>
internal sealed class ValueObjectKeyGenerator<TSelf, TValue>(ValueGenerator<TValue> inner, IProperty property)
    : ValueGenerator<TSelf>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => inner.GeneratesTemporaryValues;

    /// <inheritdoc />
    /// <remarks>
    /// A key kept as generated, a GUID, goes through <c>Create</c>: a write trusts any instance but the default to have
    /// gone through it. A temporary one, which the database replaces and nothing writes, does not: Entity Framework
    /// Core's temporary values are negative, which a key counted from one refuses. Only a relational database replaces
    /// it; another provider would store it as the key, so the add is refused there.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The key is a temporary value and the provider is not relational, so nothing would replace it.
    /// </exception>
    public override TSelf Next(EntityEntry entry)
    {
        if (!inner.GeneratesTemporaryValues)
        {
            return TSelf.Create(inner.Next(entry));
        }

        if (!entry.Context.Database.IsRelational())
        {
            throw new InvalidOperationException(
                $"'{entry.Metadata.DisplayName()}' was added with its key left to the database, but "
                + $"'{property.DeclaringType.DisplayName()}.{property.Name}' is numbered by the identity column of a "
                + $"relational database, and {entry.Context.Database.ProviderName} is not one. Assign the key in the "
                + "domain on this provider: HasValueObjectKeyGeneration() over an integer works on a relational "
                + "database only.");
        }

        return TSelf.CreateUnchecked(inner.Next(entry));
    }
}

/// <summary>
/// Generates GUIDs a database keeps in order: SQL Server's sequential ones on SQL Server, as Entity Framework Core
/// generates for a <see cref="Guid"/> key there, version 7 ones on every other provider, as Npgsql generates for
/// PostgreSQL.
/// </summary>
/// <remarks>
/// The provider is read the first time a key is generated. Entity Framework Core caches a generator per property in a
/// cache of its internal service provider, which serves one provider, so a generator never sees another.
/// </remarks>
internal sealed class ProviderGuidValueGenerator : ValueGenerator<Guid>
{
    private const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";

    private readonly SequentialGuidValueGenerator _sequential = new();

    // 0 until the provider is read, then 1 for SQL Server and 2 for any other; an int, written atomically, so that two
    // threads generating the first keys at once read the same provider and store the same answer.
    private int _provider;

    /// <inheritdoc />
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc />
    public override Guid Next(EntityEntry entry)
    {
        if (_provider == 0)
        {
            _provider = entry.Context.Database.ProviderName == SqlServer ? 1 : 2;
        }

        return _provider == 1 ? _sequential.Next(entry) : Guid.CreateVersion7();
    }
}
