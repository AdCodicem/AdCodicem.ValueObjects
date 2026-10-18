using System.Globalization;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.Web;
using AdCodicem.ValueObjects.Wolverine.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// The helpers the code Wolverine generates for an endpoint calls: reading the text of a route value, a query value, a
/// header or a form field, parsing it through the value object's rules, and answering the refusals of a request at once.
/// </summary>
public sealed class WolverineHttpBindingTests
{
    [Fact]
    public void A_route_value_is_read_as_the_text_routing_matched()
    {
        var request = WolverineHttpRequests.Request();
        request.Request.RouteValues["qty"] = "5";
        request.Request.RouteValues["number"] = 5;

        ValueObjectHttpBinding.Route(request, "qty").Should().Be("5");
        ValueObjectHttpBinding.Route(request, "number").Should().BeNull("only routing's text is read");
        ValueObjectHttpBinding.Route(request, "absent").Should().BeNull();
    }

    [Fact]
    public void A_query_value_is_read_as_its_text_the_values_of_a_repeated_key_joined_and_empty_text_kept()
    {
        var request = WolverineHttpRequests.Request();
        request.Request.QueryString = new QueryString("?empty=&repeated=1&repeated=2&one=3");

        ValueObjectHttpBinding.Query(request, "absent").Should().BeNull();
        ValueObjectHttpBinding.Query(request, "empty").Should().BeEmpty("empty text is parsed, not taken for an absent value");
        ValueObjectHttpBinding.Query(request, "repeated").Should().Be("1,2");
        ValueObjectHttpBinding.Query(request, "ONE").Should().Be("3", "a query key is compared without regard to case");
        ValueObjectHttpBinding.QueryValues(request, "repeated").Should().Equal("1", "2");
        ValueObjectHttpBinding.QueryValues(request, "absent").Should().BeEmpty();
    }

    [Fact]
    public void A_header_is_read_as_its_text_the_values_of_a_repeated_header_joined_and_empty_text_kept()
    {
        var request = WolverineHttpRequests.Request();
        request.Request.Headers["X-Empty"] = string.Empty;
        request.Request.Headers["X-Repeated"] = new StringValues(["ABC-123", "DEF-456"]);

        ValueObjectHttpBinding.Header(request, "X-Absent").Should().BeNull();
        ValueObjectHttpBinding.Header(request, "X-Empty").Should().BeEmpty();
        ValueObjectHttpBinding.Header(request, "x-repeated").Should().Be("ABC-123,DEF-456", "Wolverine alone throws for a header sent twice");
    }

