using System.ComponentModel;
using System.Text.Json;
using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

/// <summary>
/// The registry and the polymorphic type, exercised against the real generated identifiers of this assembly.
/// </summary>
public class AnyEntityIdTests
{
    [Fact]
    public void The_generated_registration_publishes_every_prefix()
    {
        EntityIdRegistry.TryGetByPrefix("acc", out var account).Should().BeTrue();
        account!.ValueObjectType.Should().Be<AccountId>();
        account.Granularity.Should().Be(IdGranularity.Hour);
        account.Length.Should().Be(AccountId.Length);

        EntityIdRegistry.TryGetByPrefix("ldg_entry", out var ledger).Should().BeTrue();
        ledger!.ValueObjectType.Should().Be<LedgerEntryId>();
    }

    [Fact]
    public void An_unclaimed_prefix_resolves_to_nothing()
    {
        EntityIdRegistry.TryGetByPrefix("nope", out _).Should().BeFalse();
    }

    /// <summary>
    /// A prefix may hold separators of its own, so the split cannot be taken at the first one. The body length
    /// is what makes the boundary computable.
    /// </summary>
    [Fact]
    public void Resolution_handles_a_multi_segment_prefix()
    {
        var id = LedgerEntryId.New();

        EntityIdRegistry.TryResolve(id.Value, out var descriptor).Should().BeTrue();
        descriptor!.Prefix.Should().Be("ldg_entry");
    }

    [Fact]
    public void Parse_resolves_the_type_from_the_prefix()
    {
        var account = AccountId.New();

        var any = AnyEntityId.Parse(account.Value);

        any.ValueObjectType.Should().Be<AccountId>();
        any.Prefix.Should().Be("acc");
        any.Value.Should().Be(account.Value);
        any.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Is_and_TryConvertTo_agree_with_the_resolved_type()
    {
        var any = AnyEntityId.Parse(EventId.New().Value);

        any.Is<EventId>().Should().BeTrue();
        any.Is<AccountId>().Should().BeFalse();

        any.TryConvertTo<EventId>(out var typed).Should().BeTrue();
        typed.Value.Should().Be(any.Value);

        any.TryConvertTo<AccountId>(out _).Should().BeFalse();
    }

    [Fact]
    public void ToValueObject_hands_back_the_boxed_identifier()
    {
        var subscription = SubscriptionId.New();

        AnyEntityId.Parse(subscription.Value).ToValueObject().Should().Be(subscription);
    }

    [Fact]
    public void An_unregistered_prefix_is_reported_as_such()
    {
        AnyEntityId.TryParse("zzz_2k7x9wqmz4h3n8vyb6tcr", null, out _, out var validation).Should().BeFalse();

        validation.ErrorCode.Should().Be(IdentifierErrorCodes.UnknownPrefix);
    }

    [Fact]
    public void A_registered_prefix_with_a_corrupt_body_is_reported_differently()
    {
        // The distinction matters: one is "I have never heard of this kind of thing", the other is "I know
        // exactly what this is and it is damaged". A caller cannot write a useful message without it.
        var value = AccountId.New().Value;
        var corrupted = string.Concat(value.AsSpan(0, value.Length - 1), value[^1] == 'Z' ? "Y" : "Z");

        AnyEntityId.TryParse(corrupted, null, out _, out var validation).Should().BeFalse();

        validation.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidChecksum);
    }

    [Fact]
    public void Parse_normalizes_on_the_way_in()
    {
        var account = AccountId.New();

        AnyEntityId.Parse($"  {account.Value.ToLowerInvariant()}  ").Value.Should().Be(account.Value);
    }

    [Fact]
    public void Equality_follows_the_text()
    {
        var account = AccountId.New();

        AnyEntityId.Parse(account.Value).Should().Be(AnyEntityId.Parse(account.Value.ToLowerInvariant()));
        AnyEntityId.Parse(account.Value).Should().NotBe(AnyEntityId.Parse(SubscriptionId.New().Value));
    }

    /// <summary>
    /// It is a transport type before anything else, so it has to cross a JSON boundary the way every other
    /// identifier does: as the bare text. Reflected over instead, it would write an object carrying a value, a
    /// prefix and a type name.
    /// </summary>
    [Fact]
    public void It_serializes_as_the_bare_identifier()
    {
        var any = AnyEntityId.Parse(AccountId.New().Value);

        var json = JsonSerializer.Serialize(any);

        json.Should().Be($"\"{any.Value}\"");
        JsonSerializer.Deserialize<AnyEntityId>(json).Should().Be(any);
    }

    [Fact]
    public void Deserializing_something_no_type_claims_fails_loudly()
    {
        var act = () => JsonSerializer.Deserialize<AnyEntityId>("\"zzz_nope\"");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void It_converts_through_the_type_descriptor_for_model_binding()
    {
        var any = AnyEntityId.Parse(EventId.New().Value);
        var converter = TypeDescriptor.GetConverter(typeof(AnyEntityId));

        converter.ConvertFromString(any.Value).Should().Be(any);
        converter.ConvertToString(any).Should().Be(any.Value);
    }

    /// <summary>
    /// The line that keeps a polymorphic column from ever being mapped: the persistence integrations key off
    /// these interfaces, and this type implements neither.
    /// </summary>
    [Fact]
    public void The_polymorphic_type_is_not_a_value_object()
    {
        typeof(IValueObject).IsAssignableFrom(typeof(AnyEntityId)).Should().BeFalse();
        typeof(IEntityId).IsAssignableFrom(typeof(AnyEntityId)).Should().BeFalse();

        typeof(IValueObject).IsAssignableFrom(typeof(AccountId)).Should().BeTrue();
        typeof(IEntityId).IsAssignableFrom(typeof(AccountId)).Should().BeTrue();
    }

    /// <summary>
    /// Only a genuine collision throws; a module initializer running twice must not. The collision itself is
    /// caught a layer earlier, by <c>VO0016</c> at compile time, and asserted in the generator suite — two
    /// types in this assembly cannot share a prefix, which is exactly the point.
    /// </summary>
    [Fact]
    public void Re_registering_the_same_type_is_a_no_op()
    {
        var again = () => EntityIdRegistry.Register(EntityIdDescriptor.For<SubscriptionId>());

        again.Should().NotThrow();
        EntityIdRegistry.TryGetByPrefix("sub", out var descriptor).Should().BeTrue();
        descriptor!.ValueObjectType.Should().Be<SubscriptionId>();
    }
}
