using System.Net;
using System.Text;
using AdCodicem.ValueObjects.Wolverine.Http;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// Wolverine.Http endpoints whose value objects the package binds, in Wolverine's static mode: their code is written
/// beforehand by <c>codegen write</c> (the codegen pass of <c>native-aot.sh</c>) and compiled into this application, so
/// that each value object is named in it and parsed with no visitor, no registry and no dynamic code.
/// </summary>
internal static class WolverineHttp
{
    /// <summary>
    /// Builds the application of the endpoints, in static mode, its generated code written to, or found in, the folder
    /// <c>WOLVERINE_CODEGEN_PATH</c> names.
    /// </summary>
    /// <returns>The application, its endpoints mapped.</returns>
    public static WebApplication Build()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

        // The trace identifier differs from one run to the other. Without reflection, AddProblemDetails() is what lets the
        // HTTP JSON options write the validation problem, which UseValueObjects() checks when the endpoints are mapped.
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions.Remove("traceId"));
        builder.Host.UseWolverine(options =>
        {
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
            options.CodeGeneration.GeneratedCodeOutputPath = Environment.GetEnvironmentVariable("WOLVERINE_CODEGEN_PATH")
                ?? Path.Combine(AppContext.BaseDirectory, "wolverine-codegen");
            options.Discovery.DisableConventionalDiscovery();
            options.ApplicationAssembly = typeof(WolverineHttp).Assembly;
        });
        builder.Services.AddWolverineHttp();

        var app = builder.Build();
        app.MapWolverineEndpoints(options =>
        {
            options.CustomizeHttpEndpointDiscovery(query => query.Excludes.WithCondition(
                "not a Wolverine endpoint",
                type => type != typeof(WolverineEndpoints) && type != typeof(WolverineCompoundEndpoint)));
            options.UseValueObjects();
        });

        return app;
    }

    /// <summary>
    /// Sends the endpoints a fixed script of requests, each written with the status and the body answered, and fails the
    /// line of an answer the package would not give: a refused value object that reaches its handler, or an accepted one
    /// answered 400.
    /// </summary>
    /// <param name="report">The lines.</param>
    /// <returns>A task completing once the application stopped.</returns>
    public static async Task RunAsync(Report report)
    {
        await using var app = Build();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
            };

            foreach (var (path, refused) in new[]
            {
                ("/wolverine/quantities/7", false), ("/wolverine/quantities/0", true), ("/wolverine/quantities/abc", true),
                ("/wolverine/documents/po-1042", false), ("/wolverine/documents/PO-1042-0001-X", true),
                ("/wolverine/codes/abc", false), ("/wolverine/codes/a1", true),
                ("/wolverine/unregistered/abc", false),
                ("/wolverine/customers?id=0f8fad5b-d9cb-469f-a165-70867728950e&email=ada@example.com", false),
                ("/wolverine/customers?id=00000000-0000-0000-0000-000000000000&email=ada", true),
                ("/wolverine/customers", true),
                ("/wolverine/rates", false), ("/wolverine/rates?rate=5.5", false), ("/wolverine/rates?rate=7", true),
                ("/wolverine/lines?quantity=1&quantity=2&balance=12", false), ("/wolverine/lines?quantity=1&quantity=0&balance=x", true),
                ("/wolverine/batches?quantity=3", false), ("/wolverine/batches?quantity=1001", true),
                ("/wolverine/compound?quantity=5", false), ("/wolverine/compound?quantity=5000", true),
            })
            {
                await SendAsync(report, client, new HttpRequestMessage(HttpMethod.Get, path), path, refused);
            }

            var id = OrderId.New().Value;
            foreach (var (header, refused) in new (string?, bool)[] { (id, false), ("acc_0", true), (null, true) })
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/wolverine/orders/current");
                if (header is not null)
                {
                    request.Headers.Add("X-Order-Id", header);
                }

                await SendAsync(report, client, request, $"/wolverine/orders/current X-Order-Id: {header ?? "(none)"}", refused);
            }

            foreach (var (quantity, refused) in new[] { ("12", false), ("0", true) })
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/wolverine/forms")
                {
                    Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("quantity", quantity)]),
                };
                await SendAsync(report, client, request, $"/wolverine/forms quantity={quantity}", refused);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task SendAsync(Report report, HttpClient client, HttpRequestMessage request, string subject, bool refused)
    {
        using (request)
        {
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;
            var line = $"{status} {body}";
            if (refused != (status == 400 && body.Contains("\"errorCodes\"", StringComparison.Ordinal)) || (!refused && status != 200))
            {
                report.Fail($"wolverine {request.Method} {subject}", line);
                return;
            }

            report.Line($"wolverine {request.Method} {subject}", line);
        }
    }
}

/// <summary>
/// The Wolverine.Http endpoints of the application, over the linked domain's value objects: a route, query, header and
/// form value of each kind, nullable, in an array and a list, a generic construction, two written by hand, one of which
/// nothing registers, and a compound endpoint whose <c>Before</c> method never runs for a refused request.
/// </summary>
public static class WolverineEndpoints
{
    [WolverineGet("/wolverine/quantities/{quantity}")]
    public static string GetQuantity(Quantity quantity) => $"quantity {quantity}";

    [WolverineGet("/wolverine/documents/{number}")]
    public static string GetDocument(DocumentNumber<PurchaseOrder> number) => $"document {number}";

    [WolverineGet("/wolverine/codes/{code}")]
    public static string GetCode(HandWrittenCode code) => $"code {code}";

    [WolverineGet("/wolverine/unregistered/{code}")]
    public static string GetUnregistered(UnregisteredCode code) => $"unregistered {code}";

    [WolverineGet("/wolverine/customers")]
    public static string GetCustomer(CustomerId id, EmailAddress email) => $"customer {id} {email}";

    [WolverineGet("/wolverine/rates")]
    public static string GetRate(VatRate? rate) => $"rate {(rate is { } value ? value.ToString() : "none")}";

    [WolverineGet("/wolverine/lines")]
    public static string GetLines(Quantity[] quantity, LedgerBalance balance) => $"lines [{string.Join(",", quantity)}] {balance}";

    [WolverineGet("/wolverine/batches")]
    public static string GetBatches(List<Quantity> quantity) => $"batches [{string.Join(",", quantity)}]";

    [WolverineGet("/wolverine/orders/current")]
    public static string GetOrder([FromHeader(Name = "X-Order-Id")] OrderId order) => $"order {order}";

    [WolverinePost("/wolverine/forms")]
    public static string PostForm([FromForm] Quantity quantity) => $"form {quantity}";
}

/// <summary>A compound endpoint, whose <c>Before</c> method takes the value object its handler takes.</summary>
public static class WolverineCompoundEndpoint
{
    /// <summary>Takes the quantity before the handler, and fails a refused one, which the package never lets through.</summary>
    /// <param name="quantity">The quantity.</param>
    public static void Before(Quantity quantity)
    {
        if (((IValueObject<Quantity, short>)quantity).IsDefault)
        {
            throw new InvalidOperationException("Before ran with the default instance of a refused quantity.");
        }
    }

    [WolverineGet("/wolverine/compound")]
    public static string Get(Quantity quantity) => $"compound {quantity}";
}
