using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>Settings keeping the text of value objects, which the attributes hold to their rules.</summary>
public sealed class TransferSettings
{
    [Required]
    [ParsesAs(typeof(Iban))]
    public string? Account { get; set; }

    [ParsesAs<Quantity, int>]
    public string? MaxItems { get; set; }

    [NotDefault]
    public Iban Fallback { get; set; }
}

/// <summary>The validator <c>[OptionsValidator]</c> writes in this SDK's compiler.</summary>
[OptionsValidator]
public sealed partial class TransferSettingsValidator : IValidateOptions<TransferSettings>;

/// <summary>A transfer as a record, the attributes on its constructor's parameters, where MVC requires them.</summary>
/// <param name="Iban">An account.</param>
/// <param name="Quantity">A quantity.</param>
/// <param name="Localized">A quantity whose message is read from the resource by the code of its rule.</param>
public sealed record TextTransfer(
    [ParsesAs(typeof(Iban))] string? Iban,
    [ParsesAs<Quantity, int>] string? Quantity,
    [ParsesAs(typeof(Quantity), ErrorMessageResourceType = typeof(Messages))] string? Localized);

/// <summary>A body whose member holds no text, a misuse of the attribute.</summary>
public sealed class MisusedTransfer
{
#pragma warning disable VO0039 // The misuse .NET 11 lets out of the request.
    [ParsesAs(typeof(Iban))]
    public object? Raw { get; set; }
#pragma warning restore VO0039
}

/// <summary>An MVC controller taking the record.</summary>
[ApiController]
[Route("annotations")]
public sealed class TextTransfersController : ControllerBase
{
    /// <summary>Accepts a transfer.</summary>
    /// <param name="transfer">The transfer.</param>
    /// <returns>No content.</returns>
    [HttpPost]
    public IActionResult Post(TextTransfer transfer) => NoContent();
}

/// <summary>
/// The DataAnnotations attributes on the next major of .NET: <c>Validator</c>, options validated as the host starts and
/// by the validator <c>[OptionsValidator]</c> writes, MVC, and the minimal API validation of
/// Microsoft.Extensions.Validation 11, its own message keys and its handling of an attribute's exception included.
/// </summary>
public sealed class DataAnnotationsTests
{
    private const string Account = "FR7630006000011234567890189";

    [Fact]
    public void Validator_reports_each_refusal_with_its_code()
    {
        var settings = new TransferSettings { Account = "XX", MaxItems = "500" };
        var results = new List<AnnotationResult>();

        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true).Should().BeFalse();

        results.Cast<ValueObjectValidationResult>().Select(static result => (result.MemberNames.Single(), result.ErrorCode)).Should().Equal(
            (nameof(TransferSettings.Account), ValueObjectErrorCodes.TooShort),
            (nameof(TransferSettings.MaxItems), ValueObjectErrorCodes.OutOfRange),
            (nameof(TransferSettings.Fallback), ValueObjectErrorCodes.Required));
        var thrown = FluentActions.Invoking(() => Validator.ValidateObject(settings, new ValidationContext(settings), validateAllProperties: true))
            .Should().Throw<ValidationException>().Which;
        ValueObjectErrors.TryGetCode(thrown, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
        Validator.TryValidateValue("PO-1042", new ValidationContext(settings), null, [new ParsesAsAttribute(typeof(Reference<PurchaseOrder>))]).Should().BeTrue();
    }

    [Fact]
    public async Task Options_holding_text_are_held_to_the_rules_as_the_host_starts_and_by_the_generated_validator()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Transfer:Account"] = "XX", ["Transfer:MaxItems"] = "500" });
        builder.Services.AddOptions<TransferSettings>().BindConfiguration("Transfer").ValidateDataAnnotations().ValidateOnStart();
        using var host = builder.Build();

