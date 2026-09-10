using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.Sample.Api.Persistence;

/// <summary>
/// A customer of the bank.
/// </summary>
public sealed class Customer
{
    /// <summary>Gets or sets the identifier.</summary>
    public CustomerId Id { get; set; }

    /// <summary>Gets or sets the email address.</summary>
    public EmailAddress Email { get; set; }

    /// <summary>Gets or sets the country of residence.</summary>
    public CountryCode Country { get; set; }

    /// <summary>Gets or sets the accounts held by the customer.</summary>
    public List<BankAccount> Accounts { get; } = [];
}

/// <summary>
/// A bank account held by a customer.
/// </summary>
public sealed class BankAccount
{
    /// <summary>Gets or sets the account number.</summary>
    public Iban Iban { get; set; }

    /// <summary>Gets or sets the owner.</summary>
    public CustomerId CustomerId { get; set; }

    /// <summary>Gets or sets the current balance.</summary>
    public Amount Balance { get; set; }
}

/// <summary>
/// A payment, keyed by the public identifier a client sees.
/// </summary>
public sealed class Payment
{
    /// <summary>Gets or sets the public identifier.</summary>
    public PaymentId Id { get; set; }

    /// <summary>Gets or sets the account the payment debits.</summary>
    public Iban Account { get; set; }

    /// <summary>Gets or sets the amount paid.</summary>
    public Amount Amount { get; set; }
}

/// <summary>
/// The banking model, where every column holds the underlying value of its value object.
/// </summary>
public sealed class BankingDbContext(DbContextOptions<BankingDbContext> options) : DbContext(options)
{
    /// <summary>Gets the customers.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Gets the accounts.</summary>
    public DbSet<BankAccount> Accounts => Set<BankAccount>();

    /// <summary>Gets the payments.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // One call maps every value object of the assembly. An Iban lands in a varchar(34) because the type
        // says its maximum length is 34, and a CustomerId lands in the provider's native uuid column.
        configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

        // Identifiers are fixed width and ASCII, so they earn a narrower column than the convention above would
        // give them: char(n) rather than varchar(n), and never nchar.
        configurationBuilder.ConfigureEntityIds(typeof(PaymentId).Assembly);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(entity => entity.Id);
            customer.HasIndex(entity => entity.Email).IsUnique();
            customer.HasMany(entity => entity.Accounts)
                .WithOne()
                .HasForeignKey(account => account.CustomerId);
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

            // The collation is the one thing the convention cannot pick for you, since it names a provider.
            // Binary comparison matches what the application does in memory, where identifiers compare
            // ordinally, so a sort in SQL and a sort in code agree.
            payment.Property(entity => entity.Id).UseCollation(Database.IsNpgsql()
                ? IdCollations.PostgreSql
                : IdCollations.SqlServer);
        });
    }
}
