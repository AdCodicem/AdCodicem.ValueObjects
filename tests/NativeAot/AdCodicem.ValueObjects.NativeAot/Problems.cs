using System.Globalization;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The minimal API endpoints whose refusals are answered with problem details carrying the rule code, under a
/// <c>/problems</c> group covered by <see cref="ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails{TBuilder}(TBuilder)"/>,
/// and under a <c>/localized</c> group answering with the messages of <see cref="ProblemMessages"/>, and the requests the
/// script sends them. Each handler names its parameters apart from those of <see cref="Endpoints"/> and of the other group:
/// the Request Delegate Generator writes one interceptor for two handlers of the same parameter types and names, and
/// drops the attributes of the second.
/// </summary>
internal static class Problems
{
    public static void Map(WebApplication app)
    {
        var problems = app.MapGroup("/problems").WithValueObjectProblemDetails();

        // A route value, query values required and optional, a header, the members of an [AsParameters] record, and an
        // array from the query string.
        problems.MapGet("/quantities/{count}", (Quantity count) => count.ToString());
        problems.MapGet("/lines", (CustomerId buyer, VatRate? rate) => $"{buyer} {rate}");
        problems.MapGet("/orders/current", ([FromHeader(Name = "X-Order")] OrderId order) => order.Value);
        problems.MapGet("/search", ([AsParameters] ProblemSearch search) => $"{search.Count} {search.Rate} {search.Buyer}");
        problems.MapGet("/batches", (Quantity[] counts) => string.Join(',', counts));

        // A construction of a generic value object and a value object written by hand, both registered by Program.cs, and
        // one written by hand that nothing registered, which native AOT cannot describe: its absence is left unexplained.
        problems.MapGet("/documents/{document}", (DocumentNumber<PurchaseOrder> document) => document.Value);
        problems.MapGet("/codes/{code}", (HandWrittenCode code) => code.Value);
        problems.MapGet("/unregistered", (UnregisteredCode unregistered) => unregistered.Value);

        // A body, which only the exception handler explains, with ThrowOnBadRequest on.
        problems.MapPost("/orders", (Order placed) => placed.Number.Value);

        // The same refusals, answered with the messages of a resource of the application's, read through the localizer.
        var localized = app.MapGroup("/localized").WithValueObjectProblemDetails(static messages => messages.UseResource<ProblemMessages>());
        localized.MapGet("/stock/{units}", (Quantity units) => units.ToString());
        localized.MapGet("/pages", (PageNumber first) => first.ToString());
        localized.MapGet("/scores", (Score mark) => mark.ToString());
        localized.MapGet("/rates", (VatRate? vat) => $"{vat}");
        localized.MapGet("/clients", (CustomerId client) => client.ToString());
        localized.MapGet("/labels/{label}", (HandWrittenCode label) => label.Value);
        localized.MapPost("/orders", (Order submitted) => submitted.Number.Value);
    }

    /// <summary>
    /// Sends the script's requests, and fails each one whose status, or whose codes, are not those expected: a refusal
    /// the package stops explaining in both runs would otherwise pass by agreeing with itself.
    /// </summary>
    /// <param name="report">Where each answer is written.</param>
    /// <param name="client">A client of the application.</param>
    /// <param name="mode"><c>production</c>, with <c>ThrowOnBadRequest</c> off, or <c>development</c>, with it on.</param>
    public static async Task RunAsync(Report report, HttpClient client, string mode)
    {
        var development = mode == "development";
        var customer = "0f8fad5b-d9cb-469f-a165-70867728950e";

        // The answer of the framework, which the package leaves in place: a bare 400 with ThrowOnBadRequest off, and the
        // 500 of the exception handler middleware with it on.
        var framework = development ? "500" : "400";
        (string Path, string Expected)[] gets =
        [
            ("/problems/quantities/7", "200"),
            ("/problems/quantities/0", """400 {"count":"value_object.out_of_range"}"""),
            ("/problems/quantities/abc", """400 {"count":"value_object.not_parsable"}"""),
            ($"/problems/lines?buyer={customer}&rate=5.5", "200"),
            ("/problems/lines?buyer=nope&rate=7", """400 {"buyer":"value_object.not_parsable","rate":"value_object.not_a_known_value"}"""),
            ("/problems/lines", """400 {"buyer":"value_object.required"}"""),
            ($"/problems/lines?buyer={customer}&rate=", "200"),
            ("/problems/batches?counts=1&counts=0&counts=x&counts=", """400 {"counts":"value_object.out_of_range"}"""),
            ("/problems/search?count=0&r=7", """400 {"Count":"value_object.out_of_range","r":"value_object.not_a_known_value","X-Buyer":"value_object.required"}"""),
            ("/problems/documents/PO-1042-0001-X", """400 {"document":"value_object.too_long"}"""),
            ("/problems/codes/123", """400 {"code":"value_object.invalid_format"}"""),

            // Native AOT cannot describe a value object nothing registered, and its refusal keeps the framework's answer.
            ("/problems/unregistered", framework),
        ];
        foreach (var (path, expected) in gets)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (path.StartsWith("/problems/search", StringComparison.Ordinal))
            {
                request.Headers.Add("X-Buyer", Guid.Empty.ToString());
            }

            await ExpectAsync(report, client, request, $"{mode} {path}", expected);
        }

        foreach (var (header, expected) in ((string?, string)[])[
            ("acc_0", """400 {"X-Order":"value_object.id.invalid_length"}"""),
            (null, """400 {"X-Order":"value_object.required"}""")])
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/problems/orders/current");
            if (header is not null)
            {
                request.Headers.Add("X-Order", header);
            }