        var start = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await start.Should().ThrowAsync<OptionsValidationException>()).Which.Failures.Should().HaveCount(3);

        // The generator of .NET 11 hands the attribute the member's name alone, where .NET 10's hands it
        // "TransferSettings.Fallback".
        new TransferSettingsValidator().Validate(null, new TransferSettings { Account = "XX", MaxItems = "500" }).Failures.Should().Equal(
            "Account: The value must be at least 15 characters long.",
            "MaxItems: The value must be less than or equal to 100.",
            "Fallback: The Fallback field is required.");
        new TransferSettingsValidator().Validate(null, new TransferSettings { Account = Account, Fallback = Iban.Create(Account) }).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task MVC_refuses_a_record_member_by_member_and_keeps_a_message_read_by_code()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddLocalization();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new ControllerPart(typeof(TextTransfersController)));
            })
            .AddDataAnnotationsLocalization();
        await using var application = builder.Build();
        application.UseRequestLocalization(static options => options.SetDefaultCulture("fr-FR").AddSupportedCultures("fr-FR").AddSupportedUICultures("fr-FR"));
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var client = application.GetTestClient();

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("/annotations", new { iban = "XX", quantity = "500", localized = "500" }, TestContext.Current.CancellationToken));

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 100."],
            ["Localized"] = ["« Localized » doit être compris entre 1 et 100."],
        });
    }

    /// <summary>
    /// .NET 11's validation looks the message of an attribute naming no resource up in its localizer, under
    /// <c>{Attribute}_Error</c> among others: <c>ParsesAsAttribute_Error</c> gives one template to every rule of the
    /// <see cref="Type"/> form, the generic form's key being <c>ParsesAsAttribute`2_Error</c>; an attribute naming a
    /// resource type alone keeps the message read by the code of the rule.
    /// </summary>
    [Fact]
    public async Task A_minimal_API_on_the_next_major_reads_its_own_keys_and_steps_aside_for_a_resource()
    {
        await using var application = await MinimalApiAsync();
        using var client = application.GetTestClient();

        var french = await ErrorsAsync(await client.PostAsJsonAsync("/transfers?culture=fr-FR", new { iban = "XX", quantity = "500", localized = "500" }, TestContext.Current.CancellationToken));
        var english = await ErrorsAsync(await client.PostAsJsonAsync("/transfers?culture=en-US", new { iban = "XX", quantity = "500", localized = "500" }, TestContext.Current.CancellationToken));

        french.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["« Iban » est refusé."],
            ["Quantity"] = ["The value must be less than or equal to 100."],
            ["Localized"] = ["« Localized » doit être compris entre 1 et 100."],
        });
        english.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Iban"] = ["The value must be at least 15 characters long."],
            ["Quantity"] = ["The value must be less than or equal to 100."],
            ["Localized"] = ["The value must be less than or equal to 100."],
        });
    }

    /// <summary>.NET 11's validation lets an attribute's exception out of the request, where .NET 10 answers a 400.</summary>
    [Fact]
    public async Task A_minimal_API_on_the_next_major_lets_a_misused_attributes_exception_out()
    {
        await using var application = await MinimalApiAsync();
        using var client = application.GetTestClient();

        var send = () => client.PostAsJsonAsync("/misused", new { raw = "XX" }, TestContext.Current.CancellationToken);

        (await send.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().StartWith("[ParsesAs] holds text to the rules of 'Iban'");
    }

    private static async Task<WebApplication> MinimalApiAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddLocalization();
        builder.Services.AddValidation(static options => options.LocalizerProvider = static (_, factory) => factory.Create(typeof(Messages)));
        var application = builder.Build();
        application.UseRequestLocalization(static options => options.SetDefaultCulture("fr-FR").AddSupportedCultures("fr-FR", "en-US").AddSupportedUICultures("fr-FR", "en-US"));
        application.MapPost("/transfers", static (TextTransfer transfer) => Results.NoContent());
        application.MapPost("/misused", static (MisusedTransfer transfer) => Results.NoContent());
        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
        }
    }

    private sealed class ControllerPart(Type controller) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => controller.Name;

        public IEnumerable<TypeInfo> Types => [controller.GetTypeInfo()];
    }
}
