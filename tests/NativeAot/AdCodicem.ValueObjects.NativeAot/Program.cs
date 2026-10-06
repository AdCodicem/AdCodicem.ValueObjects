// Runs a fixed script over every value object of the domain and writes one line per scenario to the file it is given.
// ci.yml's native AOT job runs it once under the JIT (dotnet run) and once as the native binary, then diffs the two
// files: a difference is a behaviour native AOT changes, and fails the job. The process also fails on its own when a
// scenario throws where nothing should, so that a script both runs get wrong does not pass by agreeing with itself.
using System.Net;
using AdCodicem.ValueObjects.AspNetCore.Http;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.NativeAot;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

if (args.Length != 1)
{
    await Console.Error.WriteLineAsync("Usage: AdCodicem.ValueObjects.NativeAot <output file>");
    return 2;
}

var report = new Report();

// A generic value object registers its definition alone. Native AOT cannot describe a construction by reflection,
// so the application registers the ones it uses, the schema read off the type and the converter named statically.
report.Line("registry", $"DocumentNumber<PurchaseOrder> registered before the application did: {ValueObjectRegistry.TryGet(typeof(DocumentNumber<PurchaseOrder>), out _)}");
ValueObjectRegistry.Register<DocumentNumber<PurchaseOrder>, string>(static () => new DocumentNumber<PurchaseOrder>.ValueJsonConverter());

// A value object written by hand registers nothing, and native AOT cannot describe one by reflection either, so the
// application registers the two it uses. It names no converter, since none was generated: the JSON factory closes its
// general-purpose converter over each one through the type arguments the descriptor hands back.
ValueObjectRegistry.Register<HandWrittenCode, string>(HandWrittenCode.Schema);
ValueObjectRegistry.Register<HandWrittenLink, Uri>(HandWrittenLink.Schema);

// Identifiers minted from a fixed clock and a counter rather than the system's, so that both runs mint the same ones.
ValueObjectIds.Configure(new FixedTimeProvider(), new CountingEntropySource());

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

await using var app = builder.Build();
Endpoints.Map(app);
await app.StartAsync();

try
{
    Scenarios.Run(report);

    using var client = ClientOf(app);
    await Requests.RunAsync(report, client);
}
finally
{
    await app.StopAsync();
}

// The problem details carrying the rule code, in an application of their own in each setting of ThrowOnBadRequest: off,
// as in Production, where the endpoint filter answers; on, as in Development, where the exception handler does. Each
// answer is checked against the status and the codes the script expects, since both runs explaining nothing would agree.
foreach (var (mode, throwOnBadRequest) in new[] { ("production", false), ("development", true) })
{
    var problemsBuilder = WebApplication.CreateSlimBuilder();
    problemsBuilder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
    problemsBuilder.Logging.ClearProviders();
    problemsBuilder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
    problemsBuilder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = throwOnBadRequest);

    // The trace identifier differs from one run to the other.
    problemsBuilder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions.Remove("traceId"));
    problemsBuilder.Services.AddValueObjectHttpProblemDetails();

    await using var problems = problemsBuilder.Build();
    problems.UseExceptionHandler();
    Problems.Map(problems);
    await problems.StartAsync();
    try
    {
        using var client = ClientOf(problems);
        await Problems.RunAsync(report, client, mode);
    }
    finally
    {
        await problems.StopAsync();
    }
}

// Without AddProblemDetails(), the HTTP JSON options of an application without reflection cannot write problem details,
// and the convention fails the build of the endpoints rather than answer each refusal with a 500.
await Problems.RunWithoutProblemDetailsAsync(report);

await File.WriteAllLinesAsync(args[0], report.Lines);
if (report.Unexpected.Count == 0)
{
    Console.WriteLine($"{report.Lines.Count} lines written to {args[0]}.");
    return 0;
}

foreach (var line in report.Unexpected)
{
    await Console.Error.WriteLineAsync($"Unexpected: {line}");
}

return 1;

static HttpClient ClientOf(WebApplication app)
    => new() { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
