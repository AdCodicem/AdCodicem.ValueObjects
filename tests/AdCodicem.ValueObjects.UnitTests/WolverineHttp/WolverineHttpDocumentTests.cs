using System.Text.Json;
using AdCodicem.ValueObjects.OpenApi;
using AdCodicem.ValueObjects.Swashbuckle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// The OpenAPI documents of Wolverine.Http endpoints whose value objects the package binds: Wolverine describes a query,
/// header or form value only from the variables its own factories created, which the strategy creates and leaves unused,
/// so that ApiExplorer, the built-in document and Swashbuckle's describe each value object with its rules.
/// </summary>
[Collection(nameof(WolverineHttpHosts))]
public sealed class WolverineHttpDocumentTests
{
    private static readonly TimeSpan DocumentTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ApiExplorer_lists_every_value_object_the_package_binds_from_its_source()
    {
        var (host, client) = await StartAsync();
        await using var started = host;
        client.Dispose();

        var descriptions = host.Application.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(static group => group.Items)
            .ToDictionary(static description => description.RelativePath!, static description => description.ParameterDescriptions);

        descriptions["documented/{qty}"].Select(static parameter => (parameter.Name, parameter.Source, parameter.Type, parameter.IsRequired))
            .Should().BeEquivalentTo(
            [
                ("qty", BindingSource.Path, typeof(Quantity), true),
                ("country", BindingSource.Query, typeof(CountryCode?), false),
                ("X-Customer", BindingSource.Header, typeof(CustomerId), false),
                ("quantities", BindingSource.Query, typeof(Quantity[]), false),
            ]);
        descriptions["documented-form"].Should().ContainSingle(static parameter =>
            parameter.Name == "qty" && parameter.Source == BindingSource.Form && parameter.Type == typeof(Quantity));
    }

    [Fact]
    public async Task The_built_in_document_and_Swashbuckle_s_describe_each_value_object_with_its_rules()
    {
        var (host, client) = await StartAsync();
        await using var started = host;
        using var disposable = client;

        foreach (var path in new[] { "/openapi/v1.json", "/swagger/v1/swagger.json" })
        {
            var document = await ReadAsync(client, path);
            var parameters = document.GetProperty("paths").GetProperty("/documented/{qty}").GetProperty("get").GetProperty("parameters")
                .EnumerateArray().ToDictionary(static parameter => parameter.GetProperty("name").GetString()!);

            Reference(parameters["qty"]).Should().Be("#/components/schemas/Quantity", path);
            parameters["qty"].GetProperty("in").GetString().Should().Be("path");
            Reference(parameters["country"]).Should().Be("#/components/schemas/CountryCode", path);
            Reference(parameters["X-Customer"]).Should().Be("#/components/schemas/CustomerId", path);
            parameters["X-Customer"].GetProperty("in").GetString().Should().Be("header");
            parameters["quantities"].GetProperty("schema").GetProperty("items").GetProperty("$ref").GetString()
                .Should().Be("#/components/schemas/Quantity", path);

            var quantity = document.GetProperty("components").GetProperty("schemas").GetProperty("Quantity");
            quantity.GetProperty("minimum").GetInt32().Should().Be(0);
            quantity.GetProperty("maximum").GetInt32().Should().Be(1000);
            document.GetProperty("paths").GetProperty("/documented-form").GetProperty("post").GetProperty("requestBody").GetRawText()
                .Should().Contain("#/components/schemas/Quantity", path);
        }
    }

    private static async Task<(WolverineHttpHost Host, HttpClient Client)> StartAsync()
    {
        var host = WolverineHttpHost.Map(typeof(WolverineDocumentedEndpoints), services: static services =>
        {
            services.AddOpenApi(static options => options.AddValueObjects());
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(static options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Value objects", Version = "v1" });
                options.AddValueObjects();
            });
        });
        host.Application.MapOpenApi();
        host.Application.UseSwagger();

        return (host, await host.StartAsync());
    }

    private static string? Reference(JsonElement parameter) => parameter.GetProperty("schema").GetProperty("$ref").GetString();

    private static async Task<JsonElement> ReadAsync(HttpClient client, string path)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(DocumentTimeout);
        using var document = JsonDocument.Parse(await client.GetStringAsync(path, timeout.Token));

        return document.RootElement.Clone();
    }
}

/// <summary>Endpoints whose value objects the documents describe, from the route, the query, a header and a form.</summary>
public static class WolverineDocumentedEndpoints
{
    [WolverineGet("/documented/{qty}")]
    public static string Get(Quantity qty, CountryCode? country, [FromHeader(Name = "X-Customer")] CustomerId customer, Quantity[] quantities)
        => $"{qty} {country} {customer} {quantities.Length}";

    [WolverinePost("/documented-form")]
    public static string Post([FromForm] Quantity qty) => qty.ToString();
}
