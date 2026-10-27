using System.Globalization;
using System.Text.Json;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.UnitTests.Web;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using Path = HotChocolate.Path;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// The messages of refused values in the application's language: the error filter <c>UseResource&lt;T&gt;()</c> registers
/// reads the application's resource by the rule's code, in the culture of the execution, and quotes the argument, the
/// input field or the variable as <c>{0}</c>.
/// </summary>
/// <remarks>
/// Each test sets the cultures of its own execution and gives them back. The resources are those of <c>Web/</c>, French
/// alone.
/// </remarks>
public sealed class LocalizedGraphQLTests
{
    private const string Valid = "FR7630006000011234567890189";

    /// <summary>The resource, named by its type, as configuration that knows it only at run time names it.</summary>
    private static readonly Type Resource = typeof(ValidationMessages);

    [Fact]
    public async Task Every_refusal_is_answered_in_the_current_culture_quoting_what_it_concerns()
    {
        var executor = await LocalizedAsync<ValidationMessages>();

        using (Culture("fr-FR"))
        {
            (await MessageAsync(executor, """{ account(iban: "XX", qty: 3) { iban } }""")).Should().Be("« iban » : au moins 15 caractères.");
            (await MessageAsync(executor, $$"""{ account(iban: "{{Valid}}", qty: 3, amount: -1) { iban } }"""))
                .Should().Be("« amount » doit valoir au moins 0.");
            (await MessageAsync(executor, """{ country(c: "ZZ") }""")).Should().Be("« c » doit être l'une des valeurs suivantes : FR; BE; LU.");
            (await MessageAsync(executor, $$"""{ many(ibans: ["{{Valid}}", "XX"]) }""")).Should().Be("« ibans » : au moins 15 caractères.");
            (await MessageAsync(executor, $$"""{ transfer(input: { from: "{{Valid}}", qty: 5000 }) { qty } }"""))
                .Should().Be("« qty » doit être compris entre 0 et 1000.");
            (await MessageAsync(executor, $$"""query($q: Quantity!) { account(iban: "{{Valid}}", qty: $q) { iban } }""", """{"q":5000}"""))
                .Should().Be("« q » doit être compris entre 0 et 1000.");
            (await MessageAsync(executor, $$"""query($q: Quantity!) { account(iban: "{{Valid}}", qty: $q) { iban } }""", """{"q":"3"}"""))
                .Should().Be("« q » n'a pas la forme attendue.");
            (await MessageAsync(executor, """query($l: [Iban!]!) { many(ibans: $l) }""", $$"""{"l":["{{Valid}}","XX"]}"""))
                .Should().Be("« l » : au moins 15 caractères.");

            var refusal = (await executor.ExecuteAsync("""{ account(iban: "XX", qty: 3) { iban } }""")).Error();
            refusal.Code().Should().Be(ValueObjectErrorCodes.TooShort, "only the message changes");
        }

        using (Culture("en-US"))
        {
            (await MessageAsync(executor, """{ account(iban: "XX", qty: 3) { iban } }""")).Should().Be("The value must be at least 15 characters long.");
        }
    }

