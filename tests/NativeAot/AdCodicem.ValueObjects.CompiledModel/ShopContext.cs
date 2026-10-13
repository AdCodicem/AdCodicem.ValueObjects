using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdCodicem.ValueObjects.CompiledModel;

/// <summary>A context mapping every value object of the domain through the conventions, reading what it wrote unchecked.</summary>
/// <param name="connectionString">The SQL Server database to connect to, if the context ever does.</param>
public class ShopContext(string connectionString) : DbContext
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<Parcel> Parcels => Set<Parcel>();

    public DbSet<Consignment> Consignments => Set<Consignment>();

    public DbSet<Page> Pages => Set<Page>();

    /// <summary>Gets a value indicating whether the context validates what it reads.</summary>
    protected virtual bool Strict => false;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // The design-time factory builds the context through here, so that the queries precompiled for native AOT are
        // translated with .Value read from the column, as they are at run time. A row a CHECK constraint refuses carries
        // the code of its rule, read off the constraint's name, which no compiled model holds.
        optionsBuilder.UseSqlServer(connectionString).UseValueObjectQueries().UseValueObjectCheckErrors();
#if COMPILED_MODEL
        optionsBuilder.UseModel(Strict ? Compiled.Strict.StrictShopContextModel.Instance : Compiled.Relaxed.ShopContextModel.Instance);
#endif
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder
            .ConfigureValueObjects(Strict, typeof(OrderId).Assembly)
            .ConfigureEntityIds(Strict, typeof(OrderId).Assembly)
            .AddValueObjectCheckConstraints();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Parcel>().Property(parcel => parcel.Id).HasValueObjectKeyGeneration();
        modelBuilder.Entity<Consignment>();
        modelBuilder.Entity<Page>().Property(page => page.Id).HasValueObjectKeyGeneration();
    }
}

/// <summary>The same model, validating every value it reads.</summary>
/// <param name="connectionString">The SQL Server database to connect to, if the context ever does.</param>
public sealed class StrictShopContext(string connectionString) : ShopContext(connectionString)
{
    protected override bool Strict => true;
}

/// <summary>Creates the contexts for <c>dotnet ef</c>, which builds their models and opens no connection.</summary>
public sealed class ShopContextFactory : IDesignTimeDbContextFactory<ShopContext>, IDesignTimeDbContextFactory<StrictShopContext>
{
    private const string DesignTime = "Server=design-time;Database=shop";

    public ShopContext CreateDbContext(string[] args) => new(DesignTime);

    StrictShopContext IDesignTimeDbContextFactory<StrictShopContext>.CreateDbContext(string[] args) => new(DesignTime);
}
