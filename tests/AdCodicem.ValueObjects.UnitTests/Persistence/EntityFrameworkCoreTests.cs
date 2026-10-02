using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The model the Entity Framework Core conventions and per-property mappings give a value object.
/// </summary>
/// <remarks>
/// The conventions shape a model only through a <see cref="DbContext"/>, whose
/// <see cref="ModelConfigurationBuilder"/> needs a provider: the contexts here use Npgsql, and building their model
/// opens no connection. What the columns become on a real engine, and what a strict read does to a row another
/// writer stored, is checked in the integration suite.
/// </remarks>
public class EntityFrameworkCoreTests
{
    [Fact]
    public void The_convention_maps_every_value_object_to_its_underlying_column_and_sizes_it()
    {
        var ledger = DesignTimeModel(new LenientContext()).FindEntityType(typeof(Ledger))!;

        var iban = ledger.FindProperty(nameof(Ledger.Iban))!;
        iban.GetValueConverter().Should().BeOfType<ValueObjectConverter<Iban, string>>();
        iban.GetValueComparer().Should().BeOfType<ValueObjectComparer<Iban>>();
        iban.GetMaxLength().Should().Be(34, "the maximum length declared on the value object sizes the column");

        var balance = ledger.FindProperty(nameof(Ledger.Balance))!;
        balance.GetValueConverter().Should().BeOfType<ValueObjectConverter<Amount, decimal>>();
        balance.GetMaxLength().Should().BeNull("a value object declaring no maximum length leaves the column alone");

        ledger.FindProperty(nameof(Ledger.Owner))!.GetValueConverter()
            .Should().BeOfType<ValueObjectConverter<CustomerId, Guid>>("an optional value object is mapped the same way");
    }

    [Fact]
    public void The_strict_convention_validates_what_it_reads()
    {
        var ledger = DesignTimeModel(new StrictContext()).FindEntityType(typeof(Ledger))!;

        var iban = ledger.FindProperty(nameof(Ledger.Iban))!;
        iban.GetValueConverter().Should().BeOfType<StrictValueObjectConverter<Iban, string>>();
        iban.GetValueComparer().Should().BeOfType<ValueObjectComparer<Iban>>();
        iban.GetMaxLength().Should().Be(34);

        ledger.FindProperty(nameof(Ledger.Balance))!.GetValueConverter()
            .Should().BeOfType<StrictValueObjectConverter<Amount, decimal>>();
    }

    [Fact]
    public void A_property_mapped_one_by_one_gets_the_converter_the_comparer_and_the_length_of_the_convention()
    {
        var builder = new ModelBuilder();
        builder.Entity<Ledger>(ledger =>
        {
            ledger.Property(entity => entity.Iban).HasValueObjectConversion<Iban, string>();
            ledger.Property(entity => entity.Balance).HasValueObjectConversion<Amount, decimal>(strict: true);
            ledger.Property(entity => entity.Code).HasValueObjectConversion<UnregisteredCode, string>();
        });

        var ledger = builder.Model.FindEntityType(typeof(Ledger))!;

        var iban = ledger.FindProperty(nameof(Ledger.Iban))!;
        iban.GetValueConverter().Should().BeOfType<ValueObjectConverter<Iban, string>>();
        iban.GetValueComparer().Should().BeOfType<ValueObjectComparer<Iban>>();
        iban.GetMaxLength().Should().Be(34);

        var balance = ledger.FindProperty(nameof(Ledger.Balance))!;
        balance.GetValueConverter().Should().BeOfType<StrictValueObjectConverter<Amount, decimal>>();
        balance.GetMaxLength().Should().BeNull();

        // A value object written by hand registers no rules, and declares no length the column could take.
        var code = ledger.FindProperty(nameof(Ledger.Code))!;
        code.GetValueConverter().Should().BeOfType<ValueObjectConverter<UnregisteredCode, string>>();
        code.GetMaxLength().Should().BeNull();
    }

    /// <summary>
    /// A row is trusted as it is read, the way this application wrote it: it is neither normalized nor validated.
    /// </summary>
    [Fact]
    public void The_default_converter_reads_the_column_as_stored()
    {
        var converter = new ValueObjectConverter<Amount, decimal>();

        converter.ConvertFromProvider(12.345m).Should().BeOfType<Amount>().Which.Value.Should().Be(12.345m);
        converter.ConvertFromProvider(-1m).Should().BeOfType<Amount>().Which.Value.Should().Be(-1m);
        converter.ConvertToProvider(Amount.Create(5m)).Should().Be(5.00m);
    }

    /// <summary>
    /// For a table another writer shares, what is read goes through the validating factory: normalized, and refused
    /// when the domain would refuse it.
    /// </summary>
    [Fact]
    public void The_strict_converter_normalizes_and_validates_the_column()
    {
        var converter = new StrictValueObjectConverter<Amount, decimal>();

        converter.ConvertFromProvider(12.345m).Should().BeOfType<Amount>().Which.Value.Should().Be(12.34m);
        converter.Invoking(strict => strict.ConvertFromProvider(-1m)).Should().Throw<ValueObjectException>();
        converter.ConvertToProvider(Amount.Create(5m)).Should().Be(5.00m);
    }

    /// <summary>
    /// Change tracking compares with the value object's own equality, so assigning a reference that differs only by
    /// case is no change for a value object compared case-insensitively.
    /// </summary>
    [Fact]
    public void The_comparer_uses_the_comparison_the_value_object_declares()
    {
        var comparer = new ValueObjectComparer<Ordering.OrderReference>();
        var upper = Ordering.OrderReference.Create("ORD-42");
        var lower = Ordering.OrderReference.Create("ord-42");

        comparer.Equals(upper, lower).Should().BeTrue();
        comparer.GetHashCode(upper).Should().Be(comparer.GetHashCode(lower));
        comparer.Snapshot(upper).Should().Be(upper);
    }

    private static IModel DesignTimeModel(DbContext context)
    {
        using (context)
        {
            return context.GetService<IDesignTimeModel>().Model;
        }
    }

    /// <summary>An entity holding value objects of several kinds.</summary>
    private sealed class Ledger
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public Amount Balance { get; set; }

        public CustomerId? Owner { get; set; }

        public UnregisteredCode Code { get; set; }
    }

    /// <summary>A model mapping every value object already registered, and trusting what it reads.</summary>
    private sealed class LenientContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>().Ignore(entity => entity.Code);
    }

    /// <summary>A model mapping the value objects of the unit domain, and validating what it reads.</summary>
    private sealed class StrictContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(strict: true, typeof(Iban).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>().Ignore(entity => entity.Code);
    }
}
