using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Marten;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What <c>UseValueObjects()</c> and <c>UseValueObject&lt;TSelf, TValue&gt;()</c> put into a store's options: one member
/// source and one parser ahead of Marten's own, each value object's nullable type in Marten's value types, a column of its
/// underlying type for a duplicated field, and the converter factory in the System.Text.Json serializer, however the
/// serializer was configured before them.
/// </summary>
public sealed class MartenStoreOptionsTests
{
    private static readonly MartenParcel Parcel = new()
    {
        Id = Guid.Parse("0192f4a0-0000-7000-8000-000000000001"),
        Iban = Iban.Create("FR7630006000011234567890189"),
        Page = PageNumber.Create(3),
        Tag = HandWrittenTag<MartenParcelOwner>.Create("abc"),
    };

    [Fact]
    public void One_member_source_and_one_parser_go_ahead_of_Marten_s_own_however_often_it_is_called()
    {
        var options = new StoreOptions();

        options.UseValueObjects().UseValueObjects().UseValueObject<PageNumber, int>();

        options.Linq.MemberSources.OfType<ValueObjectMembers>().Should().ContainSingle()
            .Which.Should().BeSameAs(options.Linq.MemberSources[0]);
        options.Linq.MethodCallParsers.OfType<ValueObjectContainsParser>().Should().ContainSingle()
            .Which.Should().BeSameAs(options.Linq.MethodCallParsers[0]);
    }

    [Fact]
    public void Each_value_object_the_registry_holds_joins_Marten_s_value_types_as_its_nullable_type_alone_once()
    {
        var options = new StoreOptions();

        options.UseValueObjects().UseValueObjects();

        var outerTypes = MartenValueTypes.Of(options).Select(static valueType => valueType.OuterType).ToList();
        outerTypes.Should().ContainSingle(static type => type == typeof(Iban?));
        outerTypes.Should().Contain([typeof(PageNumber?), typeof(CountryCode?), typeof(BirthDate?)]);
        outerTypes.Should().NotContain(static type => !type.IsGenericType, "Marten would accept a compiled query holding a value type of its list, and answer every call with the first call's value");
        outerTypes.Should().NotContain(typeof(Port?), "Marten maps no ushort to a column, and the value object over one is left to it");
    }

    [Fact]
    public void A_store_taught_one_value_object_lists_that_one_alone()
    {
        var options = new StoreOptions();

        options.UseValueObject<PageNumber, int>();

        MartenValueTypes.Of(options).Select(static valueType => valueType.OuterType).Should().Equal(typeof(PageNumber?));
    }

