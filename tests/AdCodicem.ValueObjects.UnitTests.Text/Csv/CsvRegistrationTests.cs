using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// How the converters reach a CsvHelper context: the factory, which claims every value object and closes its converter
/// through the descriptor's visitor, and the typed registration, which asks no registry.
/// </summary>
public sealed class CsvRegistrationTests
{
    private const string NotDescribed =
        "CsvHelper meets 'string', which is not a value object the registry describes. Under native AOT, a value object written "
        + "by hand, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a construction of a generic one, "
        + "ValueObjectRegistry.Register<TSelf, TValue>(static () => new TSelf.ValueJsonConverter()), have to be registered "
        + "before CsvHelper meets them, or their converter added with AddValueObject<TSelf, TValue>().";

    [Theory]
    [InlineData(typeof(Iban), true)]
    [InlineData(typeof(Quantity), true)]
    [InlineData(typeof(Iban?), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(CsvOrder), false)]
    [InlineData(typeof(MarkerOnlyValue), false)]
    [InlineData(typeof(SelflessValue), false)]
    public void The_factory_claims_a_value_object_and_never_its_nullable_type(Type type, bool claimed)
        => ValueObjectTypeConverterFactory.Instance.CanCreate(type).Should().Be(claimed);

    [Fact]
    public void The_factory_creates_the_converter_of_each_value_object_and_CsvHelper_wraps_it_for_the_nullable_type()
    {
        var cache = new TypeConverterCache().AddValueObjects();

        cache.GetConverter<Iban>().Should().BeOfType<ValueObjectTypeConverter<Iban, string>>();
        cache.GetConverter<CustomerId>().Should().BeOfType<ValueObjectTypeConverter<CustomerId, Guid>>();
        cache.GetConverter<BirthDate>().Should().BeOfType<ValueObjectTypeConverter<BirthDate, DateOnly>>();
        cache.GetConverter<Ordering.OrderReference>().Should().BeOfType<ValueObjectTypeConverter<Ordering.OrderReference, string>>();
        cache.GetConverter<Quantity?>().Should().BeOfType<NullableConverter>()
            .Which.UnderlyingTypeConverter.Should().BeOfType<ValueObjectTypeConverter<Quantity, short>>();
        cache.GetConverter<string>().Should().NotBeAssignableTo<ValueObjectTypeConverter<Iban, string>>();
        ValueObjectTypeConverterFactory.Instance.Create(typeof(Quantity), cache, out var converter).Should().BeTrue();
        converter.Should().BeOfType<ValueObjectTypeConverter<Quantity, short>>();
    }