            await ExpectAsync(report, client, request, $"{mode} /problems/orders/current X-Order: {header ?? "(none)"}", expected);
        }

        // A body is explained only with ThrowOnBadRequest on, and one the serializer cannot read at all never is.
        const string Order = """
            {"Email":"ada@example.com","Quantity":0,"Amount":12.345,"Rate":5.5,"Status":"FINAL","Customer":"0f8fad5b-d9cb-469f-a165-70867728950e","Number":"po-1042","Id":null,"Opens":null}
            """;
        foreach (var (body, expected) in ((string, string)[])[
            (Order, development ? """400 {"$.Quantity":"value_object.out_of_range"}""" : "400"),
            (Order.Replace("\"Quantity\":0", "\"Quantity\":\"many\"", StringComparison.Ordinal), development ? """400 {"$.Quantity":"value_object.not_parsable"}""" : "400"),
            ("{\"Email\":", framework)])
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/problems/orders")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            await ExpectAsync(report, client, request, $"{mode} /problems/orders {body}", expected);
        }

        // The messages of the resource, in the invariant culture the application runs in: the key naming the one bound
        // declared first, then the key of the code, and the English message for a code the resource lacks. Each answer is
        // checked with its messages, since both runs keeping the English ones would agree.
        (string Path, string Expected)[] localized =
        [
            ("/localized/stock/7", "200"),
            ("/localized/stock/0", """400 {"units":"value_object.out_of_range"} {"units":["units lies outside 1..1000."]}"""),
            ("/localized/pages?first=0", """400 {"first":"value_object.out_of_range"} {"first":["first is below 1."]}"""),
            ("/localized/scores?mark=200", """400 {"mark":"value_object.out_of_range"} {"mark":["mark lies outside ..100."]}"""),
            ("/localized/rates?vat=7", """400 {"vat":"value_object.not_a_known_value"} {"vat":["vat is none of 20.0, 5.5."]}"""),
            ("/localized/clients", """400 {"client":"value_object.required"} {"client":["client is missing."]}"""),
            ("/localized/clients?client=" + Guid.Empty, """400 {"client":"value_object.required"} {"client":["client is missing."]}"""),
            ("/localized/scores?mark=x", """400 {"mark":"value_object.not_parsable"} {"mark":["mark cannot be read."]}"""),
            ("/localized/labels/123", """400 {"label":"value_object.invalid_format"} {"label":["A code holds ASCII letters only."]}"""),
        ];
        foreach (var (path, expected) in localized)
        {
            await ExpectAsync(report, client, new HttpRequestMessage(HttpMethod.Get, path), $"{mode} {path}", expected);
        }

        var submitted = new HttpRequestMessage(HttpMethod.Post, "/localized/orders")
        {
            Content = new StringContent(Order, Encoding.UTF8, "application/json"),
        };
        await ExpectAsync(
            report,
            client,
            submitted,
            $"{mode} /localized/orders {Order}",
            development ? """400 {"$.Quantity":"value_object.out_of_range"} {"$.Quantity":["Quantity lies outside 1..1000."]}""" : "400");
    }

    /// <summary>
    /// Maps the endpoints in an application that registers no <c>AddProblemDetails()</c>: its HTTP JSON options cannot
    /// serialize problem details without reflection, as in a native binary, so the convention fails the build of the
    /// endpoints, naming the call, where each refusal would otherwise be answered with a 500.
    /// </summary>
    /// <param name="report">Where the outcome is written.</param>
    public static async Task RunWithoutProblemDetailsAsync(Report report)
    {
        const string Subject = "problems without AddProblemDetails()";
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
        await using var application = builder.Build();
        Map(application);

        try
        {
            var endpoints = ((IEndpointRouteBuilder)application).DataSources.SelectMany(static source => source.Endpoints).Count();
            report.Fail(Subject, $"{endpoints} endpoints built");
        }
        catch (InvalidOperationException exception)
        {
            report.Line(Subject, $"{exception.GetType().Name}, naming AddProblemDetails(): {exception.Message.Contains("AddProblemDetails()", StringComparison.Ordinal)}");
        }
    }

    private static async Task ExpectAsync(Report report, HttpClient client, HttpRequestMessage request, string subject, string expected)
    {
        using (request)
        {
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;
            report.Line($"{request.Method} {subject}", $"{status} {body}");

            var outcome = Outcome(request.RequestUri!.AbsolutePath, status, body);
            if (outcome != expected)
            {
                report.Fail($"{request.Method} {subject}", $"expected {expected}, answered {outcome}");
            }
        }
    }

    /// <summary>
    /// The status, followed by the codes of the problem details when they carry any, and by their messages when they
    /// answer an endpoint of the <c>/localized</c> group.
    /// </summary>
    private static string Outcome(string path, int status, string body)
    {
        var text = status.ToString(CultureInfo.InvariantCulture);
        if (!body.StartsWith('{'))
        {
            return text;
        }

        using var problem = JsonDocument.Parse(body);
        if (!problem.RootElement.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out var codes))
        {
            return text;
        }

        return path.StartsWith("/localized/", StringComparison.Ordinal)
            ? $"{text} {codes.GetRawText()} {problem.RootElement.GetProperty("errors").GetRawText()}"
            : $"{text} {codes.GetRawText()}";
    }
}

/// <summary>A search, bound through <c>[AsParameters]</c>.</summary>
/// <param name="Count">How many, from the query string under the name of the member.</param>
/// <param name="Rate">The rate, optional, under the query name <c>r</c>.</param>
/// <param name="Buyer">The buyer, from a header.</param>
internal sealed record ProblemSearch(Quantity Count, [FromQuery(Name = "r")] VatRate? Rate, [FromHeader(Name = "X-Buyer")] CustomerId Buyer);
