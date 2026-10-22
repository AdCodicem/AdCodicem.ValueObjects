using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// Executors of HotChocolate schemas built in process, as <c>services.AddGraphQL()</c> builds them outside ASP.NET Core,
/// and their results read as JSON.
/// </summary>
internal static class GraphQLHarness
{
    /// <summary>Writes JSON as HotChocolate does, a <c>+</c> left as it is.</summary>
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Builds the executor of a schema the test configures.
    /// </summary>
    /// <param name="configure">Configures the schema.</param>
    /// <param name="services">Registers anything else among the application's services.</param>
    /// <returns>The executor.</returns>
    public static async Task<IRequestExecutor> BuildAsync(Action<IRequestExecutorBuilder> configure, Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        configure(collection.AddGraphQL());

        return await collection.BuildServiceProvider().GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Executes an operation.
    /// </summary>
    /// <param name="executor">The executor.</param>
    /// <param name="document">The operation.</param>
    /// <param name="variables">The JSON object of its variables, or <see langword="null"/> for none.</param>
    /// <returns>The result, as HotChocolate writes it.</returns>
    public static async Task<JsonObject> ExecuteAsync(this IRequestExecutor executor, string document, string? variables = null)
    {
        var request = OperationRequestBuilder.New().SetDocument(document);
        if (variables is not null)
        {
            request.SetVariableValues(variables);
        }

        var result = await executor.ExecuteAsync(request.Build(), TestContext.Current.CancellationToken);
        return JsonNode.Parse(result.ToJson())!.AsObject();
    }

    /// <summary>Gets the data of a result, as compact JSON.</summary>
    /// <param name="result">The result.</param>
    /// <returns>Its data.</returns>
    public static string Data(this JsonObject result)
    {
        result.ContainsKey("errors").Should().BeFalse("the operation succeeds: {0}", result.ToJsonString());
        return result["data"]!.ToJsonString(Compact);
    }

    /// <summary>Gets the one error of a result.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The error.</returns>
    public static JsonObject Error(this JsonObject result)
    {
        result.ContainsKey("errors").Should().BeTrue("the operation fails: {0}", result.ToJsonString());
        return result["errors"]!.AsArray().Should().ContainSingle().Subject!.AsObject();
    }

    /// <summary>Gets the message of an error.</summary>
    /// <param name="error">The error.</param>
    /// <returns>Its message.</returns>
    public static string Message(this JsonObject error) => error["message"]!.GetValue<string>();

    /// <summary>Gets the code of an error, under <c>extensions.code</c>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>Its code, or <see langword="null"/> for none.</returns>
    public static string? Code(this JsonObject error) => error["extensions"]?["code"]?.GetValue<string>();

    /// <summary>Gets a member of an error, as compact JSON.</summary>
    /// <param name="error">The error.</param>
    /// <param name="name">The member, <c>path</c>, or an extension, <c>inputPath</c>.</param>
    /// <returns>The member, or <see langword="null"/> when the error has none.</returns>
    public static string? Member(this JsonObject error, string name)
        => (error[name] ?? error["extensions"]?[name])?.ToJsonString(Compact);
}
