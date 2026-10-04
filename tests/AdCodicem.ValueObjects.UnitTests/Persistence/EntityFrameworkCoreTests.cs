using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        // An optional value object takes the configuration of the value object, the length included, then a converter
        // of its own, which stores a value the value object rejects as NULL where a required one refuses it, and a
        // comparer of its own.
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
    /// does, whatever resolved it first: nothing else in the process resolves this one. The length is read off the
    /// type, so the registry is never asked to describe the construction, which would take reflection.
    /// </summary>
    [Fact]
    public void A_construction_mapped_one_by_one_sizes_its_column()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<ExplicitShipment>), out _).Should().BeFalse();
        var builder = new ModelBuilder();

        builder.Entity<ExplicitShipment>().Property(entity => entity.Order).HasValueObjectConversion<Reference<ExplicitShipment>, string>();

        builder.Model.FindEntityType(typeof(ExplicitShipment))!.FindProperty(nameof(ExplicitShipment.Order))!
            .GetMaxLength().Should().Be(12);
        ValueObjectRegistry.TryGet(typeof(Reference<ExplicitShipment>), out _).Should().BeFalse("nothing described it");
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

        // A value object written by hand whose schema declares no length leaves the column to the provider.
        var code = ledger.FindProperty(nameof(Ledger.Code))!;
        code.GetValueConverter().Should().BeOfType<ValueObjectConverter<UnregisteredCode, string>>();
        code.GetMaxLength().Should().BeNull();
    }

    /// <summary>
    /// Entity Framework Core maps neither Int128 nor UInt128, on any provider. The convention leaves a 128-bit value
    /// object to the converter the application gives it, even one configured before the convention ran, and its
    /// optional form to the comparer the application gives that, which a compiled model can write.
    /// </summary>
    [Fact]
    public void The_convention_leaves_a_128_bit_value_object_to_a_converter_of_the_application()
    {
        var vault = DesignTimeModel(new WideContext()).FindEntityType(typeof(Vault))!;

        vault.FindProperty(nameof(Vault.Balance))!.GetValueConverter().Should().BeOfType<LedgerBalanceToDecimal>();
        var previous = vault.FindProperty(nameof(Vault.PreviousBalance))!;
        previous.GetValueConverter().Should().BeOfType<LedgerBalanceToDecimal>();
        ComparedAsOptional<LedgerBalance>(previous);
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

    /// <summary>
    /// Every optional value object is compared by a comparer of its nullable type. Given the comparer of the value
    /// object, Entity Framework Core would wrap it, and the compiled model <c>dotnet ef dbcontext optimize</c> writes for
    /// that wrapping does not compile (CS0453), since it names the comparer where the wrapper takes the value type. The
    /// convention names the comparer by its type, which a compiled model instantiates as it stands; the generic
    /// convention sets an instance, whose expressions a compiled model writes out.
    /// </summary>
    [Fact]
    public void Every_optional_value_object_is_compared_by_a_comparer_a_compiled_model_can_write()
    {
        var ledger = DesignTimeModel(new LenientContext()).FindEntityType(typeof(Ledger))!;

        ComparedAsOptional<CustomerId>(ledger.FindProperty(nameof(Ledger.Owner))!);
        ComparedAsOptional<Iban>(ledger.FindProperty(nameof(Ledger.PreviousIban))!);
        ledger.FindProperty(nameof(Ledger.Iban))!.GetValueComparer()
            .Should().BeOfType<ValueObjectComparer<Iban>>("a required value object keeps the comparer of the value object");

        var parcel = DesignTimeModel(new ParcelContext()).FindEntityType(typeof(Parcel))!;

        parcel.FindProperty(nameof(Parcel.Return))!.GetValueComparer()
            .Should().BeOfType<NullableValueObjectComparer<Reference<Parcel>>>();
        parcel.FindProperty(nameof(Parcel.Backorder))!.GetValueComparer()
            .Should().BeOfType<NullableValueObjectComparer<Catalog<Parcel>.Stock>>();
        parcel.FindProperty(nameof(Parcel.Order))!.GetValueComparer()
            .Should().BeOfType<ValueObjectComparer<Reference<Parcel>>>();
    }

    /// <summary>
    /// A value object written by hand over a reference type other than <see cref="string"/> has no optional converter:
    /// an optional property keeps the converter of the value object, and is compared as an optional one all the same.
    /// </summary>
    [Fact]
    public void An_optional_value_object_without_an_optional_converter_keeps_its_own_and_is_compared_as_optional()
    {
        var link = DesignTimeModel(new BookmarkContext()).FindEntityType(typeof(Bookmark))!
            .FindProperty(nameof(Bookmark.Link))!;

        link.GetValueConverter().Should().BeOfType<ValueObjectConverter<HandWrittenLink, Uri>>();
        ComparedAsOptional<HandWrittenLink>(link);
    }

    /// <summary>
    /// The comparer of an optional value object compares two present values as the value object does, here without
    /// regard to case, and an absent value equal to an absent one alone.
    /// </summary>
    [Fact]
    public void The_optional_comparer_compares_present_values_as_the_value_object_does_and_an_absent_one_with_none_but_another()
    {
        var comparer = new NullableValueObjectComparer<Ordering.OrderReference>();
        Ordering.OrderReference? upper = Ordering.OrderReference.Create("ORD-42");
        Ordering.OrderReference? lower = Ordering.OrderReference.Create("ord-42");
        Ordering.OrderReference? other = Ordering.OrderReference.Create("ORD-43");

        comparer.Type.Should().Be<Ordering.OrderReference?>();
        comparer.Equals(upper, lower).Should().BeTrue();
        comparer.Equals(upper, other).Should().BeFalse();
        comparer.Equals(upper, null).Should().BeFalse();
        comparer.Equals(null, lower).Should().BeFalse();
        comparer.Equals(null, null).Should().BeTrue();
        comparer.GetHashCode(upper).Should().Be(comparer.GetHashCode(lower));
        comparer.GetHashCode(null).Should().Be(0);
        comparer.Snapshot(upper).Should().Be(upper);
        comparer.Snapshot(null).Should().BeNull();
    }

    /// <summary>
    /// A compiled model generated for native AOT writes out the expressions of every converter and comparer, and calls
    /// what they call from the application's assembly, which Entity Framework Core asks to be public: its breaking
    /// change 35033. A converter calls its own helpers, public and out of sight of IntelliSense; a comparer calls the
    /// equality of the value object.
    /// </summary>
    [Fact]
    public void Everything_a_converter_or_a_comparer_calls_is_public_for_a_compiled_model_to_call()
    {
        ValueConverter[] converters =
        [
            new ValueObjectConverter<CustomerId, Guid>(),
            new StrictValueObjectConverter<Iban, string>(),
            new NullableValueObjectConverter<CustomerId, Guid>(),
            new StrictNullableValueObjectConverter<CustomerId, Guid>(),
            new NullableValueObjectConverter<Iban>(),
            new StrictNullableValueObjectConverter<Iban>(),
        ];

        foreach (var converter in converters)
        {
            var helpers = Called(converter.ConvertToProviderExpression, converter.ConvertFromProviderExpression);

            helpers.Select(static method => method.Name).Should().Equal("ToProvider", "FromProvider");
            helpers.Should().OnlyContain(method => IsPublic(method), $"a compiled model calls the helpers of {converter.GetType()}");
            helpers.Should().OnlyContain(
                method => method.GetCustomAttribute<EditorBrowsableAttribute>()!.State == EditorBrowsableState.Never);
        }

        ValueComparer[] comparers = [new ValueObjectComparer<CustomerId>(), new NullableValueObjectComparer<CustomerId>()];

        foreach (var comparer in comparers)
        {
            Called(comparer.EqualsExpression, comparer.HashCodeExpression, comparer.SnapshotExpression)
                .Should().OnlyContain(method => IsPublic(method), $"a compiled model writes out what {comparer.GetType()} calls");
        }

        // A compiled model calls them as the converter does.
        ValueObjectConverter<CustomerId, Guid>.FromProvider(Guid.Empty).Should().Be(CustomerId.CreateUnchecked(Guid.Empty));
        NullableValueObjectConverter<Iban>.ToProvider(null).Should().BeNull();
    }

    private static void ComparedAsOptional<TSelf>(IReadOnlyProperty property)
        where TSelf : struct, IEquatable<TSelf>
    {
        // The comparer type is what a compiled model instantiates, and wraps when it compares the value object alone.
        property.FindAnnotation("ValueComparerType")!.Value.Should().Be(typeof(NullableValueObjectComparer<TSelf>));
        property.GetValueComparer().Should().BeOfType<NullableValueObjectComparer<TSelf>>();
    }

    private static List<MethodInfo> Called(params LambdaExpression[] expressions)
    {
        var collector = new CallCollector();
        foreach (var expression in expressions)
        {
            collector.Visit(expression);
        }

        return collector.Methods;
    }

    private static bool IsPublic(MethodInfo method) => method.IsPublic && method.DeclaringType!.IsVisible;

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

    /// <summary>Collects the methods an expression calls, property getters included.</summary>
    private sealed class CallCollector : ExpressionVisitor
    {
        public List<MethodInfo> Methods { get; } = [];

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Methods.Add(node.Method);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo property)
            {
                Methods.Add(property.GetMethod!);
            }

            return base.VisitMember(node);
        }
    }

    /// <summary>A parcel, holding constructions of generic value objects of its own, optional ones included.</summary>
    private sealed class Parcel
    {
        public int Id { get; set; }

        public Reference<Parcel> Order { get; set; }

        public Reference<Parcel>? Return { get; set; }

        public Catalog<Parcel>.Stock? Backorder { get; set; }
    }

    /// <summary>A model mapping the parcel, whose constructions no other test resolves.</summary>
    private sealed class ParcelContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Parcel>();
    }

    /// <summary>An entity holding an optional value object written by hand over a reference type.</summary>
    private sealed class Bookmark
    {
        public int Id { get; set; }

        public HandWrittenLink? Link { get; set; }
    }

    /// <summary>
    /// A model mapping a value object written by hand, which nothing registers: the registry describes it once asked.
    /// </summary>
    private sealed class BookmarkContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            ValueObjectRegistry.TryResolve(typeof(HandWrittenLink), out _).Should().BeTrue();
            configurationBuilder.ConfigureValueObjects();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Bookmark>();
    }

    /// <summary>An entity holding 128-bit value objects.</summary>
    private sealed class Vault
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public LedgerBalance Balance { get; set; }

        public LedgerBalance? PreviousBalance { get; set; }

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
            configurationBuilder.Properties<LedgerBalance?>()
                .HaveConversion<LedgerBalanceToDecimal, NullableValueObjectComparer<LedgerBalance>>();
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
