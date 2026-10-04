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

    /// <summary>Gets a value indicating whether the context validates what it reads.</summary>
    protected virtual bool Strict => false;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(connectionString);
#if COMPILED_MODEL
        optionsBuilder.UseModel(Strict ? Compiled.Strict.StrictShopContextModel.Instance : Compiled.Relaxed.ShopContextModel.Instance);
#endif
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder
            .ConfigureValueObjects(Strict, typeof(OrderId).Assembly)
            .ConfigureEntityIds(Strict, typeof(OrderId).Assembly);
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
