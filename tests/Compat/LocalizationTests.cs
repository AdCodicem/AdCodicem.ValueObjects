using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Http;
using AdCodicem.ValueObjects.FluentValidation;
using FluentValidation;
using FluentValidation.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>The type the French messages of <c>Messages.fr.resx</c> are named after.</summary>
public sealed class Messages;

/// <summary>
/// The messages of refused value objects in the language of the request, on the next major of ASP.NET Core: MVC's binder
/// and input formatter, and a minimal API's filter and exception handler, read <see cref="Messages"/> through the
/// localizer of this major, and the codes stay the same.
/// </summary>
public sealed class LocalizationTests
{
    [Fact]
    public async Task MVC_answers_a_refused_query_value_and_body_in_the_language_of_the_request()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddLocalization();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(CustomersController).Assembly));
            })
            .AddValueObjects(static messages => messages.UseResource<Messages>());
        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());
        await using var application = builder.Build();
        application.UseRequestLocalization(static options => options.SetDefaultCulture("fr-FR").AddSupportedCultures("fr-FR", "en-US").AddSupportedUICultures("fr-FR", "en-US"));
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var client = application.GetTestClient();
        using var body = new StringContent(
            $$"""{"customer":"0193b1c0-0000-7000-8000-000000000001","email":"not an address","amount":1,"country":"fr","payment":"{{PaymentId.New()}}","purchase":"po-1"}""",
            Encoding.UTF8,
            "application/json");

        var query = await ProblemAsync(await client.GetAsync("/customers?country=ZZ", TestContext.Current.CancellationToken));
        var english = await ProblemAsync(await client.GetAsync("/customers?country=ZZ&culture=en-US", TestContext.Current.CancellationToken));
        var refused = await ProblemAsync(await client.PostAsync("/customers/orders", body, TestContext.Current.CancellationToken));

        query.GetProperty("errors").GetProperty("country")[0].GetString().Should().Be("« country » doit être l'une des valeurs suivantes : FR; BE; LU.");
        query.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("country").GetString().Should().Be(ValueObjectErrorCodes.NotAKnownValue);
        english.GetProperty("errors").GetProperty("country")[0].GetString().Should().Be("The value is not one of the accepted values.");
        refused.GetProperty("errors").GetProperty("$.email")[0].GetString().Should().Be("Le format de « EmailAddress » n'est pas valide.");
        refused.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("$.email").GetString().Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_minimal_API_answers_a_refused_value_in_the_language_of_the_request(bool throwOnBadRequest)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = throwOnBadRequest);
        builder.Services.AddLocalization();
        builder.Services.AddProblemDetails();
        builder.Services.AddValueObjectHttpProblemDetails();
        await using var application = builder.Build();
        application.UseExceptionHandler();
        application.UseRequestLocalization(static options => options.SetDefaultCulture("fr-FR").AddSupportedCultures("fr-FR", "en-US").AddSupportedUICultures("fr-FR", "en-US"));
        var api = application.MapGroup("/api").WithValueObjectProblemDetails(static messages => messages.UseResource<Messages>());
        api.MapGet("/accounts/{iban}", static (Iban iban) => iban.Value);
        api.MapGet("/lines", static (Quantity quantity) => quantity.ToString());
        api.MapPost("/orders", static (Line line) => line.Quantity.Value);
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var client = application.GetTestClient();

        var route = await ProblemAsync(await client.GetAsync("/api/accounts/XX", TestContext.Current.CancellationToken));
        var query = await ProblemAsync(await client.GetAsync("/api/lines?quantity=500", TestContext.Current.CancellationToken));

        route.GetProperty("errors").GetProperty("iban")[0].GetString().Should().Be("« iban » : au moins 15 caractères.");
        query.GetProperty("errors").GetProperty("quantity")[0].GetString().Should().Be("« quantity » doit être compris entre 1 et 100.");
        query.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("quantity").GetString().Should().Be(ValueObjectErrorCodes.OutOfRange);
        if (throwOnBadRequest)
        {
            var body = await ProblemAsync(await client.PostAsJsonAsync("/api/orders", new { quantity = 0 }, TestContext.Current.CancellationToken));
            body.GetProperty("errors").GetProperty("$.quantity")[0].GetString().Should().Be("« Quantity » doit être compris entre 1 et 100.");
        }
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        }
    }
}

/// <summary>
/// FluentValidation's language manager read by the rules of value objects, on the next major of .NET: global, so the
/// test runs alone.
/// </summary>
[Collection(nameof(GlobalLanguageManager))]
public sealed class FluentValidationLocalizationTests
{
    [Fact]
    public void A_refusal_takes_the_translation_of_its_code_with_the_arguments_of_the_rules()
    {
        var (original, ui, current) = (ValidatorOptions.Global.LanguageManager, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);
        try
        {
            ValidatorOptions.Global.LanguageManager = new Translations();
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var result = new ImportRequestValidator().Validate(new ImportRequest("XX", 101));

            result.Errors.Select(failure => (failure.ErrorCode, failure.ErrorMessage)).Should().Equal(
                (ValueObjectErrorCodes.TooShort, "« Iban » : au moins 15 caractères."),
                (ValueObjectErrorCodes.OutOfRange, "« Quantity » doit être compris entre 1 et 100 (Quantity)."));
        }
        finally
        {
            ValidatorOptions.Global.LanguageManager = original;
            CultureInfo.CurrentUICulture = ui;
            CultureInfo.CurrentCulture = current;
        }
    }

    /// <summary>The French translations of an application.</summary>
    private sealed class Translations : LanguageManager
    {
        public Translations()
        {
            AddTranslation("fr", "ValueObject.value_object.too_short", "« {PropertyName} » : au moins {MinLength} caractères.");
            AddTranslation("fr", "ValueObject.value_object.out_of_range", "« {PropertyName} » doit être compris entre {Minimum} et {Maximum} ({ValueObject}).");
        }
    }
}

/// <summary>Runs the tests that change FluentValidation's global language manager alone.</summary>
[CollectionDefinition(nameof(GlobalLanguageManager), DisableParallelization = true)]
public sealed class GlobalLanguageManager;