    [Fact]
    public async Task A_form_field_is_read_from_a_form_body_alone()
    {
        var form = WolverineHttpRequests.Request().WithForm("qty=5&repeated=1&repeated=2&empty=");

        (await ValueObjectHttpBinding.FormAsync(form, "qty")).Should().Be("5");
        (await ValueObjectHttpBinding.FormAsync(form, "repeated")).Should().Be("1,2");
        (await ValueObjectHttpBinding.FormAsync(form, "empty")).Should().BeEmpty();
        (await ValueObjectHttpBinding.FormAsync(form, "absent")).Should().BeNull();
        (await ValueObjectHttpBinding.FormValuesAsync(form, "repeated")).Should().Equal("1", "2");
        (await ValueObjectHttpBinding.FormValuesAsync(form, "absent")).Should().BeEmpty();

        var json = WolverineHttpRequests.Request();
        json.Request.ContentType = "application/json";
        json.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"qty\":5}"));
        (await ValueObjectHttpBinding.FormAsync(json, "qty")).Should().BeNull("a JSON body holds no form field");
        (await ValueObjectHttpBinding.FormValuesAsync(json, "qty")).Should().BeEmpty();
    }

    [Fact]
    public async Task An_accepted_value_binds_and_leaves_nothing_to_answer()
    {
        var request = WolverineHttpRequests.Request();

        ValueObjectHttpBinding.Bind<Quantity, short>(request, "qty", "5", required: true).Should().Be(Quantity.Create(5));
        IsDefault<Quantity, short>(ValueObjectHttpBinding.Bind<Quantity, short>(request, "absent", null, required: false))
            .Should().BeTrue("an optional value left out is left to its default");

        (await ValueObjectHttpBinding.TryAnswerRefusalsAsync(request)).Should().BeFalse();
        request.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        (await request.BodyAsync()).Should().BeEmpty();
        ValueObjectProblemDetails.GetErrorCodes(request).Should().BeEmpty();
    }

    [Fact]
    public async Task A_refused_value_is_answered_400_with_the_message_and_the_code_of_its_rule_recorded_on_the_request()
    {
        var request = WolverineHttpRequests.Request();

        IsDefault<Quantity, short>(ValueObjectHttpBinding.Bind<Quantity, short>(request, "qty", "5000", required: true))
            .Should().BeTrue("the handler never sees it");

        (await request.AnswerAsync()).Should().Be(Problem(("qty", MinimalApiProblemDetailsTests.Refusal<Quantity, short>("5000"))));
        request.Response.ContentType.Should().StartWith("application/problem+json");
        ValueObjectProblemDetails.GetErrorCodes(request).Should().Equal(new Dictionary<string, string> { ["qty"] = ValueObjectErrorCodes.OutOfRange });
    }

    [Fact]
    public async Task A_refusal_without_a_reason_is_reported_as_text_the_type_cannot_parse()
    {
        var request = WolverineHttpRequests.Request();

        ValueObjectHttpBinding.Bind<HandWrittenCounter, int>(request, "counter", "abc", required: true);

        (await request.AnswerAsync()).Should().Be(Problem(("counter", ("The value is not a valid HandWrittenCounter.", ValueObjectErrorCodes.NotParsable))));
    }

    [Fact]
    public async Task An_absent_required_value_is_refused_as_required()
    {
        var request = WolverineHttpRequests.Request();

        IsDefault<Quantity, short>(ValueObjectHttpBinding.Bind<Quantity, short>(request, "qty", null, required: true)).Should().BeTrue();

        (await request.AnswerAsync()).Should().Be(Problem(("qty", ("A value is required.", ValueObjectErrorCodes.Required))));
        ValueObjectProblemDetails.GetErrorCodes(request).Should().Equal(new Dictionary<string, string> { ["qty"] = ValueObjectErrorCodes.Required });
    }

    [Fact]
    public async Task A_nullable_value_binds_null_when_absent_and_parses_empty_text()
    {
        var absent = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindNullable<CountryCode, string>(absent, "country", null).Should().BeNull();
        (await ValueObjectHttpBinding.TryAnswerRefusalsAsync(absent)).Should().BeFalse();

        var accepted = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindNullable<CountryCode, string>(accepted, "country", "fr").Should().Be(CountryCode.France);

        var empty = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindNullable<CountryCode, string>(empty, "country", string.Empty).Should().BeNull();
        (await empty.AnswerAsync()).Should().Be(Problem(("country", MinimalApiProblemDetailsTests.Refusal<CountryCode, string>(string.Empty))));
    }

    [Fact]
    public async Task Every_element_of_a_repeated_value_is_parsed_each_refusal_listed_under_its_name_with_the_first_code()
    {
        var none = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindArray<Quantity, short>(none, "qty", StringValues.Empty).Should().BeEmpty();
        (await ValueObjectHttpBinding.TryAnswerRefusalsAsync(none)).Should().BeFalse();

        var request = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindArray<Quantity, short>(request, "qty", new StringValues(["1", "5000", "x", null])).Should().HaveCount(4);

        (await request.AnswerAsync()).Should().Be(new ReadProblem(
            new()
            {
                ["qty"] =
                [
                    MinimalApiProblemDetailsTests.Refusal<Quantity, short>("5000").Message,
                    MinimalApiProblemDetailsTests.Refusal<Quantity, short>("x").Message,
                    MinimalApiProblemDetailsTests.Refusal<Quantity, short>(string.Empty).Message,
                ],
            },
            new() { ["qty"] = ValueObjectErrorCodes.OutOfRange }));

        // A null element is parsed as empty text, whose message differs from a missing value's for a string.
        var texts = WolverineHttpRequests.Request();
        ValueObjectHttpBinding.BindArray<CountryCode, string>(texts, "countries", new StringValues(["fr", null]))[0].Should().Be(CountryCode.France);
        (await texts.AnswerAsync()).Should().Be(Problem(("countries", MinimalApiProblemDetailsTests.Refusal<CountryCode, string>(string.Empty))));
    }

    [Fact]
    public async Task Every_refusal_of_a_request_is_answered_at_once_keyed_as_the_serializer_keys_errors()
    {
        var request = WolverineHttpRequests.Request(configure: static services =>
            services.ConfigureHttpJsonOptions(static json => json.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase));

        ValueObjectHttpBinding.Bind<Quantity, short>(request, "Quantity", "5000", required: true);
        ValueObjectHttpBinding.Bind<CountryCode, string>(request, "Country", "ZZ", required: true);

        (await request.AnswerAsync()).Should().Be(Problem(
            ("quantity", MinimalApiProblemDetailsTests.Refusal<Quantity, short>("5000")),
            ("country", MinimalApiProblemDetailsTests.Refusal<CountryCode, string>("ZZ"))));
    }

    [Fact]
    public async Task The_problem_goes_through_the_problem_details_service_when_one_is_registered()
    {
        IReadOnlyDictionary<string, string>? recorded = null;
        var request = WolverineHttpRequests.Request(configure: services => services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                recorded = ValueObjectProblemDetails.GetErrorCodes(context.HttpContext);
                context.ProblemDetails.Extensions["customized"] = true;
            }));

        ValueObjectHttpBinding.Bind<Quantity, short>(request, "qty", "5000", required: true);
        (await ValueObjectHttpBinding.TryAnswerRefusalsAsync(request)).Should().BeTrue();

        var body = JsonDocument.Parse(await request.BodyAsync()).RootElement;
        body.GetProperty("customized").GetBoolean().Should().BeTrue();
        body.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("qty").GetString().Should().Be(ValueObjectErrorCodes.OutOfRange);
        recorded.Should().Equal(new Dictionary<string, string> { ["qty"] = ValueObjectErrorCodes.OutOfRange }, "a middleware or a log reads the codes recorded on the request");
    }

    [Fact]
    public async Task Without_the_package_s_options_a_refusal_keeps_the_English_message_of_its_rule()
    {
        var bare = WolverineHttpRequests.Bare();
        ValueObjectHttpBinding.Bind<Quantity, short>(bare, "qty", "5000", required: true);
        (await bare.AnswerAsync()).Should().Be(Problem(("qty", MinimalApiProblemDetailsTests.Refusal<Quantity, short>("5000"))));

        var withoutPolicy = WolverineHttpRequests.Bare(static services => services.AddSingleton(new WolverineHttpOptions()));
        ValueObjectHttpBinding.Bind<Quantity, short>(withoutPolicy, "qty", null, required: true);
        (await withoutPolicy.AnswerAsync()).Should().Be(Problem(("qty", ("A value is required.", ValueObjectErrorCodes.Required))));
    }

    [Fact]
    public async Task The_messages_UseValueObjects_names_answer_in_the_culture_of_the_request()
    {
        using var culture = new CultureScope("fr-FR");
        var messages = new ValueObjectMessageOptions().UseResource<ValidationMessages>();
        var request = WolverineHttpRequests.Request(messages, static services => services.AddLocalization());

        ValueObjectHttpBinding.Bind<Quantity, short>(request, "qty", "5000", required: true);
        ValueObjectHttpBinding.Bind<CountryCode, string>(request, "country", "ZZ", required: true);
        ValueObjectHttpBinding.Bind<Quantity, short>(request, "absent", null, required: true);

        (await request.AnswerAsync()).Should().Be(Problem(
            ("qty", ("« qty » doit être compris entre 0 et 1000.", ValueObjectErrorCodes.OutOfRange)),
            ("country", ("« country » doit être l'une des valeurs suivantes : FR; BE; LU.", ValueObjectErrorCodes.NotAKnownValue)),
            ("absent", ("« absent » est obligatoire.", ValueObjectErrorCodes.Required))));
    }

    [Fact]
    public async Task A_key_the_resource_lacks_keeps_the_English_message()
    {
        using var culture = new CultureScope("fr-FR");
        var messages = new ValueObjectMessageOptions().UseResource<SparseMessages>();
        var request = WolverineHttpRequests.Request(messages, static services => services.AddLocalization());

        ValueObjectHttpBinding.Bind<CountryCode, string>(request, "country", "ZZ", required: true);
        ValueObjectHttpBinding.Bind<Quantity, short>(request, "absent", null, required: true);

        (await request.AnswerAsync()).Should().Be(Problem(
            ("country", MinimalApiProblemDetailsTests.Refusal<CountryCode, string>("ZZ")),
            ("absent", ("A value is required.", ValueObjectErrorCodes.Required))));
    }

    [Fact]
    public void Messages_named_without_localization_fail_the_refusal_naming_the_call()
    {
        var request = WolverineHttpRequests.Request(new ValueObjectMessageOptions().UseResource<ValidationMessages>());

        var binding = () => ValueObjectHttpBinding.Bind<Quantity, short>(request, "qty", "5000", required: true);

        binding.Should().Throw<InvalidOperationException>().WithMessage("*AddLocalization()*");
    }

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, binds through the helpers as its own
    /// <c>TryParse</c> reads the text, and a refused one is answered as a minimal API answers it.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(GeneratedSurface.Samples.Names), MemberType = typeof(GeneratedSurface.Samples))]
    public async Task Every_value_object_binds_through_a_Wolverine_endpoint_as_minimal_APIs_bind_it(string type)
        => await GeneratedSurface.Samples.All[type].BindsThroughAWolverineEndpointAsync();

    [Fact]
    public async Task The_request_is_required()
    {
        var calls = new Func<Task>[]
        {
            () => Task.FromResult(ValueObjectHttpBinding.Bind<Quantity, short>(null!, "qty", "5", required: true)),
            () => Task.FromResult(ValueObjectHttpBinding.BindNullable<Quantity, short>(null!, "qty", "5")),
            () => Task.FromResult(ValueObjectHttpBinding.BindArray<Quantity, short>(null!, "qty", StringValues.Empty)),
            () => Task.FromResult(ValueObjectHttpBinding.Route(null!, "qty")),
            () => Task.FromResult(ValueObjectHttpBinding.Query(null!, "qty")),
            () => Task.FromResult(ValueObjectHttpBinding.QueryValues(null!, "qty")),
            () => Task.FromResult(ValueObjectHttpBinding.Header(null!, "qty")),
            () => ValueObjectHttpBinding.FormAsync(null!, "qty"),
            () => ValueObjectHttpBinding.FormValuesAsync(null!, "qty"),
            () => ValueObjectHttpBinding.TryAnswerRefusalsAsync(null!),
        };

        foreach (var call in calls)
        {
            (await call.Should().ThrowAsync<ArgumentNullException>()).WithParameterName("httpContext");
        }
    }

    private static ReadProblem Problem(params (string Member, (string Message, string Code) Refusal)[] refusals)
        => new(
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => new[] { refusal.Refusal.Message }),
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => refusal.Refusal.Code));

    private static bool IsDefault<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => value.IsDefault;

    /// <summary>Sets the current culture and UI culture, and gives the previous ones back.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
            => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