    /// <summary>
    /// HotChocolate reads a variable's default value under a path of its own, <c>["root"]</c>, which names nothing of the
    /// document: the filter quotes the scalar's name instead, and a field of an input object its field, while a variable
    /// named <c>$root</c>, read from its JSON value, keeps its name.
    /// </summary>
    [Fact]
    public async Task A_variables_refused_default_value_is_quoted_by_its_scalars_name()
    {
        var executor = await LocalizedAsync<ValidationMessages>();

        using (Culture("fr-FR"))
        {
            var refusal = (await executor.ExecuteAsync("""query($i: Iban = "XX") { lookup(iban: $i) }""")).Error();
            refusal.Message().Should().Be("« Iban » : au moins 15 caractères.");
            refusal.Member("path").Should().Be("""["root"]""", "HotChocolate names no variable for a default value");
            refusal.Code().Should().Be(ValueObjectErrorCodes.TooShort);
            (await MessageAsync(executor, """query($i: Iban! = "XX") { account(iban: $i, qty: 3) { iban } }"""))
                .Should().Be("« Iban » : au moins 15 caractères.");
            (await MessageAsync(executor, $$"""query($l: [Iban!]! = ["{{Valid}}", "XX"]) { many(ibans: $l) }"""))
                .Should().Be("« Iban » : au moins 15 caractères.");
            (await MessageAsync(executor, $$"""query($t: TransferInput! = { from: "{{Valid}}", qty: 5000 }) { transfer(input: $t) { qty } }"""))
                .Should().Be("« qty » doit être compris entre 0 et 1000.");
            (await MessageAsync(executor, $$"""query($q: Quantity! = 5000) { account(iban: "{{Valid}}", qty: $q) { iban } }"""))
                .Should().Be("« Quantity » doit être compris entre 0 et 1000.");
            (await MessageAsync(executor, """query($root: Iban) { lookup(iban: $root) }""", """{"root":"XX"}"""))
                .Should().Be("« root » : au moins 15 caractères.");
            (await MessageAsync(executor, $$"""query($root: Quantity!) { account(iban: "{{Valid}}", qty: $root) { iban } }""", """{"root":5000}"""))
                .Should().Be("« root » doit être compris entre 0 et 1000.");
            (await MessageAsync(executor, $$"""query($root: Quantity!) { account(iban: "{{Valid}}", qty: $root) { iban } }""", """{"root":"3"}"""))
                .Should().Be("« root » n'a pas la forme attendue.");
        }
    }

