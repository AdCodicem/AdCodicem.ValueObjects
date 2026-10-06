using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.Swashbuckle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The Swashbuckle filters, compiled against the Swashbuckle and Microsoft.OpenApi versions the current ASP.NET Core
/// takes, in a minimal API on the next one: its components, its parameters and a nullable member, in the OpenAPI 3.0
/// document Swashbuckle writes by default and in an OpenAPI 3.1 one.
/// </summary>
public sealed class SwashbuckleTests : IAsyncLifetime
{
    private JsonElement _v30;
    private JsonElement _v31;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        // The route Swashbuckle serves its documents on takes a regex constraint, which the slim builder leaves out.
        builder.Services.AddRouting();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(static options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Compatibility", Version = "v1" });
            options.AddValueObjects();
        });

        await using var application = builder.Build();
        application.MapGet("/accounts/{iban}", static (Iban iban, Quantity quantity, CountryCode? country) => Results.Ok());
        application.MapPost("/accounts", static (OpenedAccount account) => Results.Ok(account));
        application.UseSwagger();
        application.UseSwagger(static options =>
        {
            options.RouteTemplate = "swagger31/{documentName}/swagger.json";
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        _v30 = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        _v31 = await client.GetFromJsonAsync<JsonElement>("/swagger31/v1/swagger.json", TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Swashbuckle asks for a Microsoft.OpenApi that drops the example of an OpenAPI 3.0 schema; the package raises the
    /// floor to the version it is built against, which an application taking Swashbuckle alone resolves.
    /// </summary>
    [Fact]
    public void The_document_is_built_on_the_Microsoft_OpenApi_floor_the_package_raises()
    {
        var version = typeof(Microsoft.OpenApi.OpenApiDocument).Assembly.GetName().Version!;

        version.Major.Should().Be(2, "Swashbuckle 10 is built on Microsoft.OpenApi 2");
        version.Should().BeGreaterThanOrEqualTo(new Version(2, 12, 2));
    }

    [Fact]
    public void A_value_object_is_documented_as_its_underlying_type_with_its_rules_in_OpenAPI_3_0()
    {
        _v30.GetProperty("openapi").GetString().Should().StartWith("3.0");
        var iban = Schema(_v30, nameof(Iban));
        var quantity = Schema(_v30, nameof(Quantity));

        iban.GetProperty("type").GetString().Should().Be("string");
        iban.TryGetProperty("properties", out _).Should().BeFalse("a value object is documented as its underlying value");
        iban.GetProperty("minLength").GetInt32().Should().Be(15);
        iban.GetProperty("pattern").GetString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        iban.GetProperty("example").GetString().Should().Be("FR7630006000011234567890189");
        quantity.GetProperty("type").GetString().Should().Be("integer");
        quantity.GetProperty("minimum").GetInt32().Should().Be(1);
        quantity.GetProperty("maximum").GetInt32().Should().Be(100);
        quantity.GetProperty("example").GetInt32().Should().Be(3);
        Schema(_v30, nameof(CountryCode)).GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be(nameof(CountryCode));
        Schema(_v30, "PurchaseOrderNoticeChannel").GetProperty("enum").EnumerateArray().Select(static value => value.GetString())
            .Should().Equal("email", "sms");
        Property(_v30, "alternate").GetProperty("nullable").GetBoolean().Should().BeTrue();
        Property(_v30, "perCountry").GetProperty("x-jsonschema-propertyNames").GetProperty("enum").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData("iban", nameof(Iban))]
    [InlineData("quantity", nameof(Quantity))]
    [InlineData("country", nameof(CountryCode))]
    public void A_minimal_API_parameter_refers_to_its_value_object(string name, string component)
        => _v30.GetProperty("paths").GetProperty("/accounts/{iban}").GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name)
            .GetProperty("schema").GetProperty("$ref").GetString().Should().Be($"#/components/schemas/{component}");

    [Fact]
    public void An_OpenAPI_3_1_document_carries_examples_and_a_null_type()
    {
        _v31.GetProperty("openapi").GetString().Should().StartWith("3.1");
        Schema(_v31, nameof(Quantity)).GetProperty("examples")[0].GetInt32().Should().Be(3);
        Property(_v31, "alternate").GetProperty("type").EnumerateArray().Select(static type => type.GetString())
            .Should().BeEquivalentTo("null", "string");
        Property(_v31, "perCountry").GetProperty("propertyNames").GetProperty("enum").GetArrayLength().Should().Be(2);
    }

    private static JsonElement Schema(JsonElement document, string id) => document.GetProperty("components").GetProperty("schemas").GetProperty(id);

    private static JsonElement Property(JsonElement document, string name)
        => Schema(document, nameof(OpenedAccount)).GetProperty("properties").GetProperty(name);
}