    [Fact]
    public void The_factory_describes_a_construction_of_a_generic_value_object_and_one_written_by_hand_the_first_time_it_meets_them()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<CsvOwner>), out _).Should().BeFalse();
        ValueObjectRegistry.TryGet(typeof(Declared<CsvTicket, string>), out _).Should().BeFalse();

        CsvHarness.Read<CsvRow<Reference<CsvOwner>>>("Value\n po-1042 \n").Single().Value.Should().Be(Reference<CsvOwner>.Create("PO-1042"));
        CsvHarness.Read<CsvRow<Declared<CsvTicket, string>>>("Value\nT-7\n").Single().Value.Should().Be(Declared<CsvTicket, string>.Create("T-7"));
        CsvHarness.Write([new CsvRow<Reference<CsvOwner>> { Value = Reference<CsvOwner>.Create("PO-1042") }]).Should().Be("Value\nPO-1042\n");

        var refusal = CsvHarness.Refusal<CsvRow<Reference<CsvOwner>>>("Value\nPO-1042-1042-1042\n");
        refusal.Code().Should().Be(ValueObjectErrorCodes.TooLong);
        refusal.FirstLine().Should().StartWith("The field is not a valid Reference<CsvOwner>: ");
        CsvHarness.Refusal<CsvRow<Declared<CsvTicket, string>>>("Value\nT-123456789\n").FirstLine()
            .Should().Be("The field is not a valid Declared<CsvTicket, string>: A ticket is at most 8 characters long.");

        ValueObjectRegistry.TryGet(typeof(Reference<CsvOwner>), out _).Should().BeTrue();
        ValueObjectRegistry.TryGet(typeof(Declared<CsvTicket, string>), out _).Should().BeTrue();
    }

    [Fact]
    public void AddValueObject_reads_and_writes_a_value_object_written_by_hand_without_registering_it()
    {
        var registered = ValueObjectRegistry.TryGet(typeof(HandWrittenCode), out _);
        static void Typed(CsvContext context) => context.TypeConverterCache.AddValueObject<HandWrittenCode, string>();

        CsvHarness.Read<CsvRow<HandWrittenCode>>("Value\nABC\n", setup: Typed).Single().Value.Should().Be(HandWrittenCode.Create("ABC"));
        CsvHarness.Read<CsvRow<HandWrittenCode?>>("Value\n\"\"\n", setup: Typed).Single().Value.Should().BeNull();
        CsvHarness.Write([new CsvRow<HandWrittenCode> { Value = HandWrittenCode.Create("ABC") }], setup: Typed).Should().Be("Value\nABC\n");
        HandWrittenCode.TryParse("A1", CultureInfo.InvariantCulture, out _, out var expected).Should().BeFalse();
        CsvHarness.Refusal<CsvRow<HandWrittenCode>>("Value\nA1\n", setup: Typed).Code().Should().Be(expected.ErrorCode);

        ValueObjectRegistry.TryGet(typeof(HandWrittenCode), out _).Should().Be(registered, "the typed registration asks no registry");
    }

    [Fact]
    public void A_converter_the_application_adds_for_a_type_wins_over_the_factory_in_either_order()
    {
        var own = new DefaultTypeConverter();
        var before = new TypeConverterCache();
        before.AddConverter<Iban>(own);
        before.AddValueObjects();
        var after = new TypeConverterCache().AddValueObjects();
        after.AddConverter<Iban>(own);

        before.GetConverter<Iban>().Should().BeSameAs(own);
        after.GetConverter<Iban>().Should().BeSameAs(own);
        new TypeConverterCache().AddValueObjects().AddValueObject<Iban, string>().GetConverter<Iban>()
            .Should().BeOfType<ValueObjectTypeConverter<Iban, string>>();
    }

    [Fact]
    public void The_factory_asked_for_a_type_that_is_not_a_value_object_throws_naming_both_registrations()
        => FluentActions.Invoking(static () => ValueObjectTypeConverterFactory.Instance.Create(typeof(string), new TypeConverterCache(), out _))
            .Should().Throw<InvalidOperationException>().WithMessage(NotDescribed).Which.Code().Should().BeNull();

    [Fact]
    public void Each_extension_returns_its_cache_for_chaining()
    {
        var cache = new TypeConverterCache();

        cache.AddValueObjects().Should().BeSameAs(cache);
        cache.AddValueObject<Iban, string>().Should().BeSameAs(cache);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        using var reader = new CsvReader(new StringReader(string.Empty), CultureInfo.InvariantCulture);
        using var writer = new CsvWriter(new StringWriter(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var converter = new ValueObjectTypeConverter<Iban, string>();
        var data = new MemberMapData(null);

        FluentActions.Invoking(static () => ValueObjectTypeConverterCacheExtensions.AddValueObjects(null!)).Should().Throw<ArgumentNullException>().WithParameterName("cache");
        FluentActions.Invoking(static () => ValueObjectTypeConverterCacheExtensions.AddValueObject<Iban, string>(null!)).Should().Throw<ArgumentNullException>().WithParameterName("cache");
        FluentActions.Invoking(static () => ValueObjectTypeConverterFactory.Instance.CanCreate(null!)).Should().Throw<ArgumentNullException>().WithParameterName("type");
        FluentActions.Invoking(static () => ValueObjectTypeConverterFactory.Instance.Create(null!, new TypeConverterCache(), out _)).Should().Throw<ArgumentNullException>().WithParameterName("type");
        FluentActions.Invoking(() => converter.ConvertFromString("x", null!, data)).Should().Throw<ArgumentNullException>().WithParameterName("row");
        FluentActions.Invoking(() => converter.ConvertFromString("x", reader, null!)).Should().Throw<ArgumentNullException>().WithParameterName("memberMapData");
        FluentActions.Invoking(() => converter.ConvertToString(null, null!, data)).Should().Throw<ArgumentNullException>().WithParameterName("row");
        FluentActions.Invoking(() => converter.ConvertToString(null, writer, null!)).Should().Throw<ArgumentNullException>().WithParameterName("memberMapData");
    }
}
