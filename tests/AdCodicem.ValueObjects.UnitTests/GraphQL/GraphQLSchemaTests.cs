using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using AdCodicem.ValueObjects.UnitTests.Web;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// What the schema says of a value object: a scalar of its own, its name, its description, its kind, its pattern and
/// its specification, through <c>AddValueObjects()</c> and through <c>AddValueObjectScalar</c>.
/// </summary>
public sealed class GraphQLSchemaTests
{
    private const string IbanPattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$";

    /// <summary>
    /// Without the package, HotChocolate infers an input object type from a value object it meets as an argument, finds no
    /// constructor a field can fill, and the schema does not start: the package is what makes such a schema work.
    /// </summary>
    [Fact]
    public async Task Without_the_package_a_schema_taking_a_value_object_does_not_start()
    {
        var start = () => GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>());

        (await start.Should().ThrowAsync<Exception>()).Which.ToString().Should().Contain("No compatible constructor found for input type");
    }

    /// <summary>
    /// Each value object the schema meets, as an argument, a field, an element of a list or a field of an input object,
    /// is a scalar of its own, named by the schema's conventions as HotChocolate names the type it would otherwise
    /// infer, a construction of a generic one included, or by HotChocolate's attribute, and described by its schema.
    /// </summary>
    [Fact]
    public async Task Each_value_object_is_a_scalar_named_and_described_by_the_schema()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<ShopQuery>()
            .AddType<ObjectType<EchoQuery<Reference<GraphQLSchemaTests>>>>()
            .AddType<ObjectType<EchoQuery<NamedParcelCode>>>()
            .AddValueObjects());

        var sdl = Sdl(executor);
        sdl.Should().Contain("account(iban: Iban!, qty: Quantity!, amount: Amount, country: CountryCode): Account!")
            .And.Contain("transfer(input: TransferInput!): Transfer!")
            .And.Contain("input TransferInput { from: Iban! qty: Quantity! }")
            .And.Contain("type Account { iban: Iban! qty: Quantity! amount: Amount country: CountryCode id: CustomerId! }")
            .And.Contain($"\"{Iban.Schema.Description}\" scalar Iban")
            .And.Contain("scalar ReferenceOfGraphQLSchemaTests")
            .And.Contain("echo(value: ParcelCode!): ParcelCode!");

        GraphQLScalarTests.Scalar(executor, "Iban").Should().BeOfType<ValueObjectScalarType<Iban, string>>();
        GraphQLScalarTests.Scalar(executor, "ReferenceOfGraphQLSchemaTests")
            .Should().BeOfType<ValueObjectScalarType<Reference<GraphQLSchemaTests>, string>>()
            .Which.Description.Should().Be(Reference<GraphQLSchemaTests>.Schema.Description);
        GraphQLScalarTests.Scalar(executor, "ParcelCode").Should().BeOfType<ValueObjectScalarType<NamedParcelCode, string>>();
        GraphQLScalarTests.Scalar(executor, "ReferenceOfGraphQLSchemaTests").RuntimeType.Should().Be<Reference<GraphQLSchemaTests>>();
    }

    /// <summary>An application's own naming conventions name the scalars.</summary>
    [Fact]
    public async Task An_applications_naming_conventions_name_the_scalars()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<EchoQuery<Iban>>()
            .AddConvention<INamingConventions>(new SuffixedNaming())
            .AddValueObjects());

        Sdl(executor).Should().Contain("echo(value: IbanScalar!): IbanScalar!");
    }

    /// <summary>
    /// Two value objects of one name fail the schema at start, with HotChocolate's own message: the options name one of
    /// them from the composition root.
    /// </summary>
    [Fact]
    public async Task Two_value_objects_of_one_name_are_told_apart_by_the_options()
    {
        var collision = () => GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<CodeQuery>().AddValueObjects());
        (await collision.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("The name `Code` was already registered by another type.");

        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<CodeQuery>()
            .AddValueObjects(static options => options.Name<Shipping.Code>("ShippingCode")));

        Sdl(executor).Should().Contain("billingCode(code: Code!): Code!").And.Contain("shippingCode(code: ShippingCode!): ShippingCode!");
        (await executor.ExecuteAsync("{ shippingCode(code: 0) }")).Error().Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A scalar <c>AddValueObjectScalar</c> adds wins for its type over the one the handler would give it: its name wins
    /// over the options', and the options name one added without a name.
    /// </summary>
    [Fact]
    public async Task A_scalar_added_for_its_type_wins_and_takes_the_options_name_when_it_has_none()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<CodeQuery>()
            .AddValueObjects(static options => options.Name<Billing.Code>("FromOptions").Name<Shipping.Code>("ShippingFromOptions"))
            .AddValueObjectScalar<Billing.Code, string>("Explicit")
            .AddValueObjectScalar<Shipping.Code, int>());

        Sdl(executor).Should().Contain("billingCode(code: Explicit!): Explicit!")
            .And.Contain("shippingCode(code: ShippingFromOptions!): ShippingFromOptions!")
            .And.NotContain("scalar FromOptions");
        (await executor.ExecuteAsync("""{ billingCode(code: "X") }""")).Error().Code().Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// <c>AddValueObjectScalar</c> alone maps its type, named by the conventions and described by its schema, without
    /// asking the registry: a construction of a generic value object, and one written by hand that nothing registered.
    /// </summary>
    [Fact]
    public async Task A_scalar_added_alone_maps_its_type_without_the_registry()
    {
        var services = new ServiceCollection();
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder
                .AddQueryType<EchoQuery<Reference<SalesInvoice>>>()
                .AddType<ObjectType<EchoQuery<Declared<GraphQLTally, int>>>>()
                .AddValueObjectScalar<Reference<SalesInvoice>, string>()
                .AddValueObjectScalar<Declared<GraphQLTally, int>, int>("Tally"),
            collection => services = (ServiceCollection)collection);

        services.Should().NotContain(service => service.ServiceType == typeof(ValueObjectScalarRegistration));
        GraphQLScalarTests.Scalar(executor, "ReferenceOfSalesInvoice").Description.Should().Be(Reference<SalesInvoice>.Schema.Description);
        GraphQLScalarTests.Scalar(executor, "Tally").Description.Should().Be("A tally.");
        (await executor.ExecuteAsync("""{ echo(value: " po-1 ") }""")).Data().Should().Be("""{"echo":"PO-1"}""");
        (await executor.ExecuteAsync("""{ echo(value: "PO-1042-AND-MORE") }""")).Error().Code().Should().Be(ValueObjectErrorCodes.TooLong);
    }

    /// <summary>
    /// A scalar is serialized as a Boolean, an Int, a Float or a String by its underlying type, and only a String carries
    /// a pattern: the one the type declares, or the one the wire form of a duration, a time of day or a
    /// <see cref="DateTime"/> follows.
    /// </summary>
    [Fact]
    public async Task A_scalar_is_serialized_by_its_underlying_type_and_a_String_carries_its_pattern()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<EchoQuery<Consent>>()
            .AddType<ObjectType<EchoQuery<Quantity>>>()
            .AddType<ObjectType<EchoQuery<Latitude>>>()
            .AddType<ObjectType<EchoQuery<LedgerBalance>>>()
            .AddType<ObjectType<EchoQuery<Iban>>>()
            .AddType<ObjectType<EchoQuery<Duration>>>()
            .AddType<ObjectType<EchoQuery<OpeningTime>>>()
            .AddType<ObjectType<EchoQuery<RecordedAt>>>()
            .AddType<ObjectType<EchoQuery<OccurredAt>>>()
            .AddValueObjects());

        Shape(executor, "Consent").Should().Be((ScalarSerializationType.Boolean, null));
        Shape(executor, "Quantity").Should().Be((ScalarSerializationType.Int, null));
        Shape(executor, "Latitude").Should().Be((ScalarSerializationType.Float, null));
        Shape(executor, "LedgerBalance").Should().Be((ScalarSerializationType.String, null));
        Shape(executor, "OccurredAt").Should().Be((ScalarSerializationType.String, null));
        Shape(executor, "Iban").Should().Be((ScalarSerializationType.String, IbanPattern));
        Shape(executor, "Duration").Should().Be((ScalarSerializationType.String, @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$"));
        Shape(executor, "OpeningTime").Should().Be((ScalarSerializationType.String, @"^(?:[01]\d|2[0-3]):[0-5]\d(?::[0-5]\d(?:\.\d{1,7})?)?$"));
        Shape(executor, "RecordedAt").Should().Be((
            ScalarSerializationType.String,
            @"^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])(?:T(?:[01]\d|2[0-3]):[0-5]\d(?::[0-5]\d(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})?)?$"));
        Sdl(executor).Should().NotContain("@serializeAs");
    }

    /// <summary>
    /// HotChocolate publishes the kind and the pattern as <c>@serializeAs</c> when the application turns on
    /// <c>ApplySerializeAsToScalars</c>, and the specification the options give as <c>@specifiedBy</c>, none by default.
    /// </summary>
    [Fact]
    public async Task The_kind_the_pattern_and_the_specification_are_published_as_directives()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<EchoQuery<Iban>>()
            .AddType<ObjectType<EchoQuery<Quantity>>>()
            .ModifyOptions(static options => options.ApplySerializeAsToScalars = true)
            .AddValueObjects(static options => options.SpecifiedBy<Iban>("https://www.iso.org/standard/81090.html")));

        Sdl(executor).Should()
            .Contain($"scalar Iban @serializeAs(type: STRING, pattern: \"{IbanPattern}\") @specifiedBy(url: \"https://www.iso.org/standard/81090.html\")")
            .And.Contain("scalar Quantity @serializeAs(type: INT)");
        GraphQLScalarTests.Scalar(executor, "Quantity").SpecifiedBy.Should().BeNull();
    }

    /// <summary>
    /// Every call on one schema configures the same options, in the order of the calls, and registers one handler and one
    /// error filter; each schema of an application keeps options of its own.
    /// </summary>
    [Fact]
    public async Task Every_call_on_a_schema_configures_one_set_of_options_and_each_schema_its_own()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton("other", new object());
        services.AddGraphQL()
            .AddQueryType<CodeQuery>()
            .AddValueObjects(static options => options.Name<Shipping.Code>("Parcel").SpecifiedBy<Billing.Code>("https://example.com/draft"))
            .AddValueObjects(static options => options.Name<Shipping.Code>("ShippingCode"))
            .AddValueObjects(static options => options.SpecifiedBy<Billing.Code>("https://example.com/billing"))
            .AddValueObjects();
        services.AddGraphQL("a").AddQueryType<CodeQuery>().AddValueObjects(static options => options.Name<Shipping.Code>("A"));
        services.AddGraphQL("b").AddQueryType<CodeQuery>().AddValueObjects(static options => options.Name<Shipping.Code>("B"));

        services.Where(service => service.ServiceType == typeof(ValueObjectScalarRegistration))
            .Select(service => ((ValueObjectScalarRegistration)service.ImplementationInstance!).SchemaName)
            .Should().HaveCount(3).And.OnlyHaveUniqueItems();

        var provider = services.BuildServiceProvider();
        var cancellation = TestContext.Current.CancellationToken;
        Sdl(await provider.GetRequestExecutorAsync(cancellationToken: cancellation)).Should()
            .Contain("shippingCode(code: ShippingCode!): ShippingCode!")
            .And.Contain("scalar Code @specifiedBy(url: \"https://example.com/billing\")")
            .And.NotContain("Parcel", "a later call names the type again")
            .And.NotContain("draft", "a later call gives the type another specification");
        Sdl(await provider.GetRequestExecutorAsync("a", cancellation)).Should().Contain("shippingCode(code: A!): A!");
        Sdl(await provider.GetRequestExecutorAsync("b", cancellation)).Should().Contain("shippingCode(code: B!): B!");
    }

    /// <summary>
    /// HotChocolate builds the schema again when its executor is evicted, and a type system object belongs to the schema
    /// that completes it: the scalars <c>AddValueObjects</c> and <c>AddValueObjectScalar</c> give are created afresh, so
    /// that the new executor reads values through the rules, in the application's language, as the first did. A scalar
    /// shared between the two schemas would fail the second, which HotChocolate reports nowhere, keeping the first
    /// executor for good.
    /// </summary>
    [Fact]
    public async Task An_evicted_executor_is_built_again_with_scalars_of_its_own()
    {
        var services = new ServiceCollection().AddLogging().AddLocalization();
        services.AddGraphQL()
            .AddQueryType<RatingQuery>()
            .AddValueObjects(static options => options.UseResource<ValidationMessages>())
            .AddValueObjectScalar<HandWrittenRating, int>();
        var provider = services.BuildServiceProvider();
        var cancellation = TestContext.Current.CancellationToken;
        var first = await provider.GetRequestExecutorAsync(cancellationToken: cancellation);

        provider.GetRequiredService<IRequestExecutorManager>().EvictExecutor();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var second = first;
        while (ReferenceEquals(second, first))
        {
            await Task.Delay(20, deadline.Token);
            second = await provider.GetRequestExecutorAsync(cancellationToken: cancellation);
        }

        (await second.ExecuteAsync("""{ account(iban: "fr76 3000 6000 0112 3456 7890 189") rate(rating: 4) }""")).Data()
            .Should().Be("""{"account":"FR7630006000011234567890189","rate":4}""");
        using (LocalizedGraphQLTests.Culture("fr-FR"))
        {
            var refused = (await second.ExecuteAsync("""{ account(iban: "XX") }""")).Error();
            refused.Message().Should().Be("« iban » : au moins 15 caractères.");
            refused.Code().Should().Be(ValueObjectErrorCodes.TooShort);
            var rating = (await second.ExecuteAsync("{ rate(rating: 9) }")).Error();
            rating.Message().Should().Be("« rating » n'a pas la forme attendue.");
            rating.Code().Should().Be(ValueObjectErrorCodes.NotParsable);
        }
    }

    /// <summary>
    /// HotChocolate asks a discovery handler the kind of a type it has not registered yet, to predict it: a value object
    /// is a scalar, any other type the handler leaves to HotChocolate's own. No schema of the suite asks before the
    /// type is registered, so the handler is asked directly.
    /// </summary>
    [Fact]
    public void The_handler_predicts_a_scalar_for_a_value_object_alone()
    {
        var inspector = new DefaultTypeInspector();
        var handler = new ValueObjectTypeDiscoveryHandler();
        var iban = inspector.GetTypeRef(typeof(Iban), TypeContext.Input);
        var point = inspector.GetTypeRef(typeof(Point), TypeContext.Output);

        handler.TryInferKind(iban, new TypeDiscoveryInfo(iban), out var kind).Should().BeTrue();
        kind.Should().Be(TypeKind.Scalar);
        handler.TryInferKind(point, new TypeDiscoveryInfo(point), out kind).Should().BeFalse();
        handler.TryInferType(point, new TypeDiscoveryInfo(point), out var types).Should().BeFalse();
        types.Should().BeNull();
    }

    [Fact]
    public void The_registrations_check_their_arguments()
    {
        IRequestExecutorBuilder none = null!;
        var builder = new ServiceCollection().AddGraphQL();
        var options = new ValueObjectScalarOptions();

        FluentActions.Invoking(() => none.AddValueObjects()).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => none.AddValueObjectScalar<Iban, string>()).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        builder.Invoking(static b => b.AddValueObjectScalar<Iban, string>(" ")).Should().Throw<ArgumentException>().WithParameterName("name");
        options.Invoking(static o => o.Name<Iban>(" ")).Should().Throw<ArgumentException>().WithParameterName("name");
        options.Invoking(static o => o.Name<Iban>(null!)).Should().Throw<ArgumentNullException>().WithParameterName("name");
        options.Invoking(static o => o.SpecifiedBy<Iban>(" ")).Should().Throw<ArgumentException>().WithParameterName("url");
        options.Invoking(static o => o.SpecifiedBy<Iban>("standards/iban")).Should().Throw<ArgumentException>()
            .WithParameterName("url").WithMessage("'standards/iban' is not an absolute URL.*");
        options.Invoking(static o => o.UseResource(null!)).Should().Throw<ArgumentNullException>().WithParameterName("resourceType");

        options.ResourceType.Should().BeNull();
        options.UseResource<GraphQLSchemaTests>().Should().BeSameAs(options);
        options.ResourceType.Should().Be<GraphQLSchemaTests>();
        var named = typeof(string);
        options.UseResource(named).ResourceType.Should().Be<string>();
    }

    private static (ScalarSerializationType Kind, string? Pattern) Shape(IRequestExecutor executor, string name)
    {
        var scalar = GraphQLScalarTests.Scalar(executor, name);
        return (scalar.SerializationType, scalar.Pattern);
    }

    /// <summary>Gets the schema's SDL on one line, each run of white space a single space.</summary>
    private static string Sdl(IRequestExecutor executor) => Regex.Replace(executor.Schema.ToString(), @"\s+", " ");

    /// <summary>Naming conventions of an application's own, which suffix the name of every scalar.</summary>
    private sealed class SuffixedNaming : DefaultNamingConventions
    {
        public override string GetTypeName(Type type, TypeKind kind)
            => kind == TypeKind.Scalar ? base.GetTypeName(type, kind) + "Scalar" : base.GetTypeName(type, kind);
    }
}
