using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The OpenAPI document of an application whose one endpoint takes every value object of the domain in its body,
/// built in memory, once, through ASP.NET Core's own document generation.
/// </summary>
public sealed class OpenApiDocument : IAsyncLifetime
{
    /// <summary>Gets the schemas of the document's components, keyed by type name.</summary>
    public JsonElement Schemas { get; private set; }

    /// <summary>Gets the schema of one component.</summary>
    /// <param name="name">Name of the component, the type's name.</param>
    /// <returns>Its schema.</returns>
    public JsonElement Schema(string name) => Schemas.GetProperty(name);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi(static options =>
        {
            // A transformer that runs first and describes each value object as the object it is in memory, as a
            // generator blind to its converter would. The value object transformer must replace that shape.
            options.AddSchemaTransformer(static (schema, context, _) =>
            {
                if (ValueObjectRegistry.IsValueObject(context.JsonTypeInfo.Type))
                {
                    schema.Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["value"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    };
                    schema.Required = new HashSet<string> { "value" };
                }

                return Task.CompletedTask;
            });

            options.AddValueObjects();
        });

        await using var application = builder.Build();
        application.MapPost("/everything", static (EveryValueObject body) => Results.Ok(body));
        application.MapOpenApi();
        await application.StartAsync();

        using var client = application.GetTestClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Schemas = document.GetProperty("components").GetProperty("schemas").Clone();

        await application.StopAsync();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// A request body holding one of each value object of the domain, so that the document describes them all.
/// </summary>
public sealed record EveryValueObject(
    Iban Iban,
    EmailAddress Email,
    Amount Amount,
    Percentage Percentage,
    CustomerId Customer,
    CountryCode Country,
    BirthDate Born,
    Quantity Quantity,
    Ordering.OrderReference Order,
    AccountId Account,
    LedgerEntryId LedgerEntry,
    Consent Consent,
    Grade Grade,
    Adjustment Adjustment,
    Score Score,
    Port Port,
    PageNumber Page,
    SequenceNumber Sequence,
    FileSize Size,
    ByteCount Bytes,
    LedgerBalance Balance,
    Fingerprint Fingerprint,
    Latitude Latitude,
    Ratio Ratio,
    OpeningTime Opens,
    RecordedAt Recorded,
    OccurredAt Occurred,
    Duration Took,
    PhoneNumber Phone,
    Label Label,
    DocumentStatus Status,
    Mass Mass,
    TransferLimit Limit,
    Luminance Luminance,
    Priority Priority,
    StorageQuota Quota,
    VatRate VatRate,
    VoteWeight VoteWeight,
    Opacity Opacity,
    TermsAccepted Terms,
    HttpStatus HttpStatus,
    BlockSize BlockSize,
    CutOffDate CutOff,
    ShiftStart Shift,
    LaunchMoment Launch,
    Answer Answer);
