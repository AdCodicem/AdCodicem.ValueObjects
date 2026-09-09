using AdCodicem.ValueObjects.EntityFrameworkCore;
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
/// The banking model, where every column holds the underlying value of its value object.
/// </summary>
public sealed class BankingDbContext(DbContextOptions<BankingDbContext> options) : DbContext(options)
{
    /// <summary>Gets the customers.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Gets the accounts.</summary>
    public DbSet<BankAccount> Accounts => Set<BankAccount>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // One call maps every value object of the assembly. An Iban lands in a varchar(34) because the type
        // says its maximum length is 34, and a CustomerId lands in the provider's native uuid column.
        configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);
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
    }
}
