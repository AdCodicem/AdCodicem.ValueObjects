using System.Globalization;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        // An optional value object takes the configuration of the value object, the length and the comparer included,
        // then a converter of its own, which stores a value the value object rejects as NULL where a required one
        // refuses it.
        var owner = ledger.FindProperty(nameof(Ledger.Owner))!;
        owner.GetValueConverter().Should().BeOfType<NullableValueObjectConverter<CustomerId, Guid>>();
        owner.IsNullable.Should().BeTrue();

        var previous = ledger.FindProperty(nameof(Ledger.PreviousIban))!;
        previous.GetValueConverter().Should().BeOfType<NullableValueObjectConverter<Iban>>();
        previous.GetMaxLength().Should().Be(34, "the optional property takes the length of the value object");
    }

    /// <summary>
    /// A generic value object has no type the convention could configure up front. Its definition makes each
    /// construction a scalar property, and each property gets the converter, the comparer and the length closed over
    /// its own construction. Every construction is another type, and a nullable one gets the converter that stores a
    /// value the value object rejects as NULL. The
    /// constructions are closed over the entity, which nothing else in the process resolves, so the convention is what
    /// maps them, and not a descriptor something resolved first.
    /// </summary>
    [Fact]
    public void The_convention_maps_each_construction_of_a_generic_value_object()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<Shipment>), out _).Should().BeFalse();
        ValueObjectRegistry.TryGet(typeof(Catalog<Shipment>.Stock), out _).Should().BeFalse();

        var shipment = DesignTimeModel(new GenericContext()).FindEntityType(typeof(Shipment))!;

        var order = shipment.FindProperty(nameof(Shipment.Order))!;
        order.GetValueConverter().Should().BeOfType<ValueObjectConverter<Reference<Shipment>, string>>();
        order.GetValueComparer().Should().BeOfType<ValueObjectComparer<Reference<Shipment>>>();
        order.GetMaxLength().Should().Be(12);

        var invoice = shipment.FindProperty(nameof(Shipment.Invoice))!;
        invoice.GetValueConverter().Should().BeOfType<NullableValueObjectConverter<Reference<GenericContext>>>();
        invoice.GetMaxLength().Should().Be(12);

        var stock = shipment.FindProperty(nameof(Shipment.Stock))!;
        stock.GetValueConverter().Should().BeOfType<ValueObjectConverter<Catalog<Shipment>.Stock, int>>();
        stock.GetMaxLength().Should().BeNull();

        shipment.FindProperty(nameof(Shipment.Backorder))!.GetValueConverter()
            .Should().BeOfType<NullableValueObjectConverter<Catalog<Shipment>.Stock, int>>();

        shipment.FindProperty(nameof(Shipment.Carrier))!.GetValueConverter()
            .Should().BeOfType<ValueObjectConverter<IShipping.Carrier, string>>("a value object nested in an interface is not generic");
    }

    /// <summary>
    /// The strict convention validates each construction too, and a property configured explicitly keeps what it was
    /// configured with: an explicit configuration outranks a convention.
    /// </summary>
    [Fact]
    public void The_strict_convention_maps_each_construction_and_leaves_an_explicit_one_alone()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<StrictShipment>), out _).Should().BeFalse();

        var shipment = DesignTimeModel(new StrictGenericContext()).FindEntityType(typeof(StrictShipment))!;

        shipment.FindProperty(nameof(StrictShipment.Order))!.GetValueConverter()
            .Should().BeOfType<StrictValueObjectConverter<Reference<StrictShipment>, string>>();
        shipment.FindProperty(nameof(StrictShipment.Return))!.GetValueConverter()
            .Should().BeOfType<StrictNullableValueObjectConverter<Reference<StrictShipment>>>();
        shipment.FindProperty(nameof(StrictShipment.Backorder))!.GetValueConverter()
            .Should().BeOfType<StrictNullableValueObjectConverter<Catalog<StrictShipment>.Stock, int>>();
        shipment.FindProperty(nameof(StrictShipment.Stock))!.GetValueConverter()
            .Should().BeOfType<ValueObjectConverter<Catalog<StrictShipment>.Stock, int>>("the property maps itself");
    }

    /// <summary>
    /// Mapped one by one, without the convention, a construction sizes its column as the value object it is built from
    /// does, whatever resolved it first: nothing else in the process resolves this one.
    /// </summary>
    [Fact]
    public void A_construction_mapped_one_by_one_sizes_its_column()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<ExplicitShipment>), out _).Should().BeFalse();
        var builder = new ModelBuilder();

        builder.Entity<ExplicitShipment>().Property(entity => entity.Order).HasValueObjectConversion<Reference<ExplicitShipment>, string>();

        builder.Model.FindEntityType(typeof(ExplicitShipment))!.FindProperty(nameof(ExplicitShipment.Order))!
            .GetMaxLength().Should().Be(12);
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
        ledger.FindProperty(nameof(Ledger.Owner))!.GetValueConverter()
            .Should().BeOfType<StrictNullableValueObjectConverter<CustomerId, Guid>>();
        ledger.FindProperty(nameof(Ledger.PreviousIban))!.GetValueConverter()
            .Should().BeOfType<StrictNullableValueObjectConverter<Iban>>();
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
    /// Entity Framework Core maps neither Int128 nor UInt128, on any provider. The convention leaves a 128-bit value
    /// object to the converter the application gives it, even one configured before the convention ran.
    /// </summary>
    [Fact]
    public void The_convention_leaves_a_128_bit_value_object_to_a_converter_of_the_application()
    {
        var vault = DesignTimeModel(new WideContext()).FindEntityType(typeof(Vault))!;

        vault.FindProperty(nameof(Vault.Balance))!.GetValueConverter().Should().BeOfType<LedgerBalanceToDecimal>();
        vault.FindProperty(nameof(Vault.Fingerprint))!.GetValueConverter().Should().BeOfType<FingerprintToText>();
        vault.FindProperty(nameof(Vault.Iban))!.GetValueConverter()
            .Should().BeOfType<ValueObjectConverter<Iban, string>>("every other value object is still mapped");
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
    /// An instance that never went through <c>Create</c> holds the default value, which a read would trust. Both
    /// converters refuse to write one the value object rejects, with the rule and without the value, and write a zero
    /// the value object accepts, since over a value type nothing else tells it from the default.
    /// </summary>
    [Fact]
    public void The_converters_refuse_to_write_a_value_the_value_object_rejects()
    {
#pragma warning disable VO0010 // The uninitialized instance is what the converters refuse.
        var customer = default(CustomerId);
        var country = default(CountryCode);
        var amount = default(Amount);
#pragma warning restore VO0010

        var lenient = new ValueObjectConverter<CustomerId, Guid>()
            .Invoking(converter => converter.ConvertToProvider(customer))
            .Should().Throw<ValueObjectException>().Which;
        var strict = new StrictValueObjectConverter<CountryCode, string>()
            .Invoking(converter => converter.ConvertToProvider(country))
            .Should().Throw<ValueObjectException>().Which;

        lenient.Message.Should().Be("The value to write is not a valid CustomerId: A customer identifier must not be empty.");
        lenient.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        lenient.ValueObjectType.Should().Be<CustomerId>();
        lenient.AttemptedValue.Should().BeNull();
        strict.ValueObjectType.Should().Be<CountryCode>();
        new ValueObjectConverter<Amount, decimal>().ConvertToProvider(amount).Should().Be(0m, "zero is an amount");
        new StrictValueObjectConverter<Amount, decimal>().ConvertToProvider(amount).Should().Be(0m);
    }

    /// <summary>
    /// An optional property maps a value the value object rejects to a NULL, which its column takes, and anything else
    /// as a required one does. Entity Framework Core answers a null itself in both directions, so only a direct call of
    /// the typed conversions hands the converter one.
    /// </summary>
    [Fact]
    public void The_optional_converters_store_a_value_the_value_object_rejects_as_NULL()
    {
#pragma warning disable VO0010 // The uninitialized instance is what the converters store as NULL.
        var unsetCustomer = default(CustomerId);
        var unsetIban = default(Iban);
        var unsetAmount = default(Amount);
#pragma warning restore VO0010
        var customer = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));
        var iban = Iban.Create("FR7630006000011234567890189");
        var customers = new NullableValueObjectConverter<CustomerId, Guid>();
        var ibans = new NullableValueObjectConverter<Iban>();

        customers.ConvertToProvider(unsetCustomer).Should().BeNull();
        customers.ConvertToProvider(customer).Should().Be(customer.Value);
        customers.ConvertFromProvider(Guid.Empty).Should().Be(CustomerId.CreateUnchecked(Guid.Empty), "a read trusts the column");
        customers.ConvertToProviderTyped(null).Should().BeNull();
        customers.ConvertFromProviderTyped(null).Should().BeNull();
        new NullableValueObjectConverter<Amount, decimal>().ConvertToProvider(unsetAmount).Should().Be(0m, "zero is an amount");

        ibans.ConvertToProvider(unsetIban).Should().BeNull();
        ibans.ConvertToProvider(iban).Should().Be(iban.Value);
        ibans.ConvertFromProvider("fr76 3000").Should().Be(Iban.CreateUnchecked("fr76 3000"), "a read trusts the column");
        ibans.ConvertToProviderTyped(null).Should().BeNull();
        ibans.ConvertFromProviderTyped(null).Should().BeNull();
    }

    /// <summary>
    /// The strict optional converters store as the others do, and validate what they read.
    /// </summary>
    [Fact]
    public void The_strict_optional_converters_validate_what_they_read()
    {
#pragma warning disable VO0010 // The uninitialized instance is what the converters store as NULL.
        var unsetCustomer = default(CustomerId);
        var unsetIban = default(Iban);
#pragma warning restore VO0010
        var customer = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));
        var customers = new StrictNullableValueObjectConverter<CustomerId, Guid>();
        var ibans = new StrictNullableValueObjectConverter<Iban>();

        customers.ConvertToProvider(unsetCustomer).Should().BeNull();
        customers.ConvertToProvider(customer).Should().Be(customer.Value);
        customers.ConvertFromProvider(customer.Value).Should().Be(customer);
        customers.Invoking(converter => converter.ConvertFromProvider(Guid.Empty)).Should().Throw<ValueObjectException>();
        customers.ConvertToProviderTyped(null).Should().BeNull();
        customers.ConvertFromProviderTyped(null).Should().BeNull();

        ibans.ConvertToProvider(unsetIban).Should().BeNull();
        ibans.ConvertToProvider(Iban.Create("FR7630006000011234567890189")).Should().Be("FR7630006000011234567890189");
        ibans.ConvertFromProvider("fr76 3000 6000 0112 3456 7890 189").Should().Be(Iban.Create("FR7630006000011234567890189"));
        ibans.Invoking(converter => converter.ConvertFromProvider("not an IBAN")).Should().Throw<ValueObjectException>();
        ibans.ConvertToProviderTyped(null).Should().BeNull();
        ibans.ConvertFromProviderTyped(null).Should().BeNull();
    }

    /// <summary>
    /// A value object written by hand over a reference type other than <see cref="string"/> has no optional converter:
    /// its optional properties keep its own, which refuses a value it rejects rather than store it.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_over_another_reference_type_has_no_optional_converter()
    {
        ConverterTypes.Optional(typeof(HandWrittenLink), typeof(Uri), strict: false).Should().BeNull();
        ConverterTypes.Optional(typeof(HandWrittenCode), typeof(string), strict: true)
            .Should().Be<StrictNullableValueObjectConverter<HandWrittenCode>>();
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

        public Iban? PreviousIban { get; set; }

        public UnregisteredCode Code { get; set; }
    }

    /// <summary>An entity holding 128-bit value objects.</summary>
    private sealed class Vault
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public LedgerBalance Balance { get; set; }

        public Fingerprint Fingerprint { get; set; }
    }

    /// <summary>Stores a balance as a decimal, which carries the values a decimal column can hold.</summary>
    private sealed class LedgerBalanceToDecimal() : ValueConverter<LedgerBalance, decimal>(
        balance => (decimal)balance.Value,
        value => LedgerBalance.Create((Int128)value));

    /// <summary>Stores a fingerprint as its digits.</summary>
    private sealed class FingerprintToText() : ValueConverter<Fingerprint, string>(
        fingerprint => fingerprint.Value.ToString(CultureInfo.InvariantCulture),
        text => Fingerprint.Create(UInt128.Parse(text, CultureInfo.InvariantCulture)));

    /// <summary>A model mapping 128-bit value objects through converters of its own.</summary>
    private sealed class WideContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<LedgerBalance>().HaveConversion<LedgerBalanceToDecimal>();
            configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);
            configurationBuilder.Properties<Fingerprint>().HaveConversion<FingerprintToText>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Vault>();
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

    /// <summary>A shipment, holding constructions of generic value objects.</summary>
    private sealed class Shipment
    {
        public int Id { get; set; }

        public Reference<Shipment> Order { get; set; }

        public Reference<GenericContext>? Invoice { get; set; }

        public Catalog<Shipment>.Stock Stock { get; set; }

        public Catalog<Shipment>.Stock? Backorder { get; set; }

        public IShipping.Carrier Carrier { get; set; }
    }

    /// <summary>A shipment mapped by the strict convention, holding constructions of its own.</summary>
    private sealed class StrictShipment
    {
        public int Id { get; set; }

        public Reference<StrictShipment> Order { get; set; }

        public Reference<StrictShipment>? Return { get; set; }

        public Catalog<StrictShipment>.Stock Stock { get; set; }

        public Catalog<StrictShipment>.Stock? Backorder { get; set; }
    }

    /// <summary>A shipment whose construction is mapped one by one.</summary>
    private sealed class ExplicitShipment
    {
        public int Id { get; set; }

        public Reference<ExplicitShipment> Order { get; set; }
    }

    /// <summary>A model mapping the generic value objects of the unit domain.</summary>
    private sealed class GenericContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Shipment>();
    }

    /// <summary>
    /// A model mapping the generic value objects of the unit domain and validating what it reads, but for the stock,
    /// which it maps explicitly, as an application departing from the convention does.
    /// </summary>
    private sealed class StrictGenericContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(strict: true);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StrictShipment>()
                .Property(entity => entity.Stock)
                .HasValueObjectConversion<Catalog<StrictShipment>.Stock, int>();
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
