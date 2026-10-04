using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.Extensions.Configuration;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

/// <summary>
/// The registry and the polymorphic type, exercised against the real generated identifiers of this assembly.
/// </summary>
public partial class AnyEntityIdTests
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
    public void The_default_instance_is_empty_and_belongs_to_no_type()
    {
        var none = default(AnyEntityId);

        none.IsDefault.Should().BeTrue();
        none.Value.Should().BeEmpty();
        none.Prefix.Should().BeEmpty();
        none.ValueObjectType.Should().BeNull();
        none.Is<AccountId>().Should().BeFalse();
        none.TryConvertTo<AccountId>(out var typed).Should().BeFalse();
        ((IValueObject<AccountId, string>)typed).IsDefault.Should().BeTrue();
        none.ToValueObject().Should().BeNull();
        none.ToString().Should().BeEmpty();
        none.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void TryParse_over_a_span_resolves_the_type_or_leaves_the_default()
    {
        var account = AccountId.New();

        AnyEntityId.TryParse(account.Value.AsSpan(), null, out var any).Should().BeTrue();
        AnyEntityId.TryParse("zzz_nope".AsSpan(), null, out var none).Should().BeFalse();

        any.ValueObjectType.Should().Be<AccountId>();
        none.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Parse_throws_carrying_the_rule_that_fired()
    {
        var value = AccountId.New().Value;
        var corrupted = string.Concat(value.AsSpan(0, value.Length - 1), value[^1] == 'z' ? "y" : "z");

        var unknown = () => AnyEntityId.Parse("zzz_2k7x9wqmz4h3n8vyb6tcr");
        var corrupt = () => AnyEntityId.Parse(corrupted);

        var thrown = unknown.Should().Throw<ValueObjectException>().Which;
        thrown.ErrorCode.Should().Be(IdentifierErrorCodes.UnknownPrefix);
        thrown.ValueObjectType.Should().Be<AnyEntityId>();
        thrown.AttemptedValue.Should().Be("zzz_2k7x9wqmz4h3n8vyb6tcr");
        thrown.Message.Should().Be(
            "'AnyEntityId' rejected the supplied text: The text carries no prefix belonging to a registered identifier type.",
            "the message names the rule, never the text");
        corrupt.Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidChecksum);
    }

    /// <summary>
    /// A hand-written identifier may refuse a text without saying why. Parse still throws with a code, so a caller
    /// turning it into an error response always has one.
    /// </summary>
    [Fact]
    public void An_identifier_type_that_refuses_without_a_reason_is_reported_as_unparsable()
    {
        EntityIdRegistry.Register<HandWrittenId<MuteProfile>>();
        var text = EntityIdFormat.Create(MuteProfile.Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        var act = () => AnyEntityId.Parse(text);
        var value = () => JsonSerializer.Deserialize<AnyEntityId>(JsonSerializer.Serialize(text));
        var key = () => JsonSerializer.Deserialize<Dictionary<AnyEntityId, int>>($$"""{"{{text}}":1}""");

        act.Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        value.Should().Throw<ValueObjectJsonException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        key.Should().Throw<ValueObjectJsonException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// The text is taken from the value of the identifier the type built, and a hand-written type may hand back none.
    /// Such an identifier is resolved, so not the default, and converts to nothing rather than to an empty identifier.
    /// </summary>
    [Fact]
    public void An_identifier_whose_type_gives_back_no_value_converts_to_nothing()
    {
        EntityIdRegistry.Register<HandWrittenId<BlankProfile>>();
        var text = EntityIdFormat.Create(BlankProfile.Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        var any = AnyEntityId.Parse(text);

        any.IsDefault.Should().BeFalse();
        any.Value.Should().BeEmpty();
        any.ToValueObject().Should().BeNull();
    }

    /// <summary>
    /// The conversion parses the text again, against rules that may have changed since: a grant revoked after its
    /// identifier was read no longer converts.
    /// </summary>
    [Fact]
    public void An_identifier_its_type_no_longer_accepts_converts_to_nothing()
    {
        var any = AnyEntityId.Parse(RevocableId.New().Value);

        try
        {
            RevocableId.Revoked.Value = any.Value;

            any.ToValueObject().Should().BeNull();
            any.TryConvertTo<RevocableId>(out _).Should().BeFalse();
        }
        finally
        {
            RevocableId.Revoked.Value = null;
        }
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
        //
        // The replacement has to be a canonical, lower-case symbol. An upper-case one is folded back down by
        // normalization, so whenever the check character already was that letter — one run in 32 — the
        // "corrupt" text normalized to the original identifier and parsed.
        var value = AccountId.New().Value;
        var corrupted = string.Concat(value.AsSpan(0, value.Length - 1), value[^1] == 'z' ? "y" : "z");

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

    [Fact]
    public void Two_spellings_of_one_identifier_are_one_value_and_one_key()
    {
        var account = AccountId.New();
        var canonical = AnyEntityId.Parse(account.Value);
        var lower = AnyEntityId.Parse(account.Value.ToLowerInvariant());

        (canonical == lower).Should().BeTrue();
        (canonical != AnyEntityId.Parse(SubscriptionId.New().Value)).Should().BeTrue();
        new HashSet<AnyEntityId> { canonical, lower }.Should().ContainSingle();
    }

    [Fact]
    public void An_AnyEntityId_never_equals_the_typed_identifier_it_wraps()
    {
        var account = AccountId.New();
        var any = AnyEntityId.Parse(account.Value);

        any.Equals((object)AnyEntityId.Parse(account.Value)).Should().BeTrue();
        any.Equals((object)account).Should().BeFalse();
        any.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void It_formats_as_its_canonical_text_into_a_string_or_a_span()
    {
        var any = AnyEntityId.Parse(AccountId.New().Value);
        var exact = new char[any.Value.Length];

        any.ToString().Should().Be(any.Value);
        any.ToString(null, CultureInfo.InvariantCulture).Should().Be(any.Value);
        $"{any}".Should().Be(any.Value);
        any.TryFormat(exact, out var written, default, null).Should().BeTrue();
        new string(exact, 0, written).Should().Be(any.Value);
        any.TryFormat(new char[any.Value.Length - 1], out var none, default, null).Should().BeFalse();
        none.Should().Be(0);
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

    /// <summary>
    /// A formatting hook decides how an identifier reads in a log, not what it is: the polymorphic reference keeps
    /// the text that parses back, and hands back the identifier it was parsed from.
    /// </summary>
    [Fact]
    public void An_identifier_whose_formatting_hook_shortens_it_keeps_its_canonical_text()
    {
        var key = ApiKeyId.New();
        key.ToString().Should().NotBe(key.Value, "the hook shortens it");

        var any = AnyEntityId.Parse(key.Value);

        any.Value.Should().Be(key.Value);
        any.TryConvertTo<ApiKeyId>(out var typed).Should().BeTrue();
        typed.Should().Be(key);
        any.ToValueObject().Should().Be(key);
        JsonSerializer.Serialize(any).Should().Be($"\"{key.Value}\"");
    }

    [Fact]
    public void It_serves_as_a_JSON_dictionary_key()
    {
        var any = AnyEntityId.Parse(AccountId.New().Value);

        var json = JsonSerializer.Serialize(new Dictionary<AnyEntityId, int> { [any] = 1 });

        json.Should().Be($$"""{"{{any.Value}}":1}""");
        JsonSerializer.Deserialize<Dictionary<AnyEntityId, int>>(json).Should().ContainKey(any);
    }

    [Fact]
    public void A_JSON_token_that_is_not_a_string_is_refused()
    {
        var act = () => JsonSerializer.Deserialize<AnyEntityId>("42");

        var refusal = act.Should().Throw<ValueObjectJsonException>().WithMessage("*found Number*").Which;
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        refusal.ValueObjectType.Should().Be<AnyEntityId>();
    }

    /// <summary>
    /// The message says what was refused, never the text: a message is what every log records.
    /// </summary>
    [Fact]
    public void Deserializing_something_no_type_claims_fails_loudly()
    {
        var act = () => JsonSerializer.Deserialize<AnyEntityId>("\"zzz_nope\"");

        var refusal = act.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The value is not an identifier of any registered type.").Which;
        refusal.Message.Should().NotContain("zzz_nope");
        refusal.ErrorCode.Should().Be(IdentifierErrorCodes.UnknownPrefix, "the code is the one AnyEntityId.TryParse reports");
        refusal.Path.Should().Be("$");
    }

    /// <summary>
    /// A required reference sent as null must fail where it arrives, as it does for every typed identifier, rather
    /// than pass as a default that belongs to no type and fails much later. An optional one is declared optional.
    /// </summary>
    [Fact]
    public void A_JSON_null_is_refused_unless_the_identifier_is_optional()
    {
        var act = () => JsonSerializer.Deserialize<AnyEntityId>("null");

        act.Should().Throw<ValueObjectJsonException>().WithMessage("*found Null*")
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        JsonSerializer.Deserialize<AnyEntityId?>("null").Should().BeNull();
    }

    /// <summary>
    /// ASP.NET Core turns a <see cref="JsonException"/> from a request body into a 400 and lets anything else
    /// through as a 500, so a key no type claims has to be reported the way a value no type claims is.
    /// </summary>
    [Fact]
    public void A_dictionary_key_no_type_claims_is_refused_as_JSON()
    {
        var act = () => JsonSerializer.Deserialize<Dictionary<AnyEntityId, int>>("""{"zzz_nope":1}""");

        var refusal = act.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The dictionary key is not an identifier of any registered type.").Which;
        refusal.Message.Should().NotContain("zzz_nope", "a key is the text of a value, which a log must not record");
        refusal.ErrorCode.Should().Be(IdentifierErrorCodes.UnknownPrefix);
    }

    [Fact]
    public void It_converts_through_the_type_descriptor_for_model_binding()
    {
        var any = AnyEntityId.Parse(EventId.New().Value);
        var converter = TypeDescriptor.GetConverter(typeof(AnyEntityId));

        converter.ConvertFromString(any.Value).Should().Be(any);
        converter.ConvertToString(any).Should().Be(any.Value);
    }

    [Fact]
    public void It_binds_from_configuration_through_its_type_converter()
    {
        var account = AccountId.New();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Target"] = account.Value })
            .Build();

        var options = new WebhookOptions();
        configuration.Bind(options);

        options.Target.Should().Be(AnyEntityId.Parse(account.Value));
    }

    [Fact]
    public void The_type_converter_converts_from_and_to_text_and_nothing_else()
    {
        var converter = TypeDescriptor.GetConverter(typeof(AnyEntityId));
        var any = AnyEntityId.Parse(EventId.New().Value);

        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
        converter.CanConvertFrom(typeof(int)).Should().BeFalse();
        converter.CanConvertTo(typeof(string)).Should().BeTrue();
        converter.CanConvertTo(typeof(int)).Should().BeFalse();

        converter.ConvertToString(null).Should().BeEmpty();
        FluentActions.Invoking(() => converter.ConvertFrom(42)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => converter.ConvertTo(any, typeof(int))).Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// A null is no identifier, and the converter of every generated identifier refuses it: this one does not turn it
    /// into a default that belongs to no type.
    /// </summary>
    [Fact]
    public void The_type_converter_refuses_null_as_a_generated_identifier_converter_does()
    {
        var converter = TypeDescriptor.GetConverter(typeof(AnyEntityId));

        FluentActions.Invoking(() => converter.ConvertFrom(null!)).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => TypeDescriptor.GetConverter(typeof(AccountId)).ConvertFrom(null!))
            .Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// The non-generic contract answers what the static members of the type say, for code that holds an identifier
    /// only as an object - what <see cref="AnyEntityId.ToValueObject"/> hands back.
    /// </summary>
    [Fact]
    public void The_non_generic_contract_exposes_the_profile_of_the_type()
    {
        var boxed = (IEntityId)AnyEntityId.Parse(EventId.New().Value).ToValueObject()!;

        boxed.PrefixValue.Should().Be(EventId.Prefix);
        boxed.GranularityValue.Should().Be(IdGranularity.Minute);
        ((IEntityId)AccountId.New()).GranularityValue.Should().Be(IdGranularity.Hour);
    }

    /// <summary>
    /// Within one compilation <c>VO0016</c> refuses two types sharing a prefix; across assemblies only the registry
    /// can, and the first claim must survive the attempt.
    /// </summary>
    [Fact]
    public void A_second_type_claiming_a_registered_prefix_is_refused()
    {
        var act = () => EntityIdRegistry.Register<HandWrittenId<ImpostorProfile>>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*HandWrittenId*AccountId*both claim the prefix 'acc'*");
        EntityIdRegistry.TryGetByPrefix("acc", out var descriptor).Should().BeTrue();
        descriptor!.ValueObjectType.Should().Be<AccountId>();
    }

    [Fact]
    public void The_registry_lists_every_identifier_type_registered_so_far()
    {
        EntityIdRegistry.GetRegistered().Select(descriptor => descriptor.ValueObjectType).Should()
            .Contain([typeof(AccountId), typeof(SubscriptionId), typeof(EventId), typeof(LedgerEntryId)]);
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

    private sealed class WebhookOptions
    {
        public AnyEntityId Target { get; set; }
    }

    /// <summary>
    /// The identifier of an API key, which no other test uses. Its formatting hook writes the prefix and the last
    /// four characters, as a log would show a credential, unless a format asks for the whole text.
    /// </summary>
    [EntityId("key")]
    public readonly partial struct ApiKeyId : IValueObjectStringFormatter<string>
    {
        public static string FormatValue(in string value, ReadOnlySpan<char> format, IFormatProvider? provider)
            => format.IsEmpty && value.Length > 4 ? string.Concat("key_\u2026", value.AsSpan(value.Length - 4)) : value;
    }
}