    [Fact]
    public void A_duplicated_field_takes_a_column_of_the_underlying_type_sized_from_the_maximum_length()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObjects();
            options.Schema.For<MartenOrder>()
                .Duplicate(x => x.Iban).Duplicate(x => x.Backup).Duplicate(x => x.Page).Duplicate(x => x.MaybePage)
                .Duplicate(x => x.Quantity).Duplicate(x => x.Born).Duplicate(x => x.Customer).Duplicate(x => x.Consent)
                .Duplicate(x => x.Amount).Duplicate(x => x.Grade).Duplicate(x => x.Code).Duplicate(x => x.Note);
        });

        MartenHarness.Columns(store, "iban", "backup", "page", "maybe_page", "quantity", "born", "customer", "consent", "amount", "grade", "code", "note")
            .Select(static column => string.Join(' ', column.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Should().BeEquivalentTo(
                "iban varchar(34) NULL",
                "backup varchar(34) NULL",
                "page integer NULL",
                "maybe_page integer NULL",
                "quantity smallint NULL",
                "born date NULL",
                "customer uuid NULL",
                "consent boolean NULL",
                "amount decimal NULL",
                "grade text NULL",
                "code varchar(3) NULL",
                "note varchar NULL");
    }

    [Fact]
    public void A_value_object_over_text_with_no_maximum_length_takes_the_column_text_takes()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObject<Declared<MartenUnsizedDeclaration, string>, string>();
            options.Schema.For<MartenUnsizedHolder>().Duplicate(x => x.Text).Duplicate(x => x.Note);
        });

        MartenHarness.Columns(store, "text", "note")
            .Select(static column => string.Join(' ', column.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Should().BeEquivalentTo("text varchar NULL", "note varchar NULL");
    }

    [Fact]
    public void The_serializer_writes_a_value_object_written_by_hand_as_its_value()
    {
        var options = new StoreOptions();

        options.UseValueObjects();

        JsonDocument.Parse(options.Serializer().ToJson(Parcel)).RootElement.GetProperty("Tag").GetString().Should().Be("abc");
        ConverterFactories(options).Should().AllBeEquivalentTo(1);
    }

    [Fact]
    public void A_serializer_built_from_a_source_generated_context_s_options_keeps_its_one_factory()
    {
        var options = new StoreOptions();

        options.UseSystemTextJsonForSerialization(MartenJsonContext.Default.Options);
        options.UseValueObjects();

        ConverterFactories(options).Should().AllBeEquivalentTo(1);
        Written(options).Should().Be("FR7630006000011234567890189 | 3 | abc");
    }

    [Fact]
    public void A_source_generated_resolver_writes_no_value_until_the_factory_is_in_the_options()
    {
        var resolverOnly = new StoreOptions();
        ((SystemTextJsonSerializer)resolverOnly.Serializer()).UseTypeInfoResolver(MartenJsonContext.Default);
        var withTheFactory = new StoreOptions();
        ((SystemTextJsonSerializer)withTheFactory.Serializer()).UseTypeInfoResolver(MartenJsonContext.Default);

        withTheFactory.UseValueObjects();

        JsonDocument.Parse(resolverOnly.Serializer().ToJson(Parcel)).RootElement.GetProperty("Page").ValueKind
            .Should().Be(JsonValueKind.Object, "the context's own options name the factory, a resolver taken alone does not");
        Written(withTheFactory).Should().Be("FR7630006000011234567890189 | 3 | abc");
    }

    [Fact]
    public void A_serializer_replaced_afterwards_loses_the_factory_as_the_remarks_say()
    {
        var options = new StoreOptions();

        options.UseValueObjects();
        options.UseSystemTextJsonForSerialization();

        ConverterFactories(options).Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public void A_serializer_of_the_application_s_own_is_left_alone()
    {
        var options = new StoreOptions();
        var serializer = Substitute.For<ISerializer>();
        options.Serializer(serializer);

        options.UseValueObjects();

        options.Serializer().Should().BeSameAs(serializer);
        options.Linq.MemberSources.OfType<ValueObjectMembers>().Should().ContainSingle();
    }

    [Fact]
    public void AddMarten_applies_UseValueObjects_after_its_own_configuration_whatever_the_order()
    {
        var services = new ServiceCollection();
        services.AddMarten(static options =>
        {
            options.Connection(MartenHarness.Unreachable);

            // Replaced inside AddMarten's configuration, which runs first: the factory still reaches it.
            options.UseSystemTextJsonForSerialization(new JsonSerializerOptions { TypeInfoResolver = MartenJsonContext.Default });
        }).UseValueObjects();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<StoreOptions>();

        options.Linq.MemberSources[0].Should().BeOfType<ValueObjectMembers>();
        Written(options).Should().Be("FR7630006000011234567890189 | 3 | abc");
    }

    /// <summary>
    /// Marten applies what <c>ConfigureMarten</c> registers in the order of registration: a configuration registered after
    /// <c>UseValueObjects()</c> that replaces the serializer drops the factory, as the remarks say, and one registered before
    /// keeps it.
    /// </summary>
    [Fact]
    public void A_ConfigureMarten_registered_after_UseValueObjects_runs_later_and_replacing_the_serializer_there_drops_the_factory()
    {
        static StoreOptions Configured(bool replacedFirst)
        {
            var services = new ServiceCollection();
            if (replacedFirst)
            {
                services.ConfigureMarten(static options => options.UseSystemTextJsonForSerialization(new JsonSerializerOptions { TypeInfoResolver = MartenJsonContext.Default }));
            }

            services.AddMarten(static options => options.Connection(MartenHarness.Unreachable)).UseValueObjects();
            if (!replacedFirst)
            {
                services.ConfigureMarten(static options => options.UseSystemTextJsonForSerialization(new JsonSerializerOptions { TypeInfoResolver = MartenJsonContext.Default }));
            }

            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<StoreOptions>();
        }

        ConverterFactories(Configured(replacedFirst: false)).Should().AllBeEquivalentTo(0);
        var kept = Configured(replacedFirst: true);
        ConverterFactories(kept).Should().AllBeEquivalentTo(1);
        Written(kept).Should().Be("FR7630006000011234567890189 | 3 | abc");
    }

    [Fact]
    public void Each_method_refuses_a_missing_receiver()
    {
        FluentActions.Invoking(static () => ValueObjectStoreOptionsExtensions.UseValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(static () => ValueObjectStoreOptionsExtensions.UseValueObject<PageNumber, int>(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(static () => ValueObjectStoreOptionsExtensions.UseValueObjectId<MartenAccount, CustomerId, Guid>(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(static () => ValueObjectMartenConfigurationExtensions.UseValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("marten");
    }

    /// <summary>Counts the converter factories in each of the four options the store's serializer keeps.</summary>
    private static List<int> ConverterFactories(StoreOptions options)
    {
        List<int> counts = [];
        ((SystemTextJsonSerializer)options.Serializer()).Configure(json => counts.Add(json.Converters.OfType<ValueObjectJsonConverterFactory>().Count()));
        return counts;
    }

    /// <summary>Writes the parcel through the store's serializer, and gives its three value objects as JSON writes them.</summary>
    private static string Written(StoreOptions options)
    {
        var json = JsonDocument.Parse(options.Serializer().ToJson(Parcel)).RootElement;
        return $"{json.GetProperty("Iban").GetString()} | {json.GetProperty("Page").GetInt32()} | {json.GetProperty("Tag").GetString()}";
    }
}

/// <summary>A document holding a value object of each kind the serializer meets: generated, over a number, written by hand.</summary>
public sealed class MartenParcel
{
    public Guid Id { get; set; }

    public Iban Iban { get; set; }

    public PageNumber Page { get; set; }

    public HandWrittenTag<MartenParcelOwner> Tag { get; set; }
}

/// <summary>The owner of the tags a <see cref="MartenParcel"/> holds, which no other test closes over.</summary>
public sealed class MartenParcelOwner;

/// <summary>A document holding a value object over text with no maximum length, beside text.</summary>
public sealed class MartenUnsizedHolder
{
    public Guid Id { get; set; }

    public Declared<MartenUnsizedDeclaration, string> Text { get; set; }

    public string Note { get; set; } = string.Empty;
}

/// <summary>The declaration of a value object over text with no maximum length.</summary>
public sealed class MartenUnsizedDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new();
}

/// <summary>A source-generated context naming the converter factory, as the JSON guide declares one.</summary>
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(MartenParcel))]
internal sealed partial class MartenJsonContext : JsonSerializerContext;
