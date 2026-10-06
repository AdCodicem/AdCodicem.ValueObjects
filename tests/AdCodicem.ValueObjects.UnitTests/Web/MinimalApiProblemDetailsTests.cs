using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Http;
using AdCodicem.ValueObjects.AspNetCore.Http.Binding;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// A value object a minimal API refuses, answered with the problem details MVC writes, carrying the code of the rule it
/// broke: through the endpoint filter in Production, and through the exception handler in Development, where the binder
/// throws instead of answering. Each theory runs in both.
/// </summary>
public sealed class MinimalApiProblemDetailsTests(
    ProductionProblemDetailsApplication production,
    DevelopmentProblemDetailsApplication development)
    : IClassFixture<ProductionProblemDetailsApplication>, IClassFixture<DevelopmentProblemDetailsApplication>
{
    /// <summary>The two settings of <c>RouteHandlerOptions.ThrowOnBadRequest</c>.</summary>
    public static TheoryData<bool> Modes => [false, true];

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_refused_route_value_is_answered_with_the_validation_problem_MVC_writes(bool throwOnBadRequest)
    {
        using var response = await GetAsync(throwOnBadRequest, "/api/accounts/XX!!");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var (message, code) = Refusal<Iban, string>("XX!!");
        code.Should().Be(ValueObjectErrorCodes.TooShort);
        JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement).Should().Be(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ["title"] = "One or more validation errors occurred.",
            ["status"] = 400,
            ["errors"] = new Dictionary<string, string[]> { ["iban"] = [message] },
            [ValueObjectProblemDetails.ExtensionName] = new Dictionary<string, string> { ["iban"] = code },
        }));
        body.Should().NotContain("XX!!", "a response never carries the refused text");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Every_refused_value_object_of_a_request_is_listed_with_the_rule_it_broke(bool throwOnBadRequest)
    {
        var problem = await GetProblemAsync(throwOnBadRequest, "/api/lines?quantity=5000&country=ZZ");

        problem.Should().Be(Problem(("quantity", Refusal<Quantity, short>("5000")), ("country", Refusal<CountryCode, string>("ZZ"))));
        problem.Codes["quantity"].Should().Be(ValueObjectErrorCodes.OutOfRange);
        problem.Codes["country"].Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Text_that_is_not_of_the_underlying_type_is_reported_as_not_parsable(bool throwOnBadRequest)
    {
        var problem = await GetProblemAsync(throwOnBadRequest, "/api/lines?quantity=abc");

        problem.Should().Be(Problem(("quantity", Refusal<Quantity, short>("abc"))));
        problem.Codes["quantity"].Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task An_absent_required_value_object_is_reported_as_required_and_an_absent_optional_one_is_not(bool throwOnBadRequest)
        => (await GetProblemAsync(throwOnBadRequest, "/api/lines"))
            .Should().Be(Problem(("quantity", ("A value is required.", ValueObjectErrorCodes.Required))));

    /// <summary>
    /// The reflection-based binding parses empty text, and refuses it for a value object that refuses it, optional or
    /// not: the refusal is reported with the rule that made it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Empty_text_is_reported_as_the_reflection_based_binding_refuses_it(bool throwOnBadRequest)
    {
        var problem = await GetProblemAsync(throwOnBadRequest, "/api/lines?quantity=5&country=");

        problem.Should().Be(Problem(("country", Refusal<CountryCode, string>(string.Empty))));
        problem.Codes["country"].Should().Be(ValueObjectErrorCodes.Required);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_value_object_is_listed_under_the_name_an_attribute_binds_it_from(bool throwOnBadRequest)
        => (await GetProblemAsync(throwOnBadRequest, "/api/named/1001?q=-1"))
            .Should().Be(Problem(("n", Refusal<Quantity, short>("1001")), ("q", Refusal<Quantity, short>("-1"))));

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_value_object_an_attribute_binds_under_its_own_name_is_listed_under_it(bool throwOnBadRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/unnamed/1001?amount=-1")
        {
            Headers = { { "customer", Guid.Empty.ToString() } },
        };

        (await SendForProblemAsync(throwOnBadRequest, request)).Should().Be(Problem(
            ("number", Refusal<Quantity, short>("1001")),
            ("amount", Refusal<Quantity, short>("-1")),
            ("customer", Refusal<CustomerId, Guid>(Guid.Empty.ToString()))));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_refused_header_is_listed_under_its_name(bool throwOnBadRequest)
    {
        using var refused = new HttpRequestMessage(HttpMethod.Get, "/api/customers/current") { Headers = { { "X-Customer", "nope" } } };
        using var absent = new HttpRequestMessage(HttpMethod.Get, "/api/customers/current");

        (await SendForProblemAsync(throwOnBadRequest, refused)).Should().Be(Problem(("X-Customer", Refusal<CustomerId, Guid>("nope"))));
        (await SendForProblemAsync(throwOnBadRequest, absent)).Should().Be(Problem(("X-Customer", ("A value is required.", ValueObjectErrorCodes.Required))));
    }

    /// <summary>
    /// The binder reads every value of a scalar query key, or of a header sent twice, as their comma-joined text, which it
    /// parses: the refusal is that text's, whatever the first value alone would give. An empty header is parsed, as empty
    /// query text is, by the reflection-based binding.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_repeated_query_key_or_header_is_reported_as_the_comma_joined_text_the_binder_parsed(bool throwOnBadRequest)
    {
        var customer = Guid.NewGuid().ToString();
        using var twice = new HttpRequestMessage(HttpMethod.Get, "/api/customers/current");
        twice.Headers.TryAddWithoutValidation("X-Customer", [customer, customer]);

        (await GetProblemAsync(throwOnBadRequest, "/api/lines?quantity=5000&quantity=1"))
            .Should().Be(Problem(("quantity", Refusal<Quantity, short>("5000,1"))));
        (await GetProblemAsync(throwOnBadRequest, "/api/lines?quantity=1&quantity=1"))
            .Should().Be(Problem(("quantity", Refusal<Quantity, short>("1,1"))));
        (await SendForProblemAsync(throwOnBadRequest, twice))
            .Should().Be(Problem(("X-Customer", Refusal<CustomerId, Guid>($"{customer},{customer}"))));
        (await SendWithEmptyHeaderAsync(throwOnBadRequest, "/api/customers/current", "X-Customer"))
            .Should().Be(Problem(("X-Customer", Refusal<CustomerId, Guid>(string.Empty))));
        Refusal<Quantity, short>("5000,1").Code.Should().Be(ValueObjectErrorCodes.NotParsable, "the joined text is no short at all");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task The_members_of_an_AsParameters_record_are_listed_under_the_names_they_bind_from(bool throwOnBadRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/search?page=-1&c=ZZ")
        {
            Headers = { { "X-Customer", Guid.Empty.ToString() } },
        };

        (await SendForProblemAsync(throwOnBadRequest, request)).Should().Be(Problem(
            ("Page", Refusal<Quantity, short>("-1")),
            ("c", Refusal<CountryCode, string>("ZZ")),
            ("X-Customer", Refusal<CustomerId, Guid>(Guid.Empty.ToString()))));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task The_settable_members_of_an_AsParameters_class_are_listed_under_their_names(bool throwOnBadRequest)
        => (await GetProblemAsync(throwOnBadRequest, "/api/filter?page=-1&country=ZZ"))
            .Should().Be(Problem(("Page", Refusal<Quantity, short>("-1")), ("Country", Refusal<CountryCode, string>("ZZ"))));

    /// <summary>
    /// Each refused element of an array is listed under its name, with the code of the first: the problem details map a
    /// member to one code, as MVC's do. An empty element of an array of nullable value objects binds as
    /// <see langword="null"/>, and is not reported.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Every_refused_element_of_an_array_is_listed_under_its_name_with_the_code_of_the_first(bool throwOnBadRequest)
    {
        var problem = await GetProblemAsync(throwOnBadRequest, "/api/batches?quantities=1&quantities=-1&quantities=x&quantities=&c=&c=ZZ");

        problem.Messages.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["quantities"] = [Refusal<Quantity, short>("-1").Message, Refusal<Quantity, short>("x").Message, Refusal<Quantity, short>(string.Empty).Message],
            ["c"] = [Refusal<CountryCode, string>("ZZ").Message],
        }, options => options.WithStrictOrdering());
        problem.Codes.Should().Equal(new Dictionary<string, string>
        {
            ["quantities"] = ValueObjectErrorCodes.OutOfRange,
            ["c"] = ValueObjectErrorCodes.NotAKnownValue,
        });
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Absent_arrays_bind_empty(bool throwOnBadRequest)
        => (await GetStringAsync(throwOnBadRequest, "/api/batches")).Should().Be("0 0");

    /// <summary>
    /// The binder reads an array from the query string even when the route has a parameter of its name, and the
    /// refusals are read from there too, not from the route value.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task An_array_named_like_a_route_parameter_is_reported_from_the_query_string(bool throwOnBadRequest)
    {
        (await GetProblemAsync(throwOnBadRequest, "/api/arrays/1?quantities=-1"))
            .Should().Be(Problem(("quantities", Refusal<Quantity, short>("-1"))));
        (await GetStringAsync(throwOnBadRequest, "/api/arrays/-1?quantities=1&quantities=2")).Should().Be("2");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_construction_of_a_generic_value_object_is_reported_as_any_other(bool throwOnBadRequest)
        => (await GetProblemAsync(throwOnBadRequest, "/api/references/TOO-LONG-REFERENCE"))
            .Should().Be(Problem(("reference", Refusal<Reference<SalesInvoice>, string>("TOO-LONG-REFERENCE"))));

    /// <summary>
    /// A value object written by hand, which nothing registered, is described by reflection; one whose parser refuses
    /// without a reason is reported as MVC's model binder reports it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_value_object_written_by_hand_is_reported_and_a_refusal_without_a_reason_as_not_parsable(bool throwOnBadRequest)
    {
        var longCode = new string('A', 201);

        (await GetProblemAsync(throwOnBadRequest, "/api/counters/abc"))
            .Should().Be(Problem(("counter", ("The value is not a valid HandWrittenCounter.", ValueObjectErrorCodes.NotParsable))));
        (await GetProblemAsync(throwOnBadRequest, $"/api/codes/{longCode}"))
            .Should().Be(Problem(("code", ("A code is at most 200 letters long.", ValueObjectErrorCodes.TooLong))));
    }

    /// <summary>
    /// A refusal that no value object made keeps the framework's answer: a parameter that is not a value object has no
    /// rule code, and is not listed beside one that is.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_refusal_no_value_object_made_keeps_the_frameworks_answer(bool throwOnBadRequest)
    {
        using (var mixed = await GetAsync(throwOnBadRequest, "/api/mixed/abc?quantity=5"))
        {
            await ShouldBeTheFrameworksAnswerAsync(mixed, throwOnBadRequest);
        }

        using (var plain = await GetAsync(throwOnBadRequest, "/api/plain/abc"))
        {
            await ShouldBeTheFrameworksAnswerAsync(plain, throwOnBadRequest);
        }

        (await GetProblemAsync(throwOnBadRequest, "/api/mixed/abc?quantity=-1"))
            .Should().Be(Problem(("quantity", Refusal<Quantity, short>("-1"))));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task An_endpoint_the_convention_does_not_cover_keeps_the_frameworks_answer(bool throwOnBadRequest)
    {
        using var response = await GetAsync(throwOnBadRequest, "/outside/XX!!");

        await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
    }

    /// <summary>
    /// A form field is not covered, nor is a form the binder cannot read at all: with <c>ThrowOnBadRequest</c> on, the
    /// exception the binder throws for it wraps no serializer's, and carries no code. A header array is not covered either.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_form_field_and_a_header_array_are_not_covered(bool throwOnBadRequest)
    {
        using var form = new HttpRequestMessage(HttpMethod.Post, "/api/forms")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("iban", "XX")]),
        };

        // A multipart form without its boundary, which the binder cannot read.
        using var unreadable = new HttpRequestMessage(HttpMethod.Post, "/api/forms") { Content = new StringContent("iban=XX") };
        unreadable.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");
        // A refused value of the same name in the query string, which a header array never reads.
        using var header = new HttpRequestMessage(HttpMethod.Get, "/api/headers?X-Quantity=-1") { Headers = { { "X-Quantity", "-1" } } };

        using (var response = await Client(throwOnBadRequest).SendAsync(form, TestContext.Current.CancellationToken))
        {
            await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
        }

        using (var response = await Client(throwOnBadRequest).SendAsync(unreadable, TestContext.Current.CancellationToken))
        {
            await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
        }

        using (var response = await Client(throwOnBadRequest).SendAsync(header, TestContext.Current.CancellationToken))
        {
            await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Values_that_bind_reach_the_handler(bool throwOnBadRequest)
    {
        (await GetStringAsync(throwOnBadRequest, "/api/accounts/FR7630006000011234567890189")).Should().Be("FR7630006000011234567890189");
        (await GetStringAsync(throwOnBadRequest, "/api/lines?quantity=5&country=fr")).Should().Be("5 FR");
        (await GetStringAsync(throwOnBadRequest, "/api/plain/7")).Should().Be("7");
        (await GetStringAsync(throwOnBadRequest, "/api/raw")).Should().Be("raw");
    }

    /// <summary>
    /// With <c>ThrowOnBadRequest</c> off, the framework answers a refused body before any filter runs, and keeps its
    /// exception: the body gets the bare 400.
    /// </summary>
    [Fact]
    public async Task A_refused_body_keeps_the_bare_400_in_Production()
    {
        using var response = await production.Client.PostAsJsonAsync("/api/orders", new { reference = "no" }, TestContext.Current.CancellationToken);

        await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest: false);
    }

    /// <summary>
    /// With <c>ThrowOnBadRequest</c> on, the exception handler reads the code off the exception the serializer threw, and
    /// lists the refusal under its JSON path, the key MVC gives it, with the message of the converter.
    /// </summary>
    [Fact]
    public async Task A_refused_body_is_listed_under_its_JSON_path_in_Development()
    {
        using var response = await development.Client.PostAsJsonAsync("/api/orders", new { reference = "no" }, TestContext.Current.CancellationToken);
        var problem = await ReadProblemAsync(response);

        problem.Codes.Should().Equal(new Dictionary<string, string> { ["$.reference"] = ValueObjectErrorCodes.TooShort });
        problem.Messages["$.reference"].Should().ContainSingle().Which.Should().Contain("at least 3 characters").And.NotContain("\"no\"");
    }

    [Fact]
    public async Task A_token_of_the_wrong_kind_in_a_body_is_listed_as_not_parsable_in_Development()
    {
        using var content = new StringContent("""{"reference":12}""", Encoding.UTF8, "application/json");
        using var response = await development.Client.PostAsync("/api/orders", content, TestContext.Current.CancellationToken);

        (await ReadProblemAsync(response)).Codes.Should().Equal(new Dictionary<string, string> { ["$.reference"] = ValueObjectErrorCodes.NotParsable });
    }

    [Fact]
    public async Task A_value_object_read_as_the_whole_body_is_listed_under_the_root_path_in_Development()
    {
        using var response = await development.Client.PostAsJsonAsync("/api/bodies", "XX", TestContext.Current.CancellationToken);

        (await ReadProblemAsync(response)).Codes.Should().Equal(new Dictionary<string, string> { ["$"] = ValueObjectErrorCodes.TooShort });
    }

    /// <summary>
    /// A body the serializer cannot read at all carries no code, and a body of another media type is a 415, which the
    /// binder answers itself whatever <c>ThrowOnBadRequest</c> says: neither is the package's to answer.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_body_no_value_object_refused_keeps_the_frameworks_answer(bool throwOnBadRequest)
    {
        using (var malformed = new StringContent("""{"reference":""", Encoding.UTF8, "application/json"))
        using (var response = await Client(throwOnBadRequest).PostAsync("/api/orders", malformed, TestContext.Current.CancellationToken))
        {
            await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
        }

        using (var text = new StringContent("no", Encoding.UTF8, "text/plain"))
        using (var response = await Client(throwOnBadRequest).PostAsync("/api/orders", text, TestContext.Current.CancellationToken))
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType, "the binder answers it in either setting");
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(ValueObjectProblemDetails.ExtensionName);
        }
    }

    /// <summary>
    /// A body the serializer cannot read at all keeps the framework's answer beside a refused query value: with
    /// <c>ThrowOnBadRequest</c> off, the framework answers it before any filter runs, and with it on, the binder throws
    /// for the body, and the exception handler leaves the exception alone, rather than explain the query value alone.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_body_the_serializer_cannot_read_keeps_the_frameworks_answer_beside_a_refused_query_value(bool throwOnBadRequest)
    {
        using var malformed = new StringContent("""{"reference":""", Encoding.UTF8, "application/json");
        using var response = await Client(throwOnBadRequest).PostAsync("/api/placements?quantity=-1", malformed, TestContext.Current.CancellationToken);

        await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
    }

    /// <summary>
    /// A body the serializer refused with a code is the package's to explain, and the value objects of the route, the
    /// query string and the headers are parsed again beside it.
    /// </summary>
    [Fact]
    public async Task A_refused_body_is_listed_beside_a_refused_query_value_in_Development()
    {
        using var response = await development.Client.PostAsJsonAsync("/api/placements?quantity=-1", new { reference = "no" }, TestContext.Current.CancellationToken);

        (await ReadProblemAsync(response)).Codes.Should().Equal(new Dictionary<string, string>
        {
            ["$.reference"] = ValueObjectErrorCodes.TooShort,
            ["quantity"] = ValueObjectErrorCodes.OutOfRange,
        });
    }

    /// <summary>
    /// The Request Delegate Generator refuses no empty query text, binding <see langword="null"/> or the default instance:
    /// on an endpoint it marks as its own, empty text is never reported, scalar or element, even where the
    /// reflection-based binding that runs here refuses it. Another tool's mark changes nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Empty_text_is_not_reported_on_an_endpoint_the_Request_Delegate_Generator_wrote(bool throwOnBadRequest)
    {
        (await GetProblemAsync(throwOnBadRequest, "/api/generated?quantity=x&country=&quantities=&quantities=-1"))
            .Should().Be(Problem(("quantity", Refusal<Quantity, short>("x")), ("quantities", Refusal<Quantity, short>("-1"))));

        using (var response = await GetAsync(throwOnBadRequest, "/api/generated?quantity=5&country="))
        {
            await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
        }

        (await GetProblemAsync(throwOnBadRequest, "/api/tooled?quantity=5&country="))
            .Should().Be(Problem(("country", Refusal<CountryCode, string>(string.Empty))));
    }

    [Fact]
    public void An_endpoint_covered_twice_is_covered_once()
    {
        var twice = production.Endpoints.OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == "/api/twice/{iban}");

        twice.Metadata.GetOrderedMetadata<ValueObjectBindingMetadata>().Should().ContainSingle();
    }

    [Fact]
    public async Task An_endpoint_covered_twice_answers_once()
        => (await GetProblemAsync(false, "/api/twice/XX!!")).Should().Be(Problem(("iban", Refusal<Iban, string>("XX!!"))));

    /// <summary>
    /// The serializer applies the dictionary key policy of the HTTP JSON options to the keys of <c>errors</c>; the codes
    /// are keyed through it as well, so that both are keyed alike, as MVC keys them.
    /// </summary>
    [Fact]
    public async Task The_codes_are_keyed_through_the_dictionary_key_policy_as_the_messages_are()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.KebabCaseUpper);
        builder.Services.AddProblemDetails();
        await using var application = builder.Build();
        application.MapGroup("/api").WithValueObjectProblemDetails().MapGet("/lines", static (Quantity quantity) => quantity.ToString());
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var client = application.GetTestClient();

        using var response = await client.GetAsync("/api/lines?quantity=-1", TestContext.Current.CancellationToken);
        var problem = await ReadProblemAsync(response);

        problem.Messages.Keys.Should().Equal("QUANTITY");
        problem.Codes.Should().Equal(new Dictionary<string, string> { ["QUANTITY"] = ValueObjectErrorCodes.OutOfRange });
    }

    /// <summary>
    /// Without reflection-based serialization, as in a native binary, the HTTP JSON options write the problem details only
    /// through a serializer context that knows both <see cref="HttpValidationProblemDetails"/> and the
    /// <see cref="JsonElement"/> of the codes: without one, the convention fails the build of the endpoints, naming
    /// <c>AddProblemDetails()</c>, rather than answer each refusal with a 500 where the framework answers a 400.
    /// </summary>
    [Fact]
    public async Task Without_reflection_or_a_context_knowing_the_problem_details_the_endpoints_fail_to_build()
    {
        foreach (var context in (JsonSerializerContext?[])[null, ProblemOnlyJsonContext.Default])
        {
            await using var application = WithoutReflection(context, addProblemDetails: false);
            var build = () => ((IEndpointRouteBuilder)application).DataSources.SelectMany(static source => source.Endpoints).ToList();

            build.Should().Throw<InvalidOperationException>()
                .WithMessage("*no resolver of theirs knows HttpValidationProblemDetails or JsonElement*builder.Services.AddProblemDetails()*");
        }
    }

    /// <summary>
    /// <c>AddProblemDetails()</c> chains the framework's serializer context for problem details into the HTTP JSON
    /// options, and a context of the application's listing both types does as well: either writes the refusal.
    /// </summary>
    [Fact]
    public async Task Without_reflection_AddProblemDetails_or_a_context_knowing_the_problem_details_writes_them()
    {
        foreach (var (context, addProblemDetails) in new (JsonSerializerContext?, bool)[] { (null, true), (ProblemJsonContext.Default, false) })
        {
            await using var application = WithoutReflection(context, addProblemDetails);
            await application.StartAsync(TestContext.Current.CancellationToken);
            using var client = application.GetTestClient();

            using var response = await client.GetAsync("/api/lines?quantity=-1", TestContext.Current.CancellationToken);

            (await ReadProblemAsync(response)).Should().Be(Problem(("quantity", Refusal<Quantity, short>("-1"))));
        }
    }

    [Fact]
    public void The_builders_are_required()
    {
        var convention = () => ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails<RouteGroupBuilder>(null!);
        var services = () => ValueObjectHttpServiceCollectionExtensions.AddValueObjectHttpProblemDetails(null!);

        convention.Should().Throw<ArgumentNullException>().WithParameterName("builder");
        services.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    /// <summary>Gets the message and the code a value object refuses text with, as the binder parses it.</summary>
    internal static (string Message, string Code) Refusal<TSelf, TValue>(string text)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse($"'{text}' is refused");

        return (validation.ErrorMessage!, validation.ErrorCode!);
    }

    /// <summary>Gets the problem a request answers, read as the messages and the codes of each member.</summary>
    internal static async Task<ReadProblem> ReadProblemAsync(HttpResponseMessage response)
        => ProblemOf((int)response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    /// <summary>Gets the problem a response answers, read as the messages and the codes of each member.</summary>
    internal static ReadProblem ProblemOf(int status, string body)
    {
        status.Should().Be(StatusCodes.Status400BadRequest, body);
        var problem = JsonDocument.Parse(body).RootElement;

        return new ReadProblem(
            problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!,
            problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>()!);
    }

    private static ReadProblem Problem(params (string Member, (string Message, string Code) Refusal)[] refusals)
        => new(
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => new[] { refusal.Refusal.Message }),
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => refusal.Refusal.Code));

    /// <summary>
    /// The answer the framework gives a refusal without the package: an empty 400 in Production, and the exception
    /// handler's 500 in Development.
    /// </summary>
    private static async Task ShouldBeTheFrameworksAnswerAsync(HttpResponseMessage response, bool throwOnBadRequest)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(throwOnBadRequest ? HttpStatusCode.InternalServerError : HttpStatusCode.BadRequest, body);
        body.Should().NotContain(ValueObjectProblemDetails.ExtensionName);
    }

    /// <summary>
    /// An application whose HTTP JSON options serialize without reflection, as in a native binary, through the context
    /// given alone, with <c>AddProblemDetails()</c> or without it.
    /// </summary>
    private static WebApplication WithoutReflection(JsonSerializerContext? context, bool addProblemDetails)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolver = context is null
            ? JsonTypeInfoResolver.Combine()
            : JsonTypeInfoResolver.Combine(context));
        if (addProblemDetails)
        {
            builder.Services.AddProblemDetails();
        }

        var application = builder.Build();
        application.MapGroup("/api").WithValueObjectProblemDetails().MapGet("/lines", static (Quantity quantity) => quantity.ToString());

        return application;
    }

    private HttpClient Client(bool throwOnBadRequest) => throwOnBadRequest ? development.Client : production.Client;

    private Task<HttpResponseMessage> GetAsync(bool throwOnBadRequest, string url)
        => Client(throwOnBadRequest).GetAsync(url, TestContext.Current.CancellationToken);

    private Task<string> GetStringAsync(bool throwOnBadRequest, string url)
        => Client(throwOnBadRequest).GetStringAsync(url, TestContext.Current.CancellationToken);

    private async Task<ReadProblem> GetProblemAsync(bool throwOnBadRequest, string url)
    {
        using var response = await GetAsync(throwOnBadRequest, url);

        return await ReadProblemAsync(response);
    }

    /// <summary>
    /// Sends a request built on the server's own context, which carries an empty header as Kestrel hands one over, where
    /// <see cref="HttpClient"/> drops it.
    /// </summary>
    private async Task<ReadProblem> SendWithEmptyHeaderAsync(bool throwOnBadRequest, string path, string header)
    {
        var context = await (throwOnBadRequest ? (MinimalApiProblemDetailsApplication)development : production).Server.SendAsync(
            request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = path;
                request.Request.Headers[header] = string.Empty;
            },
            TestContext.Current.CancellationToken);
        using var reader = new StreamReader(context.Response.Body);

        return ProblemOf(context.Response.StatusCode, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    private async Task<ReadProblem> SendForProblemAsync(bool throwOnBadRequest, HttpRequestMessage request)
    {
        using var response = await Client(throwOnBadRequest).SendAsync(request, TestContext.Current.CancellationToken);

        return await ReadProblemAsync(response);
    }
}

