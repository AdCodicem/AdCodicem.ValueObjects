using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// Every value object of the domain implements IXmlSerializable, as the generator of the packed AdCodicem.ValueObjects
// writes it in the compiler of the next SDK.
[assembly: ValueObjectXmlSerialization]

namespace AdCodicem.ValueObjects.CompatTests;

// The value objects a consumer writes, in the style of the sample, compiled by the generator the packed
// AdCodicem.ValueObjects carries, in the compiler of the next SDK.

/// <summary>A Guid value object, with a rule the default value breaks.</summary>
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    /// <summary>Mints a new identifier.</summary>
    /// <returns>The identifier.</returns>
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    /// <inheritdoc />
    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty ? ValidationResult.Required("A customer identifier must not be empty.") : ValidationResult.Success;
}

/// <summary>A string value object with a normalizer and a source-generated pattern.</summary>
[ValueObject<string>(MaxLength = 254, SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    /// <inheritdoc />
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    /// <inheritdoc />
    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>A bounded string value object with every kind of rule: lengths, pattern, normalizer and validator.</summary>
[ValueObject<string>(MinLength = 15, MaxLength = 34, SchemaFormat = "iban")]
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectPatternValidator, IValueObjectValidator<string>
{
    /// <inheritdoc />
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    /// <inheritdoc />
    public static string NormalizeValue(string value)
        => string.Concat(value.Where(static character => !char.IsWhiteSpace(character) && character != '-')).ToUpperInvariant();

    /// <inheritdoc />
    public static ValidationResult ValidateValue(in string value)
    {
        var remainder = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[(i + 4) % value.Length];
            remainder = char.IsAsciiDigit(character)
                ? ((remainder * 10) + (character - '0')) % 97
                : ((remainder * 100) + (character - 'A' + 10)) % 97;
        }

        return remainder == 1 ? ValidationResult.Success : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
    }
}

/// <summary>A decimal value object with arithmetic and a typed lower bound.</summary>
[ValueObject<decimal>(Arithmetic = true)]
public readonly partial struct Amount : IValueObjectNormalizer<decimal>, IValueObjectMinimum<decimal>
{
    /// <inheritdoc />
    public static decimal Minimum => 0m;

    /// <inheritdoc />
    public static decimal NormalizeValue(decimal value) => decimal.Round(value, 2, MidpointRounding.ToEven) + 0.00m;
}

/// <summary>An integer value object with typed bounds.</summary>
[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 100;
}

/// <summary>A date with typed bounds, which JSON writes as a string.</summary>
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    /// <inheritdoc />
    public static DateOnly Minimum => new(1900, 1, 1);

    /// <inheritdoc />
    public static DateOnly Maximum => new(2100, 12, 31);
}

/// <summary>A real bounded on one side only, so an infinity is a value it accepts.</summary>
[ValueObject<double>]
public readonly partial struct Ratio : IValueObjectMinimum<double>
{
    /// <inheritdoc />
    public static double Minimum => 0d;
}

/// <summary>A closed set of reference data.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2)]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    [KnownValue]
    public static readonly CountryCode France = Known("FR");

    [KnownValue]
    public static readonly CountryCode Belgium = Known("BE");

    [KnownValue]
    public static readonly CountryCode Luxembourg = Known("LU");

    /// <inheritdoc />
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>A Stripe-style entity identifier.</summary>
[EntityId("pay")]
public readonly partial struct PaymentId;

/// <summary>Owner of a <see cref="Reference{TOwner}"/>.</summary>
public sealed class PurchaseOrder;

/// <summary>Another owner of a <see cref="Reference{TOwner}"/>.</summary>
public sealed class SalesInvoice;

/// <summary>A generic value object: each construction is a value object of its own.</summary>
/// <typeparam name="TOwner">The type the reference belongs to.</typeparam>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct Reference<TOwner> : IValueObjectNormalizer<string>
    where TOwner : class
{
#pragma warning disable CA1000 // A hook is a static member of the generic type.
    /// <inheritdoc />
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
#pragma warning restore CA1000
}

