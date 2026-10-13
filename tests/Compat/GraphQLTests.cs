using System.Globalization;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.HotChocolate;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>The fields of the schema, whose arguments and results are value objects.</summary>
public sealed class GraphQLQuery
{
    /// <summary>Echoes a contact.</summary>
    public string Contact(EmailAddress email, Quantity qty, CountryCode? country) => $"{email} x {qty}{(country is { } code ? $" in {code}" : string.Empty)}";

    /// <summary>Echoes accounts.</summary>
    public IReadOnlyList<Iban> Accounts(IReadOnlyList<Iban> ibans) => ibans;

#pragma warning disable VO0010 // The uninitialized instance is what the scalar refuses to write.
    /// <summary>Returns an instance that never went through its rules.</summary>
    public EmailAddress Unset() => default;
#pragma warning restore VO0010
}

/// <summary>
/// One HotChocolate scalar per value object on the next major: the package's HotChocolate.Types floor in a schema built in
/// process, read through the rules, a refusal answered with its code, and its message in the language of the execution
/// from <see cref="Messages"/>.
/// </summary>
public sealed class GraphQLTests
{
    [Fact]
    public async Task A_value_object_is_a_scalar_read_through_its_rules()
    {
        var executor = await BuildAsync();

        var email = executor.Schema.Types.OfType<ValueObjectScalarType<EmailAddress, string>>().Should().ContainSingle().Subject;
        email.Name.Should().Be("EmailAddress");
        email.Pattern.Should().Be(EmailAddress.Schema.Pattern);
        executor.Schema.Types.OfType<ValueObjectScalarType<Quantity, int>>().Should().ContainSingle()
            .Which.SerializationType.Should().Be(ScalarSerializationType.Int);

        (await ExecuteAsync(executor, """{ contact(email: "  Ada@Example.com ", qty: 3, country: "fr") }"""))["data"]!["contact"]!
            .GetValue<string>().Should().Be("ada@example.com x 3 in FR");

        var literal = Error(await ExecuteAsync(executor, """{ contact(email: "nope", qty: 3) }"""));
        literal["extensions"]!["code"]!.GetValue<string>().Should().Be(ValueObjectErrorCodes.InvalidFormat);
        literal["extensions"]!["inputPath"]!.ToJsonString().Should().Be("""["email"]""");

        var variable = Error(await ExecuteAsync(executor, """query($q: Quantity!) { contact(email: "ada@example.com", qty: $q) }""", """{"q":0}"""));
        variable["extensions"]!["code"]!.GetValue<string>().Should().Be(ValueObjectErrorCodes.OutOfRange);
        variable["path"]!.ToJsonString().Should().Be("""["q"]""");

        var output = Error(await ExecuteAsync(executor, "{ unset }"));
        output["extensions"]!["code"]!.GetValue<string>().Should().Be("EXEC_INVALID_LEAF_VALUE");
        output["message"]!.GetValue<string>().Should().Be("The value must not be empty.");
    }

    [Fact]
    public async Task A_refusal_is_answered_in_the_language_of_the_execution()
    {
        var executor = await BuildAsync();
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            Message(await ExecuteAsync(executor, """{ contact(email: "nope", qty: 3) }""")).Should().Be("Le format de « email » n'est pas valide.");
            Message(await ExecuteAsync(executor, """query($q: Quantity!) { contact(email: "ada@example.com", qty: $q) }""", """{"q":0}"""))
                .Should().Be("« q » doit être compris entre 1 et 100.");
            Message(await ExecuteAsync(executor, """{ contact(email: "ada@example.com", qty: 3, country: "ZZ") }"""))
                .Should().Be("« country » doit être l'une des valeurs suivantes : FR; BE; LU.");
            Message(await ExecuteAsync(executor, """{ accounts(ibans: ["XX"]) }""")).Should().Be("« ibans » : au moins 15 caractères.");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    private static async Task<IRequestExecutor> BuildAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddGraphQL().AddQueryType<GraphQLQuery>().AddValueObjects(static options => options.UseResource<Messages>());
        return await services.BuildServiceProvider().GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<JsonObject> ExecuteAsync(IRequestExecutor executor, string document, string? variables = null)
    {
        var request = OperationRequestBuilder.New().SetDocument(document);
        if (variables is not null)
        {
            request.SetVariableValues(variables);
        }

        var result = await executor.ExecuteAsync(request.Build(), TestContext.Current.CancellationToken);
        return JsonNode.Parse(result.ToJson())!.AsObject();
    }

    private static JsonObject Error(JsonObject result) => result["errors"]!.AsArray().Should().ContainSingle().Subject!.AsObject();

    private static string Message(JsonObject result) => Error(result)["message"]!.GetValue<string>();
}
