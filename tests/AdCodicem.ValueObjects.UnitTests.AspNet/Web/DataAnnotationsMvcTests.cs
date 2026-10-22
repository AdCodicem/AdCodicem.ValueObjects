using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// <see cref="ParsesAsAttribute"/> and <see cref="NotDefaultAttribute"/> in MVC: a refusal is answered with the message of
/// its rule, under the key of the member, and without a code, MVC keeping the message of a result alone.
/// </summary>
public sealed class DataAnnotationsMvcTests : IAsyncLifetime
{
    private const string Account = "FR7630006000011234567890189";

    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddLocalization();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new ControllerPart(typeof(AnnotatedTransfersController)));
            })
            .AddDataAnnotationsLocalization()
            .AddValueObjects();
        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

        _application = builder.Build();
        _application.UseRequestLocalization(static options => options
            .SetDefaultCulture("en-US")
            .AddSupportedCultures("fr-FR", "en-US")
            .AddSupportedUICultures("fr-FR", "en-US"));
        _application.MapControllers();
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
    /// A record takes the attributes on its constructor's parameters; each member is refused under its name, a collection
    /// under its own name rather than an element's, and no code is listed: MVC keeps the message of a result alone.
    /// </summary>
    [Fact]
    public async Task A_record_body_is_refused_member_by_member_without_codes()
    {
        var problem = await SendAsync(HttpMethod.Post, "/annotations/record", """{"iban":"XX","quantity":"5000","country":"ZZ","ibans":["FR7630006000011234567890189","XX"]}""");

        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 1000."],
            ["Country"] = ["The value is not one of the accepted values."],
            ["Ibans"] = ["The value must be at least 15 characters long."],
        });
        problem.Codes.Should().BeEmpty("MVC keeps no code of a validation attribute's result");
    }

    [Fact]
    public async Task A_body_the_rules_accept_is_bound_as_it_was_sent()
    {
        using var response = await SendRawAsync(HttpMethod.Post, "/annotations/record", """{"iban":"fr76 3000 6000 0112 3456 7890 189","quantity":"10","country":"fr"}""");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A class takes them on its properties, and a member's display name is the one quoted by a message that quotes it.
    /// </summary>
    [Fact]
    public async Task A_class_body_is_refused_member_by_member()
    {
        var problem = await SendAsync(HttpMethod.Post, "/annotations/class", """{"account":"FR76","amount":"12,5"}""");

        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Account"] = ["The value must be at least 15 characters long."],
            ["Amount"] = ["The text is not a valid decimal."],
        });
    }

    /// <summary>
    /// A record whose attributes target its properties is refused by MVC as it validates it, which is why the guide puts
    /// them on the constructor's parameters.
    /// </summary>
    [Fact]
    public async Task MVC_refuses_a_record_whose_attributes_target_its_properties()
    {
        var send = () => SendRawAsync(HttpMethod.Post, "/annotations/property-targeted", """{"iban":"XX"}""");

        (await send.Should().ThrowAsync<InvalidOperationException>()).Which.Message
            .Should().Contain("validation metadata must be associated with the constructor parameter");
    }

    [Fact]
    public async Task A_query_parameter_is_refused_under_its_name()
    {
        var problem = await SendAsync(HttpMethod.Get, "/annotations/query?iban=XX");

        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]> { ["iban"] = ["The value must be at least 15 characters long."] });
        problem.Codes.Should().BeEmpty();
    }

    /// <summary>A model bound from the query string, a class or a record, is refused member by member.</summary>
    [Theory]
    [InlineData("/annotations/search")]
    [InlineData("/annotations/search-record")]
    public async Task A_model_bound_from_the_query_string_is_refused_member_by_member(string path)
    {
        var problem = await SendAsync(HttpMethod.Get, path + "?iban=XX&quantity=5000");

        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 1000."],
        });
    }

    /// <summary>
    /// A value object left at its default passes <c>[Required]</c>, which a struct always satisfies, and is refused by
    /// <c>[NotDefault]</c>, with <c>[Required]</c>'s message.
    /// </summary>
    [Fact]
    public async Task A_value_object_left_at_its_default_is_refused_by_NotDefault()
    {
        var problem = await SendAsync(HttpMethod.Post, "/annotations/typed", "{}");

        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Account"] = ["The Account field is required."],
            ["Customer"] = ["The Customer field is required."],
        });
    }

    /// <summary>
    /// Under <c>AddDataAnnotationsLocalization()</c>, MVC leaves the message of an attribute that names a resource type
    /// as the attribute gave it: the message read from the resource by the code of the rule, in the request's culture.
    /// </summary>
    [Fact]
    public async Task A_resource_named_alone_answers_in_the_language_of_the_request()
    {
        var french = await SendAsync(HttpMethod.Post, "/annotations/record?culture=fr-FR", """{"localized":"5000"}""");
        var english = await SendAsync(HttpMethod.Post, "/annotations/record", """{"localized":"5000"}""");

        french.Errors["Localized"].Should().Equal("« Localized » doit être compris entre 0 et 1000.");
        english.Errors["Localized"].Should().Equal("The value must be less than or equal to 1000.");
    }

    private async Task<ReadProblem> SendAsync(HttpMethod method, string url, string? json = null)
    {
        using var response = await SendRawAsync(method, url, json);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);
        var problem = JsonDocument.Parse(text).RootElement;

        return new ReadProblem(
            problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!,
            problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out var codes) ? codes.Deserialize<Dictionary<string, string>>()! : []);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record ReadProblem(Dictionary<string, string[]> Errors, Dictionary<string, string> Codes);

    /// <summary>An application part holding one controller.</summary>
    /// <param name="controller">The controller.</param>
    private sealed class ControllerPart(Type controller) : ApplicationPart, IApplicationPartTypeProvider
    {
        /// <inheritdoc />
        public override string Name => controller.Name;

        /// <inheritdoc />
        public IEnumerable<TypeInfo> Types => [controller.GetTypeInfo()];
    }
}