/// <summary>A customer.</summary>
public sealed class Customer
{
    /// <summary>Gets or sets the identifier.</summary>
    public CustomerId Id { get; set; }

    /// <summary>Gets or sets the email address.</summary>
    public EmailAddress Email { get; set; }

    /// <summary>Gets or sets the country.</summary>
    public CountryCode Country { get; set; }

    /// <summary>Gets the accounts.</summary>
    public List<BankAccount> Accounts { get; } = [];
}

/// <summary>A bank account, keyed by a value object.</summary>
public sealed class BankAccount
{
    /// <summary>Gets or sets the IBAN.</summary>
    public Iban Iban { get; set; }

    /// <summary>Gets or sets the owner.</summary>
    public CustomerId CustomerId { get; set; }

    /// <summary>Gets or sets the balance.</summary>
    public Amount Balance { get; set; }
}

/// <summary>A payment, keyed by an entity identifier.</summary>
public sealed class Payment
{
    /// <summary>Gets or sets the identifier.</summary>
    public PaymentId Id { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public Iban Account { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public Amount Amount { get; set; }
}

/// <summary>An order holding two constructions of a generic value object.</summary>
public sealed class Order
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the purchase order reference.</summary>
    public Reference<PurchaseOrder> Purchase { get; set; }

    /// <summary>Gets or sets the invoice reference, if one was issued.</summary>
    public Reference<SalesInvoice>? Invoice { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public Quantity Quantity { get; set; }
}

/// <summary>A portfolio, holding collections of value objects, which the convention maps as primitive collections.</summary>
public sealed class Portfolio
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the IBANs.</summary>
    public List<Iban> Ibans { get; set; } = [];

    /// <summary>Gets or sets the IBANs the portfolio held, a null for one unknown.</summary>
    public List<Iban?> Previous { get; set; } = [];

    /// <summary>Gets or sets the quantities.</summary>
    public Quantity[] Quantities { get; set; } = [];

    /// <summary>Gets or sets the purchase orders, constructions of a generic value object.</summary>
    public List<Reference<PurchaseOrder>> Orders { get; set; } = [];
}

/// <summary>The model, mapped by the two conventions as the README says.</summary>
/// <param name="options">Options.</param>
public class ShopContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Gets the customers.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Gets the accounts.</summary>
    public DbSet<BankAccount> Accounts => Set<BankAccount>();

    /// <summary>Gets the payments.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <summary>Gets the orders.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Gets the portfolios.</summary>
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    /// <summary>Gets a value indicating whether values read from the database are validated again.</summary>
    protected virtual bool Strict => false;

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ConfigureValueObjects(Strict, typeof(Iban).Assembly);

        // The model is cached per context type and per provider, so the collation can follow the provider.
        var collation = Database.ProviderName switch
        {
            "Npgsql.EntityFrameworkCore.PostgreSQL" => IdCollations.PostgreSql,
            "Microsoft.EntityFrameworkCore.SqlServer" => IdCollations.SqlServer,
            _ => null,
        };
        configurationBuilder.ConfigureEntityIds(collation, Strict, typeof(PaymentId).Assembly);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(entity => entity.Id);
            customer.HasIndex(entity => entity.Email).IsUnique();
            customer.HasMany(entity => entity.Accounts).WithOne().HasForeignKey(account => account.CustomerId);
        });

        modelBuilder.Entity<BankAccount>(account =>
        {
            account.ToTable("accounts");
            account.HasKey(entity => entity.Iban);
            account.Property(entity => entity.Balance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("payments");
            payment.HasKey(entity => entity.Id);
            payment.Property(entity => entity.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.HasKey(entity => entity.Id);
            order.Property(entity => entity.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Portfolio>(portfolio =>
        {
            portfolio.ToTable("portfolios");
            portfolio.Property(entity => entity.Id).ValueGeneratedNever();
        });
    }
}

/// <summary>The same model, re-validating every value it reads.</summary>
/// <param name="options">Options.</param>
public sealed class StrictShopContext(DbContextOptions options) : ShopContext(options)
{
    /// <inheritdoc />
    protected override bool Strict => true;
}
