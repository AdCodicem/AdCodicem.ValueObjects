using System.Net;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.UnitTests.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// <see cref="ParsesAsAttribute"/> and <see cref="NotDefaultAttribute"/> in minimal APIs under <c>AddValidation()</c>, with
/// the reflection-based binding: a refusal is answered with the message of its rule, under the key of the member or the
/// parameter.
/// </summary>
public sealed class DataAnnotationsMinimalApiTests : IAsyncLifetime
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddValidation();

        _application = builder.Build();
        _application.UseRequestLocalization(static options => options
            .SetDefaultCulture("en-US")
            .AddSupportedCultures("fr-FR", "en-US")
            .AddSupportedUICultures("fr-FR", "en-US"));
        _application.MapPost("/annotations/record", static (RawTransfer transfer) => Results.NoContent());
        _application.MapPost("/annotations/class", static (TransferForm form) => Results.NoContent());
        _application.MapGet(
            "/annotations/query",
            static ([ParsesAs(typeof(Iban))] string? iban, [ParsesAs<Quantity, short>] string? quantity, [ParsesAs(typeof(Iban))] string[]? ibans)
                => Results.NoContent());
        _application.MapGet("/annotations/criteria", static ([AsParameters] TextCriteria criteria) => Results.NoContent());
        _application.MapGet("/annotations/search", static ([AsParameters] PropertyTargetedSearch search) => Results.NoContent());
        _application.MapPost("/annotations/typed", static (SettleCommand command) => Results.NoContent());
        _application.MapPost("/annotations/misused", static (MisusedText body) => Results.NoContent());
        await _application.StartAsync(TestContext.Current.CancellationToken);
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    /// <summary>
    /// Each member of a body is refused under its name, a collection under its own name, whatever form names the value
    /// object.
    /// </summary>
    [Fact]
    public async Task A_record_body_is_refused_member_by_member()
    {
        var errors = await ErrorsAsync(HttpMethod.Post, "/annotations/record", """{"iban":"XX","quantity":"5000","country":"ZZ","ibans":["FR7630006000011234567890189","XX"]}""");

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 1000."],
            ["Country"] = ["The value is not one of the accepted values."],
            ["Ibans"] = ["The value must be at least 15 characters long."],
        });
    }

    [Fact]
    public async Task A_class_body_is_refused_and_one_the_rules_accept_is_taken()
    {
        var errors = await ErrorsAsync(HttpMethod.Post, "/annotations/class", """{"account":"FR76","country":"zz"}""");
        using var accepted = await SendAsync(HttpMethod.Post, "/annotations/class", """{"account":"fr76 3000 6000 0112 3456 7890 189","country":"fr"}""");

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Account"] = ["The value must be at least 15 characters long."],
            ["Country"] = ["The value is not one of the accepted values."],
        });
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Query_parameters_are_refused_under_their_names()
    {
        var errors = await ErrorsAsync(HttpMethod.Get, "/annotations/query?iban=XX&quantity=5000&ibans=FR7630006000011234567890189&ibans=XX");

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["iban"] = ["The value must be at least 15 characters long."],
            ["quantity"] = ["The value must be less than or equal to 1000."],
            ["ibans"] = ["The value must be at least 15 characters long."],
        });
    }

    /// <summary>
    /// Parameters gathered with <c>[AsParameters]</c> take the attributes on the properties, of a class or of a record,
    /// where the minimal API's validation reads them.
    /// </summary>
    [Fact]
    public async Task Parameters_gathered_with_AsParameters_are_refused_member_by_member()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/annotations/criteria?account=XX");
        request.Headers.Add("X-Quantity", "5000");
        using var criteria = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var search = await ErrorsAsync(HttpMethod.Get, "/annotations/search?iban=XX&quantity=5000");

        (await Read(criteria)).Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Account"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 1000."],
        });
        search.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 1000."],
        });
    }

    [Fact]
    public async Task A_value_object_left_at_its_default_is_refused_by_NotDefault()
    {
        var errors = await ErrorsAsync(HttpMethod.Post, "/annotations/typed", "{}");

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Account"] = ["The Account field is required."],
            ["Customer"] = ["The Customer field is required."],
        });
    }

    /// <summary>
    /// The minimal API's validation names no resource of its own for a message: an attribute naming a resource type alone
    /// is answered with the message read from it by the code of the rule, in the request's culture.
    /// </summary>
    [Fact]
    public async Task A_resource_named_alone_answers_in_the_language_of_the_request()
    {
        var french = await ErrorsAsync(HttpMethod.Post, "/annotations/record?culture=fr-FR", """{"localized":"5000"}""");
        var english = await ErrorsAsync(HttpMethod.Post, "/annotations/record", """{"localized":"5000"}""");

        french["Localized"].Should().Equal("« Localized » doit être compris entre 0 et 1000.");
        english["Localized"].Should().Equal("The value must be less than or equal to 1000.");
    }

    /// <summary>
    /// .NET 10's validation catches what an attribute throws and answers it as the member's error, the message of the
    /// exception included: the misuse <c>VO0039</c> reports at build time, a member of another type than text, ends in a
    /// 400 rather than a 500, which .NET 11 answers instead.
    /// </summary>
    [Fact]
    public async Task A_misused_attribute_is_answered_with_its_exceptions_message_on_NET_10()
    {
        var errors = await ErrorsAsync(HttpMethod.Post, "/annotations/misused", """{"raw":"XX"}""");

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Raw"] = ["[ParsesAs] holds text to the rules of 'Iban', and 'Raw' is of type 'JsonElement': declare it string, or a collection of string."],
        });
    }

    private async Task<Dictionary<string, string[]>> ErrorsAsync(HttpMethod method, string url, string? json = null)
    {
        using var response = await SendAsync(method, url, json);

        return await Read(response);
    }

    private static async Task<Dictionary<string, string[]>> Read(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);

        return JsonDocument.Parse(text).RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A body whose member holds no text, a misuse of the attribute.</summary>
    public sealed class MisusedText
    {
#pragma warning disable VO0039 // The misuse a host answers in its own way.
        [ParsesAs(typeof(Iban))]
        public object? Raw { get; set; }
#pragma warning restore VO0039
    }
}