/// <summary>The messages and the codes a validation problem lists for each member, compared as values.</summary>
/// <param name="Messages">The <c>errors</c> member.</param>
/// <param name="Codes">The <c>errorCodes</c> member.</param>
public sealed record ReadProblem(Dictionary<string, string[]> Messages, Dictionary<string, string> Codes)
{
    /// <inheritdoc />
    public bool Equals(ReadProblem? other)
        => other is not null
           && Messages.Count == other.Messages.Count
           && Messages.All(entry => other.Messages.TryGetValue(entry.Key, out var messages) && messages.SequenceEqual(entry.Value))
           && Codes.Count == other.Codes.Count
           && Codes.All(entry => other.Codes.TryGetValue(entry.Key, out var code) && code == entry.Value);

    /// <inheritdoc />
    public override int GetHashCode() => Codes.Count;

    /// <inheritdoc />
    public override string ToString()
        => string.Join("; ", Messages.Select(entry => $"{entry.Key}: [{string.Join(" | ", entry.Value)}] {Codes.GetValueOrDefault(entry.Key)}"));
}

/// <summary>A serializer context that knows the validation problem, and not the <see cref="JsonElement"/> of its codes.</summary>
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class ProblemOnlyJsonContext : JsonSerializerContext;

/// <summary>A serializer context that knows the validation problem and the <see cref="JsonElement"/> of its codes.</summary>
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class ProblemJsonContext : JsonSerializerContext;