    /// <summary>An output refusal is localized too, quoting the field, and keeps HotChocolate's code.</summary>
    [Fact]
    public async Task An_output_refusal_is_answered_in_the_current_culture()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<OutputQuery>().AddValueObjects(static options => options.UseResource<ValidationMessages>()),
            static services => services.AddLogging().AddLocalization());

        using (Culture("fr-FR"))
        {
            var error = (await executor.ExecuteAsync("{ unset }")).Error();
            error.Message().Should().Be("« unset » est obligatoire.");
            error.Code().Should().Be("EXEC_INVALID_LEAF_VALUE");
        }
    }

    /// <summary>
    /// The UI culture chooses the message and the culture formats its arguments.
    /// </summary>
    [Fact]
    public async Task The_UI_culture_chooses_the_message_and_the_culture_formats_its_arguments()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<EchoQuery<Mass>>().AddValueObjects(static options => options.UseResource(Resource)),
            static services => services.AddLogging().AddLocalization());

        using (Culture("en-US", uiCulture: "fr-FR"))
        {
            (await MessageAsync(executor, "{ echo(value: -1) }")).Should().Be("« value » doit être compris entre 9.1E-31 et 2E+32.");
        }

        using (Culture("fr-FR", uiCulture: "en-US"))
        {
            (await MessageAsync(executor, "{ echo(value: -1) }")).Should().Be("The value must be greater than or equal to 9.1E-31.");
        }

        using (Culture("fr-FR"))
        {
            (await MessageAsync(executor, "{ echo(value: -1) }")).Should().Be("« value » doit être compris entre 9,1E-31 et 2E+32.");
        }
    }

    /// <summary>
    /// A code the resource holds no message for keeps the English one; a template quoting an argument its code does not
    /// supply is the application's error to see: HotChocolate answers a field it fails with its unexpected error, and
    /// lets it out of the execution for a variable.
    /// </summary>
    [Fact]
    public async Task A_code_the_resource_lacks_keeps_the_English_message()
    {
        var executor = await LocalizedAsync<SparseMessages>();

        using (Culture("fr-FR"))
        {
            (await MessageAsync(executor, """{ account(iban: "XX", qty: 3) { iban } }""")).Should().Be("The value must be at least 15 characters long.");
            (await MessageAsync(executor, $$"""{ transfer(input: { from: "{{Valid}}", qty: 5000 }) { qty } }""")).Should().Be("« qty » : de 0 à 1000.");
            await FluentActions.Awaiting(() => executor.ExecuteAsync(
                    $$"""query($q: Quantity!) { account(iban: "{{Valid}}", qty: $q) { iban } }""", """{"q":"3"}"""))
                .Should().ThrowAsync<FormatException>("the template of not_parsable quotes an argument no code supplies");
            (await MessageAsync(executor, $$"""{ account(iban: "{{Valid}}", qty: 40000) { iban } }""")).Should().Be("Unexpected Execution Error");
        }
    }

    /// <summary>
    /// The filter runs where the first <c>AddValueObjects</c> call registers it, among the schema's error filters: one of
    /// the application's registered after it sees the localized message, one registered before it the English one.
    /// </summary>
    [Fact]
    public async Task An_applications_filter_registered_after_the_packages_sees_the_localized_message()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder
                .AddQueryType<ShopQuery>()
                .AddErrorFilter(static error => error.SetExtension("before", error.Message))
                .AddValueObjects(static options => options.UseResource<ValidationMessages>())
                .AddErrorFilter(static error => error.SetExtension("after", error.Message)),
            static services => services.AddLogging().AddLocalization());

        using (Culture("fr-FR"))
        {
            var error = (await executor.ExecuteAsync("""{ account(iban: "XX", qty: 3) { iban } }""")).Error();
            error.Member("before").Should().Be("\"The value must be at least 15 characters long.\"");
            error.Member("after").Should().Be("\"« iban » : au moins 15 caractères.\"");
        }
    }

    /// <summary>Without <c>UseResource</c>, the messages stay the rules', whatever the culture.</summary>
    [Fact]
    public async Task Without_a_resource_the_messages_stay_English()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<ShopQuery>().AddValueObjects(),
            static services => services.AddLogging().AddLocalization());

        using (Culture("fr-FR"))
        {
            (await MessageAsync(executor, """{ account(iban: "XX", qty: 3) { iban } }""")).Should().Be("The value must be at least 15 characters long.");
        }
    }

    /// <summary>
    /// A resource named without <c>AddLocalization()</c> fails the executor at start, naming the call, rather than the
    /// first refusal.
    /// </summary>
    [Fact]
    public async Task A_resource_without_localization_fails_the_executor_at_start()
    {
        var start = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<ShopQuery>().AddValueObjects(static options => options.UseResource<ValidationMessages>()));

        (await start.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*services.AddLocalization()*");
    }

    /// <summary>
    /// With no path to read the member from, or under the path HotChocolate reads a literal no member holds, the filter
    /// quotes the scalar's name, and it leaves alone an error whose exception is not a refusal of a scalar of the package.
    /// </summary>
    [Fact]
    public async Task The_filter_quotes_the_scalar_where_nothing_names_the_value_and_leaves_other_errors_alone()
    {
        var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());
        var filter = new ValueObjectErrorFilter(new ValueObjectScalarOptions().UseResource<ValidationMessages>(), executor.Schema, services);
        var refused = GraphQLScalarTests.Scalar(executor, "Iban")
            .Invoking(scalar => scalar.CoerceInputLiteral(new StringValueNode("XX")))
            .Should().Throw<LeafCoercionException>().Which.Errors[0];
        var json = GraphQLScalarTests.Scalar(executor, "Iban")
            .Invoking(scalar => scalar.CoerceInputValue(JsonDocument.Parse("\"XX\"").RootElement, null!))
            .Should().Throw<LeafCoercionException>().Which.Errors[0];
        var other = ErrorBuilder.New().SetMessage("Something else.").SetException(new InvalidOperationException()).Build();
        var bare = ErrorBuilder.New().SetMessage(refused.Message).SetException(refused.Exception).Build();
        var root = Path.Root.Append("root");

        using (Culture("fr-FR"))
        {
            filter.OnError(refused).Message.Should().Be("« Iban » : au moins 15 caractères.");
            bare.Extensions.Should().BeNull();
            filter.OnError(bare).Message.Should().Be("« Iban » : au moins 15 caractères.");
            filter.OnError(refused.WithPath(root)).Message.Should().Be("« Iban » : au moins 15 caractères.");
            filter.OnError(refused.WithPath(root.Append(1))).Message.Should().Be("« Iban » : au moins 15 caractères.");
            filter.OnError(refused.WithPath(Path.Root.Append("list").Append("root"))).Message.Should().Be("« root » : au moins 15 caractères.");
            filter.OnError(refused.WithPath(Path.Root.Append("v"))).Message.Should().Be("« v » : au moins 15 caractères.");
            filter.OnError(json.WithPath(root)).Message.Should().Be("« root » : au moins 15 caractères.");
            filter.OnError(other).Should().BeSameAs(other);
            new ValueObjectErrorFilter(new ValueObjectScalarOptions(), executor.Schema, services).OnError(refused).Should().BeSameAs(refused);
        }
    }

    /// <summary>
    /// A predicate of <c>QuoteScalarNameIn</c> selects the argument or the input field the error's coordinate names, read
    /// from its text or from a coordinate HotChocolate keeps as such, any predicate selecting it being enough; a coordinate
    /// no predicate selects, one the schema does not hold, one naming an output field, and one the filter cannot read
    /// leave the usual rule.
    /// </summary>
    [Fact]
    public async Task A_predicate_quotes_the_scalar_by_the_errors_coordinate_whatever_form_it_takes()
    {
        var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());
        var options = new ValueObjectScalarOptions()
            .UseResource<ValidationMessages>()
            .QuoteScalarNameIn(static _ => false)
            .QuoteScalarNameIn(static inputValue => inputValue.Name == "from");
        var filter = new ValueObjectErrorFilter(options, executor.Schema, services);
        var refused = GraphQLScalarTests.Scalar(executor, "Iban")
            .Invoking(scalar => scalar.CoerceInputLiteral(new StringValueNode("XX")))
            .Should().Throw<LeafCoercionException>().Which.Errors[0]
            .WithPath(Path.Root.Append("v"));

        using (Culture("fr-FR"))
        {
            filter.OnError(refused).Message.Should().Be("« v » : au moins 15 caractères.");
            filter.OnError(refused.SetExtension("coordinate", "TransferInput.from")).Message.Should().Be("« Iban » : au moins 15 caractères.");
            filter.OnError(refused.SetExtension("coordinate", new SchemaCoordinate("TransferInput", "from"))).Message
                .Should().Be("« Iban » : au moins 15 caractères.");
            foreach (var coordinate in (object[])["TransferInput.qty", "Nowhere.field", "ShopQuery.account", "not a coordinate!", 42])
            {
                filter.OnError(refused.SetExtension("coordinate", coordinate)).Message.Should().Be("« v » : au moins 15 caractères.", "{0}", coordinate);
            }

            var bare = ErrorBuilder.New().SetMessage(refused.Message).SetException(refused.Exception).SetPath(Path.Root.Append("v")).Build();
            bare.Extensions.Should().BeNull();
            filter.OnError(bare).Message.Should().Be("« v » : au moins 15 caractères.", "an error with no extension has no coordinate");
        }

        FluentActions.Invoking(static () => new ValueObjectScalarOptions().QuoteScalarNameIn(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("inputValues");
    }

    /// <summary>Sets the cultures of the current execution, and gives the previous ones back when disposed.</summary>
    internal static IDisposable Culture(string culture, string? uiCulture = null) => new CultureScope(culture, uiCulture ?? culture);

    private static Task<IRequestExecutor> LocalizedAsync<TResource>()
        => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<ShopQuery>().AddValueObjects(static options => options.UseResource<TResource>()),
            static services => services.AddLogging().AddLocalization());

    private static async Task<string> MessageAsync(IRequestExecutor executor, string document, string? variables = null)
        => (await executor.ExecuteAsync(document, variables)).Error().Message();

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture, string uiCulture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
