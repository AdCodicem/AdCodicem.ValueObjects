using AdCodicem.ValueObjects.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What a collection of a value object becomes through the Entity Framework Core convention, on SQLite in memory, for the
/// samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class PrimitiveCollectionParity
{
    /// <summary>
    /// Checks that a list, an array and a list of the optional value object are primitive collections whose elements get
    /// the converter, the comparer and the length of the value object, and that they round-trip, a null element included;
    /// and, for a value object over a 128-bit integer, which Entity Framework Core maps to no column, that the convention
    /// leaves the collection to the application, so that the model is refused as it was.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">An instance.</param>
    /// <param name="large">Another instance.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var name = typeof(TSelf).Name;

        if (typeof(TValue) == typeof(Int128) || typeof(TValue) == typeof(UInt128))
        {
            using var wide = new CollectionContext<TSelf>(connection);
            wide.Invoking(static context => context.Model)
                .Should().Throw<InvalidOperationException>()
                .WithMessage($"*'List<{name}>'*");
            return;
        }

        using (var write = new CollectionContext<TSelf>(connection))
        {
            var holder = write.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CollectionHolder<TSelf>))!;
            foreach (var property in (string[])[nameof(CollectionHolder<TSelf>.Items), nameof(CollectionHolder<TSelf>.Array)])
            {
                var element = holder.FindProperty(property)!.GetElementType()!;
                element.GetValueConverter().Should().BeOfType<ValueObjectConverter<TSelf, TValue>>("{0} converts each element of {1}", name, property);
                element.GetValueComparer().Should().BeOfType<ValueObjectComparer<TSelf>>();
                element.GetMaxLength().Should().Be(TSelf.Schema.MaxLength);
            }

            // An optional element over text stores a value the value object rejects as a null; over a value type, it is
            // converted as an element of the value object is.
            var optional = holder.FindProperty(nameof(CollectionHolder<TSelf>.Optional))!.GetElementType()!;
            optional.GetValueConverter()!.GetType().Should().Be(
                typeof(TValue) == typeof(string)
                    ? typeof(NullableValueObjectConverter<>).MakeGenericType(typeof(TSelf))
                    : typeof(ValueObjectConverter<TSelf, TValue>));
            optional.GetValueComparer().Should().BeOfType<NullableValueObjectComparer<TSelf>>();
            optional.GetMaxLength().Should().Be(TSelf.Schema.MaxLength);

            write.Database.EnsureCreated();
            write.Add(new CollectionHolder<TSelf> { Id = 1, Items = [small, large], Array = [large], Optional = [null, large] });
            write.SaveChanges();
        }

        using var read = new CollectionContext<TSelf>(connection);
        var reloaded = read.Set<CollectionHolder<TSelf>>().AsNoTracking().Single();
        reloaded.Items.Should().Equal([small, large], "{0} round-trips in a list", name);
        reloaded.Array.Should().Equal([large], "{0} round-trips in an array", name);
        reloaded.Optional.Should().Equal([null, large], "{0} round-trips in a list of the optional value object", name);
    }

    /// <summary>An entity holding collections of one value object.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    private sealed class CollectionHolder<TSelf>
        where TSelf : struct
    {
        public int Id { get; set; }

        public List<TSelf> Items { get; set; } = [];

        public TSelf[] Array { get; set; } = [];

        public List<TSelf?> Optional { get; set; } = [];
    }

    /// <summary>A model of its own for each value object, mapping the value objects of the unit domain.</summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <param name="connection">The database.</param>
    private sealed class CollectionContext<TSelf>(SqliteConnection connection) : DbContext
        where TSelf : struct
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite(connection);

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<CollectionHolder<TSelf>>();
    }
}
