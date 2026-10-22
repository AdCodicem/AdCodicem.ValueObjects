using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using static AdCodicem.ValueObjects.UnitTests.Functions.FunctionInvocation;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>
/// The answer of an HTTP trigger with the messages of the application's resource, read by the code of the rule in the
/// culture of the request. The invocation runs on the host's call, not on the request's, so the worker's culture is never
/// the request's: the middleware applies the culture <c>UseRequestLocalization()</c> set on the request, or else lets the
/// application's <c>RequestLocalizationOptions</c> choose one from it, the current culture by default.
/// </summary>
public sealed class LocalizedFunctionsProblemDetailsTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly ValueObjectMessageOptions Messages = new ValueObjectMessageOptions().UseResource<ValidationMessages>();

    /// <summary>
    /// <c>UseRequestLocalization()</c>, added to the integration's pipeline through a startup filter, sets the culture it
    /// chose on the request alone: the middleware applies it around the lookup, and gives the worker's back.
    /// </summary>
    [Fact]
    public async Task The_culture_the_request_localization_middleware_chose_gives_the_messages()
    {
        var request = FunctionsHttp.Request(static services => services.AddLocalization());
        request.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(French), provider: null));
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        var problem = await AnswerAsync(request);

        problem.Should().Be(FrenchProblem());
        CultureInfo.CurrentCulture.Should().Be(culture);
        CultureInfo.CurrentUICulture.Should().Be(uiCulture);
    }

    /// <summary>
    /// The request's UI culture chooses the message and its culture formats the arguments, each applied on its own, over a
    /// worker running in the other two: a French message whose bounds are written as English writes them.
    /// </summary>
    [Fact]
    public async Task The_requests_UI_culture_chooses_the_message_and_its_culture_formats_the_arguments()
    {
        var request = FunctionsHttp.Request(static services => services.AddLocalization());
        request.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(culture: English, uiCulture: French), provider: null));
        var invocation = new FunctionInvocation("Weigh", [Parameter<Mass>("mass")], new Dictionary<string, object?> { ["mass"] = "-1" }, request);
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (French, English);
            await FunctionsHttp.Middleware(Messages).RefuseAsync(invocation);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(new ReadProblem(
            new() { ["mass"] = ["« mass » doit être compris entre 9.1E-31 et 2E+32."] },
            new() { ["mass"] = ValueObjectErrorCodes.OutOfRange }));
    }

    /// <summary>
    /// The request's culture formats the messages and reads no text: a value object is parsed again in the invariant
    /// culture, as the converter parsed it, so a decimal written with a French comma, which the converter refused, is
    /// reported refused.
    /// </summary>
    [Fact]
    public async Task The_requests_culture_reads_no_text()
    {
        var request = FunctionsHttp.Request(static services => services.AddLocalization());
        request.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(French), provider: null));
        var invocation = new FunctionInvocation("Total", [Parameter<Amount>("total")], new Dictionary<string, object?> { ["total"] = "12,5" }, request);

        await FunctionsHttp.Middleware(Messages).RefuseAsync(invocation);

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(new ReadProblem(
            new() { ["total"] = ["« total » n'a pas la forme attendue."] },
            new() { ["total"] = ValueObjectErrorCodes.NotParsable }));
    }

    /// <summary>
    /// Without the request localization middleware in the pipeline, the application's <c>RequestLocalizationOptions</c>
    /// choose the culture from the request, as that middleware would: from <c>?culture=</c> or <c>Accept-Language</c>.
    /// </summary>
    [Theory]
    [InlineData("?culture=fr-FR", null)]
    [InlineData("", "fr-FR,fr;q=0.9")]
    public async Task The_applications_request_localization_options_choose_the_culture_from_the_request(string query, string? language)
    {
        var request = FunctionsHttp.Request(static services => services.AddLocalization().AddRequestLocalization(static options =>
        {
            options.DefaultRequestCulture = new RequestCulture(English);
            options.SupportedCultures = [English, French];
            options.SupportedUICultures = [English, French];
        }));
        request.Request.QueryString = new QueryString(query);
        if (language is not null)
        {
            request.Request.Headers.AcceptLanguage = language;
        }

        (await AnswerAsync(request)).Should().Be(FrenchProblem());
    }

    [Fact]
    public async Task A_culture_the_resource_does_not_hold_keeps_the_English_messages()
    {
        var request = FunctionsHttp.Request(static services => services.AddLocalization().AddRequestLocalization(static options =>
        {
            options.DefaultRequestCulture = new RequestCulture(English);
            options.SupportedCultures = [English, French];
            options.SupportedUICultures = [English, French];
        }));
        request.Request.QueryString = new QueryString("?culture=en-US");

        (await AnswerAsync(request)).Should().Be(EnglishProblem());
    }

    /// <summary>
    /// With no options of the application's, the framework's defaults keep the current culture, which is the worker's: an
    /// application that names a resource but chooses no culture is answered in the culture its worker runs in.
    /// </summary>
    [Fact]
    public async Task Without_options_the_current_culture_gives_the_messages()
    {
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (French, French);
            (await AnswerAsync(FunctionsHttp.Request(static services => services.AddLocalization()))).Should().Be(FrenchProblem());

            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (English, English);
            (await AnswerAsync(FunctionsHttp.Request(static services => services.AddLocalization()))).Should().Be(EnglishProblem());
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    /// <summary>Without a resource, a refusal keeps the English message of its rule, whatever the request asks for.</summary>
    [Fact]
    public async Task Without_a_resource_the_English_messages_stay()
    {
        var request = FunctionsHttp.Request();
        request.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(French), provider: null));

        (await AnswerAsync(request, named: false)).Should().Be(EnglishProblem());
    }

    [Fact]
    public async Task A_resource_without_localization_services_names_the_call_to_add()
    {
        var request = FunctionsHttp.Request();
        request.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(French), provider: null));

        var answer = () => AnswerAsync(request);

        (await answer.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("AddLocalization()");
    }

    /// <summary>
    /// Runs the middleware, with the messages of the resource or without, over an invocation refusing a range, a format
    /// and an absent value.
    /// </summary>
    private static async Task<ReadProblem> AnswerAsync(DefaultHttpContext request, bool named = true)
    {
        var invocation = new FunctionInvocation(
            "Line",
            [Parameter<Quantity>("qty"), Parameter<EmailAddress>("email"), Parameter<CustomerId>("customer")],
            new Dictionary<string, object?> { ["qty"] = "5000", ["email"] = "nope" },
            request);

        await FunctionsHttp.Middleware(named ? Messages : null).RefuseAsync(invocation);

        return await FunctionsHttp.AnswerAsync(invocation, request);
    }

    private static ReadProblem FrenchProblem() => new(
        new()
        {
            ["qty"] = ["« qty » doit être compris entre 0 et 1000."],
            ["email"] = ["Le format de « email » n'est pas valide."],
            ["customer"] = ["« customer » est obligatoire."],
        },
        Codes());

    private static ReadProblem EnglishProblem() => new(
        new()
        {
            ["qty"] = ["The value must be less than or equal to 1000."],
            ["email"] = [ProblemReading.Refusal<EmailAddress, string>("nope").Message],
            ["customer"] = ["A value is required."],
        },
        Codes());

    private static Dictionary<string, string> Codes() => new()
    {
        ["qty"] = ValueObjectErrorCodes.OutOfRange,
        ["email"] = ValueObjectErrorCodes.InvalidFormat,
        ["customer"] = ValueObjectErrorCodes.Required,
    };
}
