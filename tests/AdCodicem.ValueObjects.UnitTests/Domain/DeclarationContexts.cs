using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Domain;

// Value objects declared where the generated code reaches them through their context: generic, nested in a generic
// type or an interface, private. Each construction of a generic one is a type of its own, which the registry describes
// once asked for it.

/// <summary>
/// The owner of a purchase order reference.
/// </summary>
public sealed class PurchaseOrder;

/// <summary>
/// The owner of a sales invoice reference.
/// </summary>
public sealed class SalesInvoice;

/// <summary>
/// A reference to a document, whose owner is part of its type: a purchase order's and a sales invoice's are not
/// interchangeable.
/// </summary>
/// <typeparam name="TOwner">The kind of document referenced.</typeparam>
[ValueObject<string>(MaxLength = 12, Example = "PO-1042")]
public readonly partial struct Reference<TOwner> : IValueObjectNormalizer<string>
    where TOwner : class
{
#pragma warning disable CA1000 // A hook is a static member, and the generic type is the point of the declaration.
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
#pragma warning restore CA1000
}

/// <summary>
/// A catalog of items of one kind.
/// </summary>
/// <typeparam name="TItem">The kind of item.</typeparam>
public static partial class Catalog<TItem>
{
    /// <summary>
    /// How many items of the catalog's kind are in stock.
    /// </summary>
    [ValueObject<int>(Arithmetic = true)]
    public readonly partial struct Stock : IValueObjectMinimum<int>
    {
#pragma warning disable CA1000 // A hook is a static member, and the generic type is the point of the declaration.
        public static int Minimum => 0;
#pragma warning restore CA1000
    }
}

/// <summary>
/// The vocabulary of shipping.
/// </summary>
public partial interface IShipping
{
    /// <summary>
    /// A carrier, by its three-letter code.
    /// </summary>
    [ValueObject<string>(MinLength = 3, MaxLength = 3)]
    public readonly partial struct Carrier;
}

/// <summary>
/// A strongbox, whose secret and identifier never leave it.
/// </summary>
public partial class Strongbox
{
    /// <summary>
    /// Gets the type of the secret, which only the strongbox can name.
    /// </summary>
    public static Type SecretType => typeof(Secret);

    /// <summary>
    /// Gets the type of the identifier, which only the strongbox and its derived types can name.
    /// </summary>
    public static Type IdentifierType => typeof(StrongboxId);

    /// <summary>
    /// Validates a secret, as only the strongbox can.
    /// </summary>
    /// <param name="value">The secret.</param>
    /// <returns>The secret, as a value object.</returns>
    public static object Seal(string value) => Secret.Create(value);

    /// <summary>
    /// A secret.
    /// </summary>
    [ValueObject<string>(MinLength = 8)]
    private readonly partial struct Secret;

    /// <summary>
    /// The public identifier of a strongbox.
    /// </summary>
    [EntityId("box")]
    protected readonly partial struct StrongboxId;
}

/// <summary>
/// Records the static constructors that ran, from outside the types that run them.
/// </summary>
public static class StaticConstructors
{
    /// <summary>
    /// Gets the names of the types whose static constructor ran.
    /// </summary>
    public static System.Collections.Concurrent.ConcurrentBag<string> Ran { get; } = [];
}

/// <summary>
/// An archive, whose static constructor nothing but its own use may run, around a private value object.
/// </summary>
public partial class Archive
{
    static Archive() => StaticConstructors.Ran.Add(nameof(Archive));

    /// <summary>
    /// A shelf of the archive.
    /// </summary>
    [ValueObject<string>]
    private readonly partial struct Shelf;
}
