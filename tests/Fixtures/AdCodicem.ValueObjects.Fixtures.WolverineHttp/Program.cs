using System.Globalization;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Wolverine.Http;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.Fixtures.WolverineHttp;

/// <summary>
/// Starts a Wolverine.Http application in the configuration its first argument names, sends it a fixed script of requests
/// over TestHost, and writes each answer as a JSON line into the file its second argument names: <c>lenient</c>,
/// Wolverine's default <c>RejectUnparseableQueryValues = false</c>; <c>strict</c>, the same script with it on; and
/// <c>french</c>, the messages of <see cref="ProblemMessages"/> in the request's language.
/// </summary>
internal static class Program
{
    private static readonly CultureInfo[] Cultures = [CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("fr-FR")];

    private static async Task<int> Main(string[] args)
    {
        if (args is not [var configuration and ("lenient" or "strict" or "french"), var output])
        {
            await Console.Error.WriteLineAsync("Usage: <lenient|strict|french> <output file>");
            return 2;
        }

        var french = configuration == "french";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Host.UseWolverine(static options =>
        {
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
            options.Discovery.DisableConventionalDiscovery();
        });
        builder.Services.AddWolverineHttp();
        builder.Services.AddProblemDetails(static options => options.CustomizeProblemDetails = static context =>
        {
            // The trace identifier changes with each request; the codes the package recorded on the request are what a
            // middleware or a log reads.
            context.ProblemDetails.Extensions.Remove("traceId");
            var recorded = ValueObjectProblemDetails.GetErrorCodes(context.HttpContext);
            RequestTrace.Write($"recorded {string.Join(",", recorded.Select(static code => $"{code.Key}={code.Value}"))}");
        });
        if (french)
        {
            builder.Services.AddLocalization();
        }

        await using var application = builder.Build();
        if (french)
        {
            application.UseRequestLocalization(static options =>
            {
                options.SetDefaultCulture(Cultures[0].Name);
                options.SupportedCultures = Cultures;
                options.SupportedUICultures = Cultures;
            });
        }

        application.MapWolverineEndpoints(options =>
        {
            options.RejectUnparseableQueryValues = configuration == "strict";
            if (french)
            {
                options.UseValueObjects(static messages => messages.UseResource<ProblemMessages>());
            }
            else
            {
                options.UseValueObjects();
            }

            // Middleware applied before the policies run, and a policy registered after UseValueObjects() adding a
            // middleware method whose value object it matches itself, as an application's ConfigureEndpoints may.
            options.AddMiddleware(typeof(FinallyMiddleware), static chain => chain.RoutePattern?.RawText == "/finally/{qty}");
            options.ConfigureEndpoints(static chain =>
            {
                if (chain.RoutePattern?.RawText is "/late" or "/late-only")
                {
                    var before = new MethodCall(typeof(LateMiddleware), nameof(LateMiddleware.Before));
                    chain.Middleware.Add(before);
                    chain.ApplyParameterMatching(before);
                }
            });
        });

        await application.StartAsync();
        using var client = application.GetTestClient();
        await using var writer = new StreamWriter(output, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        foreach (var request in french ? Script.French() : Script.All())
        {
            await writer.WriteLineAsync(await Script.SendAsync(client, request));
        }

        await application.StopAsync();
        return 0;
    }
}

/// <summary>What the endpoints and the problem details write as a request runs, which its answer line carries.</summary>
public static class RequestTrace
{
    private static readonly List<string> Notes = [];

    /// <summary>Notes something that happened while the current request ran.</summary>
    /// <param name="note">The note.</param>
    public static void Write(string note)
    {
        lock (Notes)
        {
            Notes.Add(note);
        }
    }

