using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

// The objects the AutoFixture, Bogus and FsCheck packages fill: value objects in every shape a member takes, a nullable
// one, a list, an array, a construction of a generic one, an init-only one, a field, one behind a private setter, and one
// declared on a base type. Every value object here is generated, or a construction of the hand-written Declared<,> over a
// declaration of these tests' own, so that no test resolves a hand-written type other tests share.

/// <summary>The part of an order its base type declares.</summary>
public abstract class TestDataDocument
{
    /// <summary>Gets or sets the reference, declared on the base type.</summary>
    public Ordering.OrderReference Reference { get; set; }
}

/// <summary>An order whose members are value objects in every shape the adapters fill.</summary>
public sealed class TestDataOrder : TestDataDocument
{
#pragma warning disable CA1051 // A field is one of the shapes a member takes.
    /// <summary>The date of birth of the customer, a field.</summary>
    public BirthDate Born;
#pragma warning restore CA1051

    public CustomerId Customer { get; set; }

    public Iban Account { get; set; }

    public Iban? Backup { get; set; }

    public Quantity Quantity { get; set; }

    public CountryCode Country { get; set; }

    public List<EmailAddress> Contacts { get; set; } = [];

    public Grade[] Grades { get; set; } = [];

    public Reference<PurchaseOrder> PurchaseOrder { get; set; }

    public string? Note { get; set; }

    public Amount Total { get; init; }

    /// <summary>Gets the quantity set aside, behind a private setter: Bogus fills it, AutoFixture does not.</summary>
    public Quantity Reserved { get; private set; }

    /// <summary>Gets the value objects of the order, each with what it holds, for an assertion that every one is accepted.</summary>
    /// <returns>Each member and its value object.</returns>
    public IEnumerable<(string Member, IValueObject Value)> ValueObjects()
    {
        yield return (nameof(Reference), Reference);
        yield return (nameof(Born), Born);
        yield return (nameof(Customer), Customer);
        yield return (nameof(Account), Account);
        yield return (nameof(Backup), Backup.GetValueOrDefault());
        yield return (nameof(Quantity), Quantity);
        yield return (nameof(Country), Country);
        foreach (var contact in Contacts)
        {
            yield return (nameof(Contacts), contact);
        }

        foreach (var grade in Grades)
        {
            yield return (nameof(Grades), grade);
        }

        yield return (nameof(PurchaseOrder), PurchaseOrder);
        yield return (nameof(Total), Total);
    }
}

/// <summary>The checks the adapters' tests share.</summary>
public static class TestDataChecks
{
    /// <summary>Asserts that each value object is one its rules accept, read back through its descriptor.</summary>
    /// <param name="values">Each member and its value object.</param>
    public static void ShouldAllBeAccepted(this IEnumerable<(string Member, IValueObject Value)> values)
    {
        foreach (var (member, value) in values)
        {
            ValueObjectRegistry.TryGet(value.GetType(), out var descriptor).Should().BeTrue(member);
            descriptor!.TryCreate(value.GetBoxedValue(), out var created, out var validation).Should().BeTrue($"{member} holds '{value}': {validation.ErrorMessage}");
            created.Should().Be(value, member);
        }
    }
}

/// <summary>
/// An object whose members are lists and arrays of nullable value objects, which AutoFixture fills and Bogus leaves to the
/// faker's other rules, a value object of these tests' own written by hand, and an internal field, which Bogus's binder
/// binds and AutoFixture leaves alone.
/// </summary>
public sealed class TestDataLedger
{
#pragma warning disable CS0649 // Assigned by Bogus, through reflection.
    /// <summary>The rank, an internal field.</summary>
    internal Grade Rank;
#pragma warning restore CS0649

    public Iban?[] Accounts { get; set; } = [];

    public List<Quantity?> Quantities { get; set; } = [];

    public Declared<Tally, int> Tally { get; set; }
}

/// <summary>An order line FsCheck derives through its constructor.</summary>
/// <param name="Account">The account.</param>
/// <param name="Backup">A backup account, or none.</param>
/// <param name="Quantity">The quantity.</param>
/// <param name="Country">The country.</param>
/// <param name="Contacts">The contacts.</param>
/// <param name="Grades">The grades.</param>
/// <param name="PurchaseOrder">The purchase order, a construction of a generic value object.</param>
/// <param name="Note">A note FsCheck draws on its own.</param>
public sealed record TestDataLine(
    Iban Account,
    Iban? Backup,
    Quantity Quantity,
    CountryCode Country,
    List<EmailAddress> Contacts,
    Grade[] Grades,
    Reference<PurchaseOrder> PurchaseOrder,
    string Note);

/// <summary>
/// A count from 1 to 9 written by hand, which no registration lists: the adapters' tests own it, and resolve it only through
/// the adapters.
/// </summary>
public sealed class Tally : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { Minimum = "1", Maximum = "9" };

    public static ValidationResult Validate(int value)
        => value is >= 1 and <= 9 ? ValidationResult.Success : ValidationResult.Failure("out_of_range", "A tally counts from 1 to 9.");
}

/// <summary>
/// A count from 1 to 9 written by hand, which counts how often its schema is read: an arbitrary reads nothing of it until it
/// draws.
/// </summary>
public sealed class WatchedTally : IDeclaration<int>
{
    private static int _reads;

    /// <summary>Gets how often the schema was read.</summary>
    public static int Reads => Volatile.Read(ref _reads);

    public static ValueObjectSchema Schema
    {
        get
        {
            Interlocked.Increment(ref _reads);
            return Tally.Schema;
        }
    }

    public static ValidationResult Validate(int value) => Tally.Validate(value);
}
