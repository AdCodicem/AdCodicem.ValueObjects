using System.Text;
using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Orleans;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Orleans.Serialization;
using Orleans.Serialization.Codecs;
using static AdCodicem.ValueObjects.UnitTests.Grains.OrleansWire;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// <see cref="ValueObjectCodec{TSelf, TValue}"/> and the two registrations, through Orleans' serializer: a value object
/// written as the underlying value it replaces, with its type where the member is typed <see cref="object"/>, read back
/// through its rules unless trusted, refused on write when its type rejects its default, and each refusal carrying its
/// rule's code.
/// </summary>
/// <remarks>
/// Grain calls are <see cref="OrleansGrainCallTests"/>'s, and the exceptions a grain lets escape
/// <see cref="OrleansExceptionTests"/>'s.
/// </remarks>
public sealed class OrleansCodecTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is written as the bare value it carries is,
    /// reads back what that value reads back, and refuses what its rules refuse with their codes.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_written_as_the_value_it_carries(string type)
        => Samples.All[type].RoundTripsThroughOrleansAsItsUnderlyingValue();

    /// <summary>
    /// A message holding value objects alone, nullable, in a list, a dictionary and an array is written byte for byte as
    /// its twin over primitives, and each reads the other's bytes.
    /// </summary>
    [Fact]
    public void An_order_of_value_objects_is_written_as_its_twin_over_primitives()
    {
        var id = Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10");
        var order = new GrainOrder
        {
            Id = GrainOrderId.Create(id),
            Sku = GrainSku.Create("abc-123"),
            Quantity = GrainQuantity.Create(3),
            Discount = null,
            Skus = [GrainSku.Create("abc-123"), GrainSku.Create("xyz-999")],
            Stock = new() { [GrainSku.Create("xyz-999")] = GrainQuantity.Create(7) },
            Amount = GrainAmount.Create(12.5m),
            Due = GrainDueDate.Create(new DateOnly(2026, 10, 17)),
            Gift = GrainGiftWrap.Create(true),
            Weight = GrainWeight.Create(1.25),
            Related = [GrainOrderId.Create(id)],
            Optional = GrainSku.Create("opt-001"),
        };

        // Each string a distinct instance, as each normalizer makes one: Orleans writes a string it met before as a reference.
        var plain = new PlainGrainOrder
        {
            Id = id,
            Sku = Distinct("ABC-123"),
            Quantity = 3,
            Discount = null,
            Skus = [Distinct("ABC-123"), Distinct("XYZ-999")],
            Stock = new() { [Distinct("XYZ-999")] = 7 },
            Amount = 12.5m,
            Due = new DateOnly(2026, 10, 17),
            Gift = true,
            Weight = 1.25,
            Related = [id],
            Optional = Distinct("OPT-001"),
        };

        var written = Strict.SerializeToArray(order);
        written.Should().Equal(Bare.SerializeToArray(plain));

        var back = Strict.Read<GrainOrder>(written);
        back.Should().BeEquivalentTo(order);
        Bare.Read<PlainGrainOrder>(written).Should().BeEquivalentTo(plain);
        Strict.Read<GrainOrder>(Bare.SerializeToArray(plain)).Should().BeEquivalentTo(order);
    }

    [Fact]
    public void A_nullable_value_object_is_written_as_a_nullable_primitive_is()
    {
        Strict.SerializeToArray(new GrainHolder<GrainQuantity?> { Value = null })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<int?> { Value = null }));
        Strict.SerializeToArray(new GrainHolder<GrainQuantity?> { Value = GrainQuantity.Create(2) })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<int?> { Value = 2 }));

        Strict.Read<GrainHolder<GrainQuantity?>>(Bare.SerializeToArray(new GrainHolder<int?> { Value = null })).Value.Should().BeNull();
        Strict.Read<GrainHolder<GrainQuantity?>>(Bare.SerializeToArray(new GrainHolder<int?> { Value = 2 })).Value
            .Should().Be(GrainQuantity.Create(2));
        Strict.Deserialize<GrainQuantity?>(Strict.SerializeToArray<GrainQuantity?>(null)).Should().BeNull();
    }

    [Fact]
    public void A_value_object_met_twice_is_written_as_a_reference_to_its_text_as_the_text_is()
    {
        var sku = GrainSku.Create("abc-123");
        var text = sku.Value;

        Strict.SerializeToArray(new GrainHolder<List<GrainSku>> { Value = [sku, sku] })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<List<string>> { Value = [text, text] }));
        Strict.Read<GrainHolder<List<GrainSku>>>(Strict.SerializeToArray(new GrainHolder<List<GrainSku>> { Value = [sku, sku] })).Value
            .Should().Equal(sku, sku);
    }

    /// <summary>
    /// A value object held by a member typed <see cref="object"/> is written with its type, an object whose field 0 is the
    /// underlying value, and reads back as itself, alone, in a collection, nullable, or a construction of a generic value
    /// object the registry describes.
    /// </summary>
    [Fact]
    public void A_value_object_in_a_member_typed_object_is_written_with_its_type_and_reads_back_as_itself()
    {
        var written = Strict.SerializeToArray(new GrainBox { Value = GrainSku.Create("abc-123") });
        Encoding.UTF8.GetString(written).Should().Contain("AdCodicem.ValueObjects.Fixtures.Grains.GrainSku");
        Strict.Read<GrainBox>(written).Value.Should().Be(GrainSku.Create("abc-123"));

        object[] values =
        [
            GrainQuantity.Create(5),
            GrainOrderId.Create(Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10")),
            new List<GrainSku> { GrainSku.Create("abc-123") },
            new GrainQuantity?[] { GrainQuantity.Create(1), null },
            Reference<OrleansCodecOwner>.Create("po-1"),
            Iban.Create(ValidIban),
        ];
        foreach (var value in values)
        {
            Strict.Read<GrainBox>(Strict.SerializeToArray(new GrainBox { Value = value })).Value
                .Should().BeEquivalentTo(value, "a {0} in a member typed object reads back as itself", value.GetType().Name);
        }

        Strict.Deserialize<object>(Strict.SerializeToArray<object>(GrainSku.Create("abc-123"))).Should().Be(GrainSku.Create("abc-123"));
    }

    /// <summary>
    /// A value object written with its type takes its place among the references of the message, as Orleans' own structs
    /// do, so a reference written after it still points at what it named.
    /// </summary>
    [Fact]
    public void A_value_object_written_with_its_type_keeps_the_references_after_it()
    {
        var text = Distinct("shared");
        object[] values = [GrainSku.Create("abc-123"), text, text, GrainQuantity.Create(3), text];

        var read = Strict.Read<GrainHolder<object[]>>(Strict.SerializeToArray(new GrainHolder<object[]> { Value = values })).Value;

        read.Should().Equal(GrainSku.Create("ABC-123"), "shared", "shared", GrainQuantity.Create(3), "shared");
    }

    [Fact]
    public void An_alias_replaces_the_name_of_the_value_object_on_the_wire()
    {
        var written = Strict.SerializeToArray(new GrainBox { Value = AliasedCode.Create("ab") });

        Encoding.UTF8.GetString(written).Should().Contain("fixtures.grains.aliased-code").And.NotContain(nameof(AliasedCode));
        Strict.Read<GrainBox>(written).Value.Should().Be(AliasedCode.Create("ab"));
    }

    [Fact]
    public void A_value_read_goes_through_the_rules_and_is_normalized()
    {
        Strict.Read<GrainHolder<Iban>>(Primitive("fr76 3000 6000 0112 3456 7890 189")).Value.Should().Be(Iban.Create(ValidIban));
        Strict.Read<GrainHolder<GrainSku>>(Primitive(" abc-123 ")).Value.Should().Be(GrainSku.Create("ABC-123"));
    }

    /// <summary>
    /// A value the type refuses is refused with the rule that fired, its message, and its code, never with the value.
    /// </summary>
    [Fact]
    public void Each_rule_refuses_a_value_with_its_own_code_and_never_its_value()
    {
        Refused<Iban>(Primitive(string.Empty), ValueObjectErrorCodes.Required);
        Refused<Iban>(Primitive("FR76"), ValueObjectErrorCodes.TooShort);
        Refused<Iban>(Primitive("FR76" + new string('1', 31)), ValueObjectErrorCodes.TooLong);
        Refused<Iban>(Primitive("FR7630006000011234567890@89"), ValueObjectErrorCodes.InvalidFormat);
        Refused<Iban>(Primitive("FR7630006000011234567890188"), ValueObjectErrorCodes.InvalidFormat);
        Refused<GrainQuantity>(Primitive(500), ValueObjectErrorCodes.OutOfRange);
        Refused<CountryCode>(Primitive("ZZ"), ValueObjectErrorCodes.NotAKnownValue);
        Refused<CustomerId>(Primitive(Guid.Empty), ValueObjectErrorCodes.Required);

        var refusal = Refused<GrainQuantity>(Primitive(500), ValueObjectErrorCodes.OutOfRange);
        refusal.Message.Should().Be("The value read is not a valid GrainQuantity: The value must be less than or equal to 100.");
        refusal.InnerException.Should().BeNull();
    }

    [Fact]
    public void An_element_or_a_dictionary_value_is_refused_as_a_member_is()
    {
        Refused<List<GrainSku>>(Bare.SerializeToArray(new GrainHolder<List<string>> { Value = ["ABC-123", "TOO-LONG-1"] }), ValueObjectErrorCodes.TooLong);
        Refused<Dictionary<GrainSku, GrainQuantity>>(
            Bare.SerializeToArray(new GrainHolder<Dictionary<string, int>> { Value = new() { ["ABC-123"] = 500 } }),
            ValueObjectErrorCodes.OutOfRange);
        Refused<GrainQuantity?>(Bare.SerializeToArray(new GrainHolder<int?> { Value = 0 }), ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_value_object_in_a_member_typed_object_is_read_through_its_rules()
    {
        var forged = Strict.SerializeToArray(new GrainBox { Value = Unchecked.Of<GrainQuantity, int>(1000) });

        var refusal = FluentActions.Invoking(() => Strict.Read<GrainBox>(forged)).Should().Throw<SerializerException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        Trusted.Read<GrainBox>(forged).Value.Should().Be(Unchecked.Of<GrainQuantity, int>(1000));
    }

    [Fact]
    public void A_null_is_refused_as_required_whatever_the_trust()
    {
        foreach (var serializer in new[] { Strict, Trusted })
        {
            var refusal = Refused<GrainSku>(Primitive<string?>(null), ValueObjectErrorCodes.Required, serializer);
            refusal.Message.Should().Be("A null cannot be read as GrainSku; declare the member as GrainSku? instead.");
        }
    }

    /// <summary>
    /// A value the codec of the underlying value cannot read is refused as not parsable, whichever exception that codec
    /// throws, and never quotes it.
    /// </summary>
    [Fact]
    public void A_value_the_underlying_codec_cannot_read_is_refused_as_not_parsable()
    {
        var refusal = Refused<Bic>(Primitive(3), ValueObjectErrorCodes.NotParsable);
        refusal.Message.Should().Be("The Orleans value read cannot be read as Bic, a value object over String.");

        Refused<PageNumber>(Primitive("x"), ValueObjectErrorCodes.NotParsable);
        Refused<PageNumber>(Primitive(5_000_000_000L), ValueObjectErrorCodes.NotParsable);
        Refused<Grade>(Primitive(70000), ValueObjectErrorCodes.NotParsable);
        Refused<EffectiveDate>(Primitive(int.MaxValue), ValueObjectErrorCodes.NotParsable);
        Refused<RecordedAt>(Primitive(long.MaxValue), ValueObjectErrorCodes.NotParsable);
        Refused<OpeningTime>(Primitive(long.MaxValue), ValueObjectErrorCodes.NotParsable);
        Refused<Ratio>(Primitive(1e300), ValueObjectErrorCodes.NotParsable);
        Refused<CustomerId>(Primitive("abc"), ValueObjectErrorCodes.NotParsable).InnerException.Should().BeNull();

        // A narrower value of a wider type is read, as Orleans reads it for the primitive.
        Strict.Read<GrainHolder<PageNumber>>(Primitive(7L)).Value.Should().Be(PageNumber.Create(7));
    }

    [Fact]
    public void Truncated_bytes_fail_as_Orleans_reader_fails_them()
    {
        var written = Strict.SerializeToArray(GrainSku.Create("abc-123"));

        FluentActions.Invoking(() => Strict.Deserialize<GrainSku>(written[..^2])).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void A_value_object_that_refuses_without_a_reason_is_refused_as_not_parsable()
    {
        var serializer = Build(static builder => builder.AddValueObject<HandWrittenId<MuteProfile>, string>());

        var refusal = Refused<HandWrittenId<MuteProfile>>(Primitive("mute_anything"), ValueObjectErrorCodes.NotParsable, serializer);
        refusal.Message.Should().StartWith("The value read is not a valid HandWrittenId`1");
    }

    /// <summary>
    /// A value object written with its type, which a later version may write with more fields, is read from field 0, any
    /// other field skipped; without field 0 it is refused as not parsable.
    /// </summary>
    [Fact]
    public void A_value_object_written_with_its_type_is_read_from_its_field_zero()
    {
        var extra = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(GrainSku));
            StringCodec.WriteField(ref writer, 0, "abc-123");
            StringCodec.WriteField(ref writer, 1, "extra");
            writer.WriteEndObject();
        });
        Strict.Deserialize<object>(extra).Should().Be(GrainSku.Create("ABC-123"));

        var missing = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(GrainSku));
            StringCodec.WriteField(ref writer, 1, "extra");
            writer.WriteEndObject();
        });
        var refusal = FluentActions.Invoking(() => Strict.Deserialize<object>(missing)).Should().Throw<SerializerException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.NotParsable);

        var refusedInside = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(GrainQuantity));
            Int32Codec.WriteField(ref writer, 0, 500);
            writer.WriteEndObject();
        });
        FluentActions.Invoking(() => Strict.Deserialize<object>(refusedInside)).Should().Throw<SerializerException>()
            .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_member_the_message_leaves_out_holds_the_default_instance()
    {
        var older = Bare.SerializeToArray(new GrainNothing());

        var read = Strict.Read<GrainHolder<GrainQuantity>>(older).Value;
        ((IValueObject<GrainQuantity, int>)read).IsDefault.Should().BeTrue("Orleans never asks the codec for a member it does not find");
    }

    /// <summary>
    /// The default instance of a type that refuses its value is refused on write, before anything is sent; one whose value
    /// the type accepts is written as its value is.
    /// </summary>
    [Fact]
    public void A_default_is_written_only_when_its_type_accepts_its_value()
    {
#pragma warning disable VO0010 // the default instances, on purpose
        var quantity = FluentActions.Invoking(() => Strict.SerializeToArray(new GrainHolder<GrainQuantity> { Value = default }))
            .Should().Throw<SerializerException>().Which;
        quantity.Message.Should().Be("The value to write is not a valid GrainQuantity: The value must be greater than or equal to 1.");
        quantity.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
        FluentActions.Invoking(() => Strict.SerializeToArray(new GrainBox { Value = default(GrainSku) }))
            .Should().Throw<SerializerException>().Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.Required);

        Strict.SerializeToArray(new GrainHolder<GrainAmount> { Value = default })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<decimal> { Value = 0m }));