    /// <summary>Takes the notes of the request that just ran.</summary>
    /// <returns>The notes, in order.</returns>
    public static string[] Take()
    {
        lock (Notes)
        {
            var notes = Notes.ToArray();
            Notes.Clear();
            return notes;
        }
    }
}

/// <summary>The requests of the script, each with a label that is unique in its configuration.</summary>
internal static class Script
{
    private static readonly JsonSerializerOptions Lines = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Every request, sent in the lenient and the strict configurations.</summary>
    public static IEnumerable<(string Label, HttpRequestMessage Request)> All()
    {
        foreach (var url in new[]
        {
            "/orders/3f2504e0-4f89-41d3-9a0c-0305e82c3301", "/orders/not-a-guid",
            "/lines/7", "/lines/007", "/lines/500", "/lines/x",
            "/refs/po-1042", "/refs/ABCDEFGHIJKLMNOP", "/hand/abc", "/hand/a1", "/due/2026-10-17", "/due/17-10-2026",
            "/items?qty=5", "/items?qty=500", "/items?qty=x", "/items?qty=", "/items", "/items?QTY=5", "/items?qty=5&qty=6",
            "/named?q=5", "/named?q=500", "/named?quantity=5", "/quoted?a%22b%5Cc=5", "/quoted?a%22b%5Cc=500",
            "/optional?country=fr", "/optional?country=XX", "/optional?country=", "/optional",
            "/defaulted?qty=5", "/defaulted?qty=500", "/defaulted",
            "/many?qty=1&qty=2", "/many?qty=1&qty=500&qty=x", "/many?qty=", "/many",
            "/list?qty=1&qty=2", "/list?qty=500", "/list", "/sequence?qty=3", "/sequence?qty=0", "/readonly?qty=4", "/readonly?qty=x",
            "/when?due=2026-10-17&at=2026-10-17T10:00:00Z&consent=true&amount=12.5", "/when?due=x&at=x&consent=x&amount=-1",
            "/renamed/5", "/renamed/500",
            "/optional-segment/5", "/optional-segment", "/optional-segment/500",
            "/nullable-segment/5", "/nullable-segment", "/nullable-segment/500",
            "/constrained/5", "/constrained/500", "/constrained/x",
            "/as-parameters?customer=ABC-123&quantity=5&country=fr", "/as-parameters?customer=abc&quantity=500&country=XX",
            "/compound?qty=5", "/compound?qty=500",
            "/after-route/5", "/after-route/500", "/finally/5", "/finally/500",
            "/entity/3f2504e0-4f89-41d3-9a0c-0305e82c3301", "/entity/not-a-guid",
            "/entity?id=3f2504e0-4f89-41d3-9a0c-0305e82c3301", "/entity?id=not-a-guid",
        })
        {
            yield return ($"GET {url}", new HttpRequestMessage(HttpMethod.Get, url));
        }

        yield return Get("/header", ("X-Customer", ["ABC-12345"]));
        yield return Get("/header", ("X-Customer", ["abc"]));
        yield return Get("/header");
        yield return Get("/header", ("X-Customer", ["ABC-12345", "DEF-12345"]));
        yield return Get("/nullable-header");
        yield return Get("/nullable-header", ("X-Country", ["de"]));
        yield return Get("/nullable-header", ("X-Country", ["XX"]));
        yield return Get("/route-or-header/5", ("qty", ["500"]));
        yield return Get("/after-header", ("X-Customer", ["ABC-12345"]));
        yield return Get("/after-header", ("X-Customer", ["abc"]));
        yield return Get("/late?qty=5", ("X-Late", ["ABC-12345"]));
        yield return Get("/late?qty=500", ("X-Late", ["abc"]));
        yield return Get("/late-only", ("X-Late", ["ABC-12345"]));
        yield return Get("/late-only", ("X-Late", ["abc"]));
        yield return Post("/form", "qty=5", Form(("qty", "5")));
        yield return Post("/form", "qty=500", Form(("qty", "500")));
        yield return Post("/form", "nothing", Form());
        yield return Post("/form", "json", new StringContent("{}", Encoding.UTF8, "application/json"));
        yield return Post("/form-payload", "qty=5", Form(("qty", "5")));
        yield return Post("/form-many", "qty=5&qty=6", Form(("qty", "5"), ("qty", "6")));
        yield return Post("/form-many", "qty=5&qty=500", Form(("qty", "5"), ("qty", "500")));
        yield return Post("/form-many", "nothing", Form());
        yield return Post(
            "/orders",
            "quantity 500",
            new StringContent("{\"orderId\":\"3f2504e0-4f89-41d3-9a0c-0305e82c3301\",\"customer\":\"ABC-12345\",\"quantity\":500}", Encoding.UTF8, "application/json"));
    }

    /// <summary>The requests sent in French.</summary>
    public static IEnumerable<(string Label, HttpRequestMessage Request)> French()
    {
        foreach (var url in new[] { "/items?qty=500", "/items", "/optional?country=XX", "/when?due=x&at=x&consent=x&amount=-1", "/lines/7" })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.AcceptLanguage.ParseAdd("fr-FR");
            yield return ($"GET {url}", request);
        }

        var english = new HttpRequestMessage(HttpMethod.Get, "/items?qty=500");
        english.Headers.AcceptLanguage.ParseAdd("en-US");
        yield return ("GET /items?qty=500 in English", english);
    }

    /// <summary>Sends a request, and writes its answer as a JSON line.</summary>
    /// <param name="client">The TestHost client.</param>
    /// <param name="request">The request and its label.</param>
    /// <returns>The line.</returns>
    public static async Task<string> SendAsync(HttpClient client, (string Label, HttpRequestMessage Request) request)
    {
        _ = RequestTrace.Take();
        int? status;
        string body;
        try
        {
            using var response = await client.SendAsync(request.Request);
            status = (int)response.StatusCode;
            body = await response.Content.ReadAsStringAsync();
        }
#pragma warning disable CA1031 // The line records whatever the request ended in, an exception included.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            status = null;
            body = $"{exception.GetType().Name}: {exception.Message.Split('\n')[0]}";
        }
        finally
        {
            request.Request.Dispose();
        }

        return JsonSerializer.Serialize(new Answer(request.Label, status, body, RequestTrace.Take()), Lines);
    }

    private static (string Label, HttpRequestMessage Request) Get(string url, params (string Name, string[] Values)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, values) in headers)
        {
            request.Headers.Add(name, values);
        }

        var label = $"GET {url}{string.Concat(headers.Select(static header => $" [{header.Name}: {string.Join(",", header.Values)}]"))}";
        return (label, request);
    }

    private static (string Label, HttpRequestMessage Request) Post(string url, string content, HttpContent body)
        => ($"POST {url} {content}", new HttpRequestMessage(HttpMethod.Post, url) { Content = body });

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] fields)
        => new(fields.Select(static field => new KeyValuePair<string, string>(field.Name, field.Value)));
}

/// <summary>The answer to one request of the script.</summary>
/// <param name="Request">The request's label.</param>
/// <param name="Status">The status code, or <see langword="null"/> when the request threw.</param>
/// <param name="Body">The body, or the exception the request threw.</param>
/// <param name="Notes">What the endpoints and the problem details wrote while it ran.</param>
internal sealed record Answer(string Request, int? Status, string Body, string[] Notes);
