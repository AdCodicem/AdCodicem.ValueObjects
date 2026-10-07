using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.MessagePack;
using AdCodicem.ValueObjects.Metadata;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.SignalR;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.MessagePackWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectResolver"/> and the two calls that put it in front of a resolver: which types it answers, what
/// the options keep, and value objects inside the objects and collections an application writes.
/// </summary>
public sealed class MessagePackResolverTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    private const string OtherIban = "DE89370400440532013000";

    [Fact]
    public void It_answers_nothing_but_a_value_object()
    {
        foreach (var resolver in new[] { ValueObjectResolver.Instance, ValueObjectResolver.Trusted })
        {
            resolver.GetFormatter<string>().Should().BeNull();
            resolver.GetFormatter<int>().Should().BeNull();
            resolver.GetFormatter<Guid>().Should().BeNull();
            resolver.GetFormatter<DateOnly>().Should().BeNull();
            resolver.GetFormatter<AnyEntityId>().Should().BeNull();

            // MessagePack's NullableFormatter, of the next resolver, writes the nil and asks for Iban otherwise.
            resolver.GetFormatter<Iban?>().Should().BeNull();
        }
    }

    [Fact]
    public void It_answers_a_value_object_with_a_formatter_of_its_trust_built_once()
    {
        var strict = ValueObjectResolver.Instance.GetFormatter<Iban>();
        var trusted = ValueObjectResolver.Trusted.GetFormatter<Iban>();

        strict.Should().BeOfType<ValueObjectFormatter<Iban, string>>().Which.Trusted.Should().BeFalse();
        trusted.Should().BeOfType<ValueObjectFormatter<Iban, string>>().Which.Trusted.Should().BeTrue();
        ValueObjectResolver.Instance.GetFormatter<Iban>().Should().BeSameAs(strict);
        ValueObjectResolver.Trusted.GetFormatter<Iban>().Should().BeSameAs(trusted);
    }

    /// <summary>
    /// <see cref="AnyEntityId"/> is no value object, and nothing of this package writes it: MessagePack's standard
    /// resolver refuses it, as any type it has no formatter for.
    /// </summary>
    [Fact]
    public void Any_entity_identifier_is_left_to_the_options_own_resolver()
    {
        ValueObjectRegistry.IsValueObject(typeof(AnyEntityId)).Should().BeFalse();

        FluentActions.Invoking(() => MessagePackSerializer.Serialize(AnyEntityId.Parse(AccountId.New().Value), Strict))
            .Should().Throw<MessagePackSerializationException>()
            .WithInnerException<FormatterNotRegisteredException>();
    }

    [Fact]
    public void With_value_objects_puts_the_resolver_first_and_keeps_every_other_setting()
    {
        var options = Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithOmitAssemblyVersion(true);

        var wired = options.WithValueObjects();

        wired.Should().NotBeSameAs(options);
        wired.Security.Should().BeSameAs(MessagePackSecurity.UntrustedData);
        wired.Compression.Should().Be(MessagePackCompression.Lz4BlockArray);
        wired.OmitAssemblyVersion.Should().BeTrue();
        wired.Resolver.GetFormatter<Iban>().Should().BeSameAs(ValueObjectResolver.Instance.GetFormatter<Iban>());
        wired.Resolver.GetFormatter<string>().Should().BeSameAs(StandardResolver.Instance.GetFormatter<string>());
        options.WithValueObjects(trusted: true).Resolver.GetFormatter<Iban>()
            .Should().BeSameAs(ValueObjectResolver.Trusted.GetFormatter<Iban>());
    }

    /// <summary>
    /// A contractless resolver, which writes any type through its public members, writes a value object as an empty map,
    /// read back as the default instance; in front of it, the value object's resolver answers first.
    /// </summary>
    [Fact]
    public void In_front_of_a_contractless_resolver_a_value_object_is_written_as_its_value_and_no_longer_as_an_empty_map()
    {
        var order = new ContractlessOrder { Iban = Iban.Create(ValidIban) };

        MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(order, ContractlessStandardResolver.Options))
            .Should().Be("""{"Iban":{}}""");
        var wired = ContractlessStandardResolver.Options.WithValueObjects();
        var written = MessagePackSerializer.Serialize(order, wired);

        MessagePackSerializer.ConvertToJson(written).Should().Be($$"""{"Iban":"{{ValidIban}}"}""");
        MessagePackSerializer.Deserialize<ContractlessOrder>(written, wired).Iban.Should().Be(order.Iban);
    }

    [Fact]
    public void Called_twice_the_last_call_answers_first()
    {
        var refused = Bytes((short)5000);

        MessagePackSerializer.Deserialize<Quantity>(refused, Standard.WithValueObjects().WithValueObjects(trusted: true))
            .Value.Should().Be(5000);
        Code(FluentActions.Invoking(() => MessagePackSerializer.Deserialize<Quantity>(
                refused,
                Standard.WithValueObjects(trusted: true).WithValueObjects()))
            .Should().Throw<MessagePackSerializationException>().Which).Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void Use_value_objects_wires_the_hub_protocol_keeping_its_security_and_returns_its_options()
    {
        var options = new MessagePackHubProtocolOptions();
        options.SerializerOptions.Security.Should().BeSameAs(MessagePackSecurity.UntrustedData, "SignalR sets it");
        options.SerializerOptions.Resolver.GetFormatter<Iban>().Should().NotBeOfType<ValueObjectFormatter<Iban, string>>();
        options.SerializerOptions = options.SerializerOptions.WithCompression(MessagePackCompression.Lz4BlockArray);

        options.UseValueObjects().Should().BeSameAs(options);

        options.SerializerOptions.Security.Should().BeSameAs(MessagePackSecurity.UntrustedData);
        options.SerializerOptions.Compression.Should().Be(MessagePackCompression.Lz4BlockArray, "the application set it before");
        options.SerializerOptions.Resolver.GetFormatter<Iban>().Should().BeSameAs(ValueObjectResolver.Instance.GetFormatter<Iban>());

        // SignalR's own resolver still answers a class declared for no serializer, as a hub argument usually is.
        var order = new ContractlessOrder { Iban = Iban.Create(ValidIban) };
        var written = MessagePackSerializer.Serialize(order, options.SerializerOptions);
        MessagePackSerializer.ConvertToJson(written, options.SerializerOptions).Should().Be($$"""{"Iban":"{{ValidIban}}"}""");
        MessagePackSerializer.Deserialize<ContractlessOrder>(written, options.SerializerOptions).Iban.Should().Be(order.Iban);
        new MessagePackHubProtocolOptions().UseValueObjects(trusted: true).SerializerOptions.Resolver.GetFormatter<Iban>()
            .Should().BeSameAs(ValueObjectResolver.Trusted.GetFormatter<Iban>());
    }

    [Fact]
    public void Both_calls_require_their_options()
    {
        FluentActions.Invoking(() => ((MessagePackSerializerOptions)null!).WithValueObjects())
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(() => ((MessagePackHubProtocolOptions)null!).UseValueObjects())
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    /// <summary>
    /// An object of value objects is written as the same object of the primitives they replace is, byte for byte, its
    /// nullable members, collections and dictionary keys included.
    /// </summary>
    [Fact]
    public void An_object_of_value_objects_is_written_as_the_same_object_of_primitives()
    {
        var customer = CustomerId.New();
        var order = new ValueObjectOrder
        {
            Customer = customer,
            Account = Iban.Create(ValidIban),
            Quantity = Quantity.Create(3),
            Discount = null,
            Stock = new() { [Iban.Create(OtherIban)] = Quantity.Create(7) },
            Countries = [CountryCode.France, CountryCode.Belgium],
        };
        var primitives = new PrimitiveOrder
        {
            Customer = customer.Value,
            Account = ValidIban,
            Quantity = 3,
            Discount = null,
            Stock = new() { [OtherIban] = 7 },
            Countries = ["FR", "BE"],
        };

        var written = MessagePackSerializer.Serialize(order, Strict);

        written.Should().Equal(MessagePackSerializer.Serialize(primitives, Standard));
        var read = MessagePackSerializer.Deserialize<ValueObjectOrder>(written, Strict);
        read.Customer.Should().Be(customer);
        read.Account.Should().Be(order.Account);
        read.Quantity.Should().Be(order.Quantity);
        read.Discount.Should().BeNull();
        read.Stock.Should().Equal(order.Stock);
        read.Countries.Should().Equal(order.Countries);
    }

    [Fact]
    public void A_member_or_a_key_the_type_refuses_fails_the_object_with_its_code()
    {
        var refusedMember = new PrimitiveOrder { Customer = Guid.NewGuid(), Account = ValidIban, Quantity = 1001 };
        var refusedKey = new PrimitiveOrder { Customer = Guid.NewGuid(), Account = ValidIban, Stock = new() { ["FR76"] = 1 } };

        Refused(refusedMember).Should().Be(ValueObjectErrorCodes.OutOfRange);
        Refused(refusedKey).Should().Be(ValueObjectErrorCodes.TooShort);

        static string? Refused(PrimitiveOrder primitives)
        {
            var thrown = FluentActions.Invoking(() => MessagePackSerializer.Deserialize<ValueObjectOrder>(
                    MessagePackSerializer.Serialize(primitives, Standard),
                    Strict))
                .Should().Throw<MessagePackSerializationException>().Which;
            thrown.Message.Should().Be($"Failed to deserialize {typeof(ValueObjectOrder).FullName} value.");

            return Code(thrown);
        }
    }

    /// <summary>
    /// Under <see cref="MessagePackSecurity.UntrustedData"/>, which SignalR's options set, MessagePack refuses a dictionary
    /// keyed by a value object and a set of value objects, for want of a hash-collision-resistant comparer of the value
    /// object; a security of the application's own that compares them as their type does reads them.
    /// </summary>
    [Fact]
    public void Untrusted_data_refuses_value_object_keys_until_the_application_gives_a_comparer()
    {
        var untrusted = Strict.WithSecurity(MessagePackSecurity.UntrustedData);

        Untrusted(new Dictionary<Iban, Quantity> { [Iban.Create(ValidIban)] = Quantity.Create(2) }, untrusted, "Iban");
        Untrusted(new Dictionary<Quantity, int> { [Quantity.Create(2)] = 1 }, untrusted, "Quantity");
        Untrusted(new HashSet<CustomerId> { CustomerId.New() }, untrusted, "CustomerId");

        var keyed = Strict.WithSecurity(new ValueObjectKeysSecurity(MessagePackSecurity.UntrustedData));
        var stock = new Dictionary<Iban, Quantity> { [Iban.Create(ValidIban)] = Quantity.Create(2) };
        keyed.Security.HashCollisionResistant.Should().BeTrue();
        MessagePackSerializer.Deserialize<Dictionary<Iban, Quantity>>(MessagePackSerializer.Serialize(stock, keyed), keyed)
            .Should().Equal(stock);
        MessagePackSerializer.Deserialize<Dictionary<string, int>>(MessagePackSerializer.Serialize(new Dictionary<string, int> { ["a"] = 1 }, keyed), keyed)
            .Should().ContainKey("a");

        static void Untrusted<T>(T value, MessagePackSerializerOptions options, string name)
        {
            var written = MessagePackSerializer.Serialize(value, options);

            FluentActions.Invoking(() => MessagePackSerializer.Deserialize<T>(written, options))
                .Should().Throw<MessagePackSerializationException>()
                .WithInnerException<TypeAccessException>().WithMessage($"*{name}");
        }
    }

    /// <summary>
    /// Keys are read through the rules too, so two keys the sender held apart can normalize to one value object:
    /// MessagePack's dictionary then fails to add the second, with an exception of its own that carries no code and
    /// quotes the key, which no formatter can intercept. A trusted read normalizes nothing and keeps both.
    /// </summary>
    [Fact]
    public void Two_keys_that_normalize_to_one_value_object_fail_the_dictionary_without_a_code()
    {
        var written = Bytes(new Dictionary<string, int> { [ValidIban] = 1, ["fr76 3000 6000 0112 3456 7890 189"] = 2 });

        var thrown = FluentActions.Invoking(() => MessagePackSerializer.Deserialize<Dictionary<Iban, int>>(written, Strict))
            .Should().Throw<MessagePackSerializationException>().Which;

        thrown.InnerException.Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Be($"An item with the same key has already been added. Key: {ValidIban}");
        Code(thrown).Should().BeNull();
        MessagePackSerializer.Deserialize<Dictionary<Iban, int>>(written, Trusting).Should().HaveCount(2);
        MessagePackSerializer.Deserialize<HashSet<Iban>>(Bytes(new[] { ValidIban, "fr76 3000 6000 0112 3456 7890 189" }), Strict)
            .Should().Equal(Iban.Create(ValidIban));
    }

    /// <summary>
    /// The workaround the guide shows: a security that compares a value object as its own equality does, at the cost of
    /// collision resistance for those keys alone.
    /// </summary>
    private sealed class ValueObjectKeysSecurity : MessagePackSecurity
    {
        public ValueObjectKeysSecurity(MessagePackSecurity copyFrom)
            : base(copyFrom)
        {
        }

        protected override IEqualityComparer<T> GetHashCollisionResistantEqualityComparer<T>()
            => ValueObjectRegistry.IsValueObject(typeof(T))
                ? EqualityComparer<T>.Default
                : base.GetHashCollisionResistantEqualityComparer<T>();

        protected override MessagePackSecurity Clone() => new ValueObjectKeysSecurity(this);
    }
}

/// <summary>
/// An object written by a contractless resolver, through its public members.
/// </summary>
public sealed class ContractlessOrder
{
    public Iban Iban { get; set; }
}

/// <summary>
/// An object of value objects, as an application declares one for MessagePack.
/// </summary>
[MessagePackObject]
public sealed class ValueObjectOrder
{
    [Key(0)]
    public CustomerId Customer { get; set; }

    [Key(1)]
    public Iban Account { get; set; }

    [Key(2)]
    public Quantity Quantity { get; set; }

    [Key(3)]
    public Quantity? Discount { get; set; }

    [Key(4)]
    public Dictionary<Iban, Quantity> Stock { get; set; } = [];

    [Key(5)]
    public List<CountryCode> Countries { get; set; } = [];
}

/// <summary>
/// The same object, of the primitives the value objects replace.
/// </summary>
[MessagePackObject]
public sealed class PrimitiveOrder
{
    [Key(0)]
    public Guid Customer { get; set; }

    [Key(1)]
    public string Account { get; set; } = string.Empty;

    [Key(2)]
    public short Quantity { get; set; }

    [Key(3)]
    public short? Discount { get; set; }

    [Key(4)]
    public Dictionary<string, short> Stock { get; set; } = [];

    [Key(5)]
    public List<string> Countries { get; set; } = [];
}
