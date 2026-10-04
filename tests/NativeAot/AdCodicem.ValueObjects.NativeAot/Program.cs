// Runs a fixed script over every value object of the domain and writes one line per scenario to the file it is given.
// ci.yml's native AOT job runs it once under the JIT (dotnet run) and once as the native binary, then diffs the two
// files: a difference is a behaviour native AOT changes, and fails the job. The process also fails on its own when a
// scenario throws where nothing should, so that a script both runs get wrong does not pass by agreeing with itself.
using System.Net;
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

    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    await Requests.RunAsync(report, client);
}
finally
{
    await app.StopAsync();
}

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