#pragma warning restore VO0010
        Strict.SerializeToArray(new GrainHolder<GrainQuantity> { Value = Unchecked.Of<GrainQuantity, int>(500) })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<int> { Value = 500 }), "an instance that is not the default is written as it is");
    }

    [Fact]
    public void A_trusted_read_neither_validates_nor_normalizes()
    {
        Trusted.Read<GrainHolder<GrainQuantity>>(Primitive(500)).Value.Value.Should().Be(500);
        Trusted.Read<GrainHolder<GrainSku>>(Primitive(" nope ")).Value.Value.Should().Be(" nope ");
        Build(static builder => builder.AddValueObject<GrainQuantity, int>(trusted: true))
            .Read<GrainHolder<GrainQuantity>>(Primitive(500)).Value.Value.Should().Be(500);
    }

    [Fact]
    public void A_codec_is_strict_unless_it_is_told_to_trust()
    {
        new ValueObjectCodec<GrainSku, string>().Trusted.Should().BeFalse();
        new ValueObjectCodec<GrainSku, string>(trusted: true).Trusted.Should().BeTrue();
        new ValueObjectCodecSelector(trusted: false).Trusted.Should().BeFalse();
        new ValueObjectCodecSelector(trusted: true).Trusted.Should().BeTrue();
    }

    /// <summary>
    /// Each registration answers before the ones made earlier: the last trust applies, and a typed registration made after
    /// <c>AddValueObjects</c> answers for its type.
    /// </summary>
    [Fact]
    public void The_last_registration_answers_first()
    {
        var forged = Primitive(500);

        Read(builder => builder.AddValueObjects().AddValueObjects(trusted: true)).Should().Be(500);
        ReadRefused(builder => builder.AddValueObjects(trusted: true).AddValueObjects());
        Read(builder => builder.AddValueObjects().AddValueObject<GrainQuantity, int>(trusted: true)).Should().Be(500);
        ReadRefused(builder => builder.AddValueObject<GrainQuantity, int>(trusted: true).AddValueObjects());

        int Read(Action<ISerializerBuilder> configure) => Build(configure).Read<GrainHolder<GrainQuantity>>(forged).Value.Value;

        void ReadRefused(Action<ISerializerBuilder> configure)
            => FluentActions.Invoking(() => Read(configure)).Should().Throw<SerializerException>()
                .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The typed registration serializes its value object without the registry, a construction of a generic one included,
    /// and allows it where Orleans writes its type; a value object it does not name has no codec.
    /// </summary>
    [Fact]
    public void A_typed_registration_serializes_its_value_object_alone()
    {
        var serializer = Build(static builder => builder.AddValueObject<Reference<OrleansTypedOwner>, string>());

        var holder = serializer.SerializeToArray(new GrainHolder<Reference<OrleansTypedOwner>> { Value = Reference<OrleansTypedOwner>.Create("po-2") });
        holder.Should().Equal(Bare.SerializeToArray(new GrainHolder<string> { Value = "PO-2" }));
        serializer.Read<GrainHolder<Reference<OrleansTypedOwner>>>(holder).Value.Should().Be(Reference<OrleansTypedOwner>.Create("PO-2"));
        serializer.Read<GrainBox>(serializer.SerializeToArray(new GrainBox { Value = Reference<OrleansTypedOwner>.Create("po-3") })).Value
            .Should().Be(Reference<OrleansTypedOwner>.Create("PO-3"));
        Refused<Reference<OrleansTypedOwner>>(Primitive("PO-123456789012"), ValueObjectErrorCodes.TooLong, serializer);

        FluentActions.Invoking(() => serializer.SerializeToArray(new GrainHolder<GrainSku> { Value = GrainSku.Create("abc-123") }))
            .Should().Throw<CodecNotFoundException>();
    }

    [Fact]
    public void A_value_object_written_by_hand_is_resolved_by_the_registry()
    {
        var code = Declared<OrleansCodeDeclaration, string>.Create("ab");

        var written = Strict.SerializeToArray(new GrainHolder<Declared<OrleansCodeDeclaration, string>> { Value = code });
        written.Should().Equal(Bare.SerializeToArray(new GrainHolder<string> { Value = "ab" }));
        Strict.Read<GrainHolder<Declared<OrleansCodeDeclaration, string>>>(written).Value.Should().Be(code);
        Strict.Read<GrainBox>(Strict.SerializeToArray(new GrainBox { Value = code })).Value.Should().Be(code);
        Refused<Declared<OrleansCodeDeclaration, string>>(Primitive("abcd"), ValueObjectErrorCodes.TooLong);
    }

    [Fact]
    public void Each_registration_refuses_a_missing_builder()
    {
        FluentActions.Invoking(static () => ValueObjectSerializerBuilderExtensions.AddValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(static () => ValueObjectSerializerBuilderExtensions.AddValueObject<GrainSku, string>(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    /// <summary>
    /// The selector answers for a value object, never for a nullable one, which Orleans' own codec of a nullable value
    /// serves by asking for the value object's, nor for any other type. Orleans never asks it for a nullable value: this
    /// is called directly.
    /// </summary>
    [Fact]
    public void The_selector_answers_for_value_objects_alone()
    {
        var selector = new ValueObjectCodecSelector(trusted: false);

        selector.IsSupportedType(typeof(GrainSku)).Should().BeTrue();
        selector.IsSupportedType(typeof(GrainSku?)).Should().BeFalse();
        selector.IsSupportedType(typeof(Guid)).Should().BeFalse();
        selector.IsSupportedType(typeof(GrainOrder)).Should().BeFalse();
        selector.GetSpecializedCodec(typeof(GrainSku)).Should().BeOfType<ValueObjectCodec<GrainSku, string>>()
            .Which.Trusted.Should().BeFalse();
    }

    [Fact]
    public void Without_the_package_Orleans_finds_no_codec_for_a_value_object()
    {
        FluentActions.Invoking(() => Bare.SerializeToArray(GrainSku.Create("abc-123"))).Should().Throw<CodecNotFoundException>();
    }

    private static string Distinct(string text) => new(text.AsSpan());

    private static byte[] Primitive<TValue>(TValue value) => Bare.SerializeToArray(new GrainHolder<TValue> { Value = value });

    private static SerializerException Refused<T>(byte[] bytes, string code, Serializer? serializer = null)
    {
        var refusal = FluentActions.Invoking(() => (serializer ?? Strict).Read<GrainHolder<T>>(bytes))
            .Should().Throw<SerializerException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var carried).Should().BeTrue();
        carried.Should().Be(code);
        return refusal;
    }
}

/// <summary>The owner of the references the registry describes for <see cref="OrleansCodecTests"/>, which nothing else names.</summary>
public sealed class OrleansCodecOwner;

/// <summary>The owner of the references a typed registration serializes, which nothing else names.</summary>
public sealed class OrleansTypedOwner;

/// <summary>A code of up to three characters, written by hand, which nothing else declares.</summary>
public sealed class OrleansCodeDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };

    public static ValidationResult Validate(string value)
        => value is null ? ValidationResult.Required() : value.Length > 3 ? ValidationResult.Failure(ValueObjectErrorCodes.TooLong, "Too long.") : ValidationResult.Success;
}
