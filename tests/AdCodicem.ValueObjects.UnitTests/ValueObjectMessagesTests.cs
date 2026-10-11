using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The keys a refusal's message is looked up under, and the arguments the declared rules supply it, formatted for the
/// culture: the table every integration localises a message with.
/// </summary>
public sealed class ValueObjectMessagesTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// The bounds of every value object of the domain that declares one, written in French: a number with its decimal
    /// comma, a date in the day-first order, an empty text for the bound the type leaves out.
    /// </summary>
    public static TheoryData<Type, string, string> FrenchBounds => new()
    {
        { typeof(Grade), "A", "F" },
        { typeof(Adjustment), "-10", "10" },
        { typeof(Score), string.Empty, "100" },
        { typeof(Port), "1", string.Empty },
        { typeof(PageNumber), "1", string.Empty },
        { typeof(FileSize), "0", string.Empty },
        { typeof(LedgerBalance), "-1000000000000000000000", "1000000000000000000000" },
        { typeof(Latitude), "-90", "90" },
        { typeof(Ratio), "0", "1" },
        { typeof(Tolerance), string.Empty, "1" },
        { typeof(Quantity), "0", "1000" },
        { typeof(Amount), "0", string.Empty },
        { typeof(Percentage), "0", "100" },
        { typeof(BirthDate), "01/01/1900", "31/12/2100" },
        { typeof(EffectiveDate), "01/01/2000", string.Empty },
        { typeof(OpeningTime), "06:00", "12:00" },
        { typeof(RecordedAt), "01/01/2000 00:00:00", "31/12/2099 00:00:00" },
        { typeof(OccurredAt), "01/01/2000 00:00:00 +00:00", string.Empty },
        { typeof(Duration), "00:00:00", "1.00:00:00" },
    };

    [Theory]
    [InlineData(ValueObjectErrorCodes.TooShort, "ValueObject.value_object.too_short")]
    [InlineData("iban.check_digits", "ValueObject.iban.check_digits")]
    public void The_key_of_a_code_is_the_prefix_followed_by_the_code(string code, string key)
        => ValueObjectMessages.KeyFor(code).Should().Be(key).And.StartWith(ValueObjectMessages.KeyPrefix);

    [Fact]
    public void A_code_that_is_null_or_empty_has_no_key()
    {
        FluentActions.Invoking(() => ValueObjectMessages.KeyFor(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ValueObjectMessages.KeyFor(string.Empty)).Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// A range declaring one bound is looked up under a key naming that bound first, then under the key of the code; a
    /// range declaring both, or none, and any other code, under the key of the code alone.
    /// </summary>
    [Fact]
    public void A_range_of_one_bound_is_looked_up_under_the_key_naming_it_first()
    {
        const string Key = "ValueObject.value_object.out_of_range";

        ValueObjectMessages.KeysFor(PageNumber.Schema, ValueObjectErrorCodes.OutOfRange).Should().Equal(Key + ".minimum", Key);
        ValueObjectMessages.KeysFor(Score.Schema, ValueObjectErrorCodes.OutOfRange).Should().Equal(Key + ".maximum", Key);
        ValueObjectMessages.KeysFor(Quantity.Schema, ValueObjectErrorCodes.OutOfRange).Should().Equal(Key);
        ValueObjectMessages.KeysFor(ValueObjectSchema.Unconstrained, ValueObjectErrorCodes.OutOfRange).Should().Equal(Key);
        ValueObjectMessages.KeysFor(HandWrittenLevel.Schema, ValueObjectErrorCodes.OutOfRange).Should().Equal(Key + ".minimum", Key);
        ValueObjectMessages.KeysFor(Score.Schema, ValueObjectErrorCodes.TooLong).Should().Equal("ValueObject.value_object.too_long");
    }

    [Fact]
    public void The_keys_need_a_schema_and_a_code()
    {
        FluentActions.Invoking(() => ValueObjectMessages.KeysFor(null!, ValueObjectErrorCodes.OutOfRange)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ValueObjectMessages.KeysFor(Score.Schema, string.Empty)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_length_and_the_known_values_are_the_arguments_of_their_codes()
    {
        Arguments<CountryCode, string>(ValueObjectErrorCodes.TooShort, French).Should().Equal(Argument("MinLength", "2"));
        Arguments<CountryCode, string>(ValueObjectErrorCodes.TooLong, French).Should().Equal(Argument("MaxLength", "2"));
        Arguments<CountryCode, string>(ValueObjectErrorCodes.NotAKnownValue, French).Should().Equal(Argument("KnownValues", "FR; BE; LU"));
        Arguments<CountryCode, string>(ValueObjectErrorCodes.NotAKnownValue, English).Should().Equal(Argument("KnownValues", "FR, BE, LU"));
        Arguments<CountryCode, string>(ValueObjectErrorCodes.NotAKnownValue, CultureInfo.InvariantCulture).Should().Equal(Argument("KnownValues", "FR, BE, LU"));
    }

    /// <summary>
    /// A localised message is derived from the rule declared once: the length a construction of a generic value object
    /// declares is the one it quotes.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_quotes_the_length_it_declares()
    {
        var maxLength = Reference<PurchaseOrder>.Schema.MaxLength!.Value;

        Arguments<Reference<PurchaseOrder>, string>(ValueObjectErrorCodes.TooLong, French)
            .Should().Equal(Argument("MaxLength", maxLength.ToString(French)));
        maxLength.Should().Be(12);
    }

    [Theory]
    [InlineData(ValueObjectErrorCodes.InvalidFormat)]
    [InlineData(ValueObjectErrorCodes.Required)]
    [InlineData(ValueObjectErrorCodes.NotParsable)]
    [InlineData("iban.check_digits")]
    public void A_code_outside_the_table_has_no_argument(string code)
        => Arguments<Iban, string>(code, French).Should().BeEmpty();

    [Theory]
    [MemberData(nameof(FrenchBounds))]
    public void The_bounds_of_every_underlying_type_are_written_for_the_culture(Type valueObject, string minimum, string maximum)
    {
        var descriptor = Descriptor(valueObject);

        var arguments = Texts(ValueObjectMessages.ArgumentsFor(descriptor, ValueObjectErrorCodes.OutOfRange, French));

        arguments.Should().Equal(Argument("Minimum", minimum), Argument("Maximum", maximum));
        Texts(descriptor.Accept(new TypedArguments(ValueObjectErrorCodes.OutOfRange, French))).Should().Equal(arguments);
    }

    /// <summary>
    /// A fraction takes the culture's decimal separator. A descriptor registered by hand may hold rules set apart from
    /// the type's, which are those the integrations reading the registry enforce: its bounds are the ones quoted.
    /// </summary>
    [Fact]
    public void A_fraction_is_written_with_the_culture_s_decimal_separator()
    {
        var amount = ValueObjectDescriptor.For<Amount, decimal>(Amount.Schema with { Minimum = "0.01" });
        var latitude = ValueObjectDescriptor.For<Latitude, double>(Latitude.Schema with { Minimum = "0.5" });
        var ratio = ValueObjectDescriptor.For<Ratio, float>(Ratio.Schema with { Minimum = "0.5" });

        ValueObjectMessages.ArgumentsFor(amount, ValueObjectErrorCodes.OutOfRange, French)[0].Value.Should().Be("0,01");
        ValueObjectMessages.ArgumentsFor(amount, ValueObjectErrorCodes.OutOfRange, English)[0].Value.Should().Be("0.01");
        ValueObjectMessages.ArgumentsFor(latitude, ValueObjectErrorCodes.OutOfRange, French)[0].Value.Should().Be("0,5");
        ValueObjectMessages.ArgumentsFor(ratio, ValueObjectErrorCodes.OutOfRange, French)[0].Value.Should().Be("0,5");
    }

    [Fact]
    public void A_bound_of_every_other_integer_type_is_read_back()
    {
        var withMinimum = ValueObjectSchema.Unconstrained with { Minimum = "1", Maximum = "4000000000" };

        Bounds(ValueObjectDescriptor.For<SequenceNumber, uint>(withMinimum)).Should().Equal("1", "4000000000");
        Bounds(ValueObjectDescriptor.For<ByteCount, ulong>(withMinimum)).Should().Equal("1", "4000000000");
        Bounds(ValueObjectDescriptor.For<Fingerprint, UInt128>(withMinimum)).Should().Equal("1", "4000000000");
    }

    /// <summary>
    /// A bound the underlying type cannot read, which a schema written by hand may hold, or of a type that takes none,
    /// is quoted as it is written.
    /// </summary>
    [Fact]
    public void A_bound_the_underlying_type_cannot_read_is_kept_as_written()
    {
        var unreadable = ValueObjectDescriptor.For<Amount, decimal>(Amount.Schema with { Minimum = "abc" });
        var unbounded = ValueObjectDescriptor.For<HandWrittenLink, Uri>(HandWrittenLink.Schema with { Maximum = "z" });
        var instant = ValueObjectDescriptor.For<RecordedAt, DateTime>(RecordedAt.Schema with { Maximum = "not a date" });

        Bounds(unreadable).Should().Equal("abc", string.Empty);
        Bounds(unbounded).Should().Equal(string.Empty, "z");
        Bounds(instant).Should().Equal("01/01/2000 00:00:00", "not a date");
    }

    [Fact]
    public void Known_numbers_and_dates_are_written_for_the_culture()
    {
        Arguments<VatRate, decimal>(ValueObjectErrorCodes.NotAKnownValue, French).Should().Equal(Argument("KnownValues", "20,0; 5,5"));
        Arguments<CutOffDate, DateOnly>(ValueObjectErrorCodes.NotAKnownValue, French).Should().Equal(Argument("KnownValues", "01/01/2000; 01/01/2001"));
        Arguments<Quantity, short>(ValueObjectErrorCodes.NotAKnownValue, French).Should().Equal(Argument("KnownValues", string.Empty));
    }

    /// <summary>A schema written by hand gives what it declares, and an empty text for what it does not.</summary>
    [Fact]
    public void A_value_object_written_by_hand_gives_what_its_schema_declares()
    {
        Arguments<HandWrittenLevel, int>(ValueObjectErrorCodes.OutOfRange, French).Should().Equal(Argument("Minimum", "1"), Argument("Maximum", string.Empty));
        Arguments<HandWrittenRating, int>(ValueObjectErrorCodes.OutOfRange, French).Should().Equal(Argument("Minimum", string.Empty), Argument("Maximum", string.Empty));
        Arguments<HandWrittenCode, string>(ValueObjectErrorCodes.TooLong, French).Should().Equal(Argument("MaxLength", string.Empty));
        Arguments<HandWrittenCode, string>(ValueObjectErrorCodes.TooShort, French).Should().Equal(Argument("MinLength", string.Empty));
    }

    /// <summary>
    /// Without a provider, the current culture formats the arguments. A provider that is not a culture formats the
    /// values, and the current culture gives the list separator.
    /// </summary>
    [Fact]
    public void Without_a_provider_the_current_culture_formats_the_arguments()
    {
        var current = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = French;

            Arguments<VatRate, decimal>(ValueObjectErrorCodes.NotAKnownValue, provider: null).Should().Equal(Argument("KnownValues", "20,0; 5,5"));
            Arguments<VatRate, decimal>(ValueObjectErrorCodes.NotAKnownValue, NumberFormatInfo.InvariantInfo).Should().Equal(Argument("KnownValues", "20.0; 5.5"));
            Arguments<BirthDate, DateOnly>(ValueObjectErrorCodes.OutOfRange, provider: null).Should().Equal(Argument("Minimum", "01/01/1900"), Argument("Maximum", "31/12/2100"));
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
        }
    }

    /// <summary>
    /// The typed overload reads the type's own schema, and the registered descriptor holds the same: both give the same
    /// arguments, for a generated value object and for a construction of a generic one.
    /// </summary>
    [Fact]
    public void The_typed_arguments_are_those_of_the_registered_descriptor()
    {
        ValueObjectRegistry.TryResolve(typeof(Reference<PurchaseOrder>), out var reference).Should().BeTrue();

        Texts(ValueObjectMessages.ArgumentsFor(Descriptor(typeof(Quantity)), ValueObjectErrorCodes.OutOfRange, French))
            .Should().Equal(Arguments<Quantity, short>(ValueObjectErrorCodes.OutOfRange, French));
        Texts(ValueObjectMessages.ArgumentsFor(reference!, ValueObjectErrorCodes.TooLong, French))
            .Should().Equal(Arguments<Reference<PurchaseOrder>, string>(ValueObjectErrorCodes.TooLong, French));
    }

    [Fact]
    public void The_arguments_need_a_descriptor_and_a_code()
    {
        FluentActions.Invoking(() => ValueObjectMessages.ArgumentsFor(null!, ValueObjectErrorCodes.TooLong, French)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ValueObjectMessages.ArgumentsFor(Descriptor(typeof(Quantity)), null!, French)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => Arguments<Quantity, short>(string.Empty, French)).Should().Throw<ArgumentException>();
    }

    private static List<string> Arguments<TSelf, TValue>(string code, IFormatProvider? provider)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Texts(ValueObjectMessages.ArgumentsFor<TSelf, TValue>(code, provider));

    /// <summary>Writes each argument as its name and its text, in order, so that two lists compare position by position.</summary>
    private static List<string> Texts(IReadOnlyList<KeyValuePair<string, object?>> arguments)
        => [.. arguments.Select(static argument => $"{argument.Key}={argument.Value}")];

    private static IEnumerable<object?> Bounds(ValueObjectDescriptor descriptor)
        => ValueObjectMessages.ArgumentsFor(descriptor, ValueObjectErrorCodes.OutOfRange, French).Select(static argument => argument.Value);

    private static string Argument(string name, string value) => $"{name}={value}";

    private static ValueObjectDescriptor Descriptor(Type valueObject)
    {
        ValueObjectRegistry.TryGet(valueObject, out var descriptor).Should().BeTrue();

        return descriptor!;
    }

    /// <summary>Reads the arguments through the typed overload, closed over the types a descriptor hands back.</summary>
    private sealed class TypedArguments(string code, IFormatProvider provider) : IValueObjectVisitor<IReadOnlyList<KeyValuePair<string, object?>>>
    {
        public IReadOnlyList<KeyValuePair<string, object?>> Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => ValueObjectMessages.ArgumentsFor<TSelf, TValue>(code, provider);
    }
}
