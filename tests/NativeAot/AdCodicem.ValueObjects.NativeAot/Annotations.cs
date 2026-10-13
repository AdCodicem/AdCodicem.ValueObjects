using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.Annotations;
using Microsoft.Extensions.Options;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>Settings holding the text of value objects, which the attributes hold to their rules.</summary>
internal sealed class AnnotatedSettings
{
    [ParsesAs(typeof(EmailAddress))]
    public string? Contact { get; set; }

    [ParsesAs<Quantity, short>]
    public string? MaxItems { get; set; }

    // A construction of a generic value object, which native AOT finds only because Program.cs registered it.
    [ParsesAs(typeof(DocumentNumber<PurchaseOrder>))]
    public string? Number { get; set; }

    [NotDefault]
    public CustomerId Customer { get; set; }
}

/// <summary>The validator the options generator writes for <see cref="AnnotatedSettings"/>, which creates the attributes itself.</summary>
[OptionsValidator]
internal sealed partial class AnnotatedSettingsValidator : IValidateOptions<AnnotatedSettings>;

/// <summary>
/// A body holding the text of value objects and a value object that must be set: public, since the validations generator
/// of .NET 10 describes no internal type, whose body is then never validated.
/// </summary>
public sealed class AnnotatedTransfer
{
    [ParsesAs(typeof(EmailAddress))]
    public string? Contact { get; set; }

    [ParsesAs<Quantity, short>]
    public string? Count { get; set; }

    [NotDefault]
    public CustomerId Customer { get; set; }
}

/// <summary>
/// The DataAnnotations attributes, run through <see cref="Validator"/>, through the validator <c>[OptionsValidator]</c>
/// writes, and, by <see cref="RunAsync"/>, through a minimal API under <c>AddValidation()</c>: both forms of
/// <see cref="ParsesAsAttribute"/> over value objects registered by their assembly and by Program.cs, and
/// <see cref="NotDefaultAttribute"/>, whose check of the default instance is closed through the descriptor's visitor.
/// </summary>
internal static class Annotations
{
    private const string Customer = "0f8fad5b-d9cb-469f-a165-70867728950e";

    public static void Run(Report report)
    {
        (string Subject, ValidationAttribute Attribute, object? Value, string? Code)[] checks =
        [
            ("ParsesAs(typeof(EmailAddress))", new ParsesAsAttribute(typeof(EmailAddress)), " Ada@Example.com ", null),
            ("ParsesAs(typeof(EmailAddress))", new ParsesAsAttribute(typeof(EmailAddress)), "ada", ValueObjectErrorCodes.InvalidFormat),
            ("ParsesAs(typeof(DocumentStatus))", new ParsesAsAttribute(typeof(DocumentStatus)), "archived", ValueObjectErrorCodes.NotAKnownValue),
            ("ParsesAs(typeof(DocumentNumber<PurchaseOrder>))", new ParsesAsAttribute(typeof(DocumentNumber<PurchaseOrder>)), "PO-1042-0001-X", ValueObjectErrorCodes.TooLong),
            ("ParsesAs(typeof(HandWrittenCode))", new ParsesAsAttribute(typeof(HandWrittenCode)), "123", ValueObjectErrorCodes.InvalidFormat),
            ("ParsesAs<UnregisteredCode, string>", new ParsesAsAttribute<UnregisteredCode, string>(), string.Empty, ValueObjectErrorCodes.Required),
            ("ParsesAs<UnregisteredCode, string>", new ParsesAsAttribute<UnregisteredCode, string>(), "any", null),
            ("ParsesAs<Quantity, short> elements", new ParsesAsAttribute<Quantity, short>(), new[] { "1", null, "1001" }, ValueObjectErrorCodes.OutOfRange),
            ("ParsesAs<Quantity, short> resource", new ParsesAsAttribute<Quantity, short> { ErrorMessageResourceType = typeof(ProblemMessages) }, "0", ValueObjectErrorCodes.OutOfRange),
            ("ParsesAs(typeof(VatRate)) resource", new ParsesAsAttribute(typeof(VatRate)) { ErrorMessageResourceType = typeof(ProblemMessages) }, "7", ValueObjectErrorCodes.NotAKnownValue),
#pragma warning disable VO0010 // The uninitialized instances are what the attribute refuses.
            ("NotDefault CustomerId", new NotDefaultAttribute(), default(CustomerId), ValueObjectErrorCodes.Required),
            ("NotDefault HandWrittenCode", new NotDefaultAttribute(), default(HandWrittenCode), ValueObjectErrorCodes.Required),
            ("NotDefault resource", new NotDefaultAttribute { ErrorMessageResourceType = typeof(ProblemMessages) }, default(Quantity), ValueObjectErrorCodes.Required),
#pragma warning restore VO0010
            ("NotDefault CustomerId", new NotDefaultAttribute(), CustomerId.Parse(Customer, null), null),
        ];

        foreach (var (subject, attribute, value, code) in checks)
        {
            Check(report, subject, attribute, value, code);
        }

        // A value object nothing registered, which native AOT cannot describe, is refused the same way in both runs.
        Misuse(report, "ParsesAs(typeof(UnregisteredCode))", new ParsesAsAttribute(typeof(UnregisteredCode)), "any");
#pragma warning disable VO0010 // The uninitialized instance is what the attribute checks.
        Misuse(report, "NotDefault UnregisteredCode", new NotDefaultAttribute(), default(UnregisteredCode));
#pragma warning restore VO0010

        var validator = new AnnotatedSettingsValidator();
        var refused = validator.Validate(null, new AnnotatedSettings { Contact = "ada", MaxItems = "0", Number = "PO-1042-0001-X" });
        var accepted = validator.Validate(null, new AnnotatedSettings { Contact = "ada@example.com", MaxItems = "7", Number = "po-1042", Customer = CustomerId.Parse(Customer, null) });
        report.Line("OptionsValidator refused", string.Join("; ", refused.Failures ?? []));
        report.Line("OptionsValidator accepted", $"succeeded {accepted.Succeeded}");
        if (refused.Failures?.Count() != 4 || !accepted.Succeeded)
        {
            report.Fail("OptionsValidator", $"{refused.Failures?.Count()} failures, accepted {accepted.Succeeded}");
        }
    }

    /// <summary>Maps the endpoints of an application under <c>AddValidation()</c>.</summary>
    /// <param name="app">The application.</param>
    public static void Map(WebApplication app)
    {
        app.MapPost("/annotations/transfers", (AnnotatedTransfer transfer) => transfer.Customer.ToString());
        app.MapGet("/annotations/contacts", ([ParsesAs(typeof(EmailAddress))] string? contact, [ParsesAs<Quantity, short>] string? count) => contact ?? "(none)");
    }

    /// <summary>
    /// Sends the requests the minimal API's validation answers, and fails each one whose status, or whose members, are not
    /// those expected.
    /// </summary>
    /// <param name="report">Where each answer is written.</param>
    /// <param name="client">A client of the application.</param>
    public static async Task RunAsync(Report report, HttpClient client)
    {
        (HttpMethod Method, string Path, string? Body, string Expected)[] requests =
        [
            (HttpMethod.Post, "/annotations/transfers", $$"""{"Contact":"ada@example.com","Count":"7","Customer":"{{Customer}}"}""", "200"),
            (HttpMethod.Post, "/annotations/transfers", """{"Contact":"ada","Count":"0"}""", "400 Contact, Count, Customer"),
            (HttpMethod.Get, "/annotations/contacts?contact=ada%40example.com&count=7", null, "200"),
            (HttpMethod.Get, "/annotations/contacts?contact=ada&count=1001", null, "400 contact, count"),
        ];

        foreach (var (method, path, body, expected) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            report.Line($"AddValidation {method} {path} {body}", $"{(int)response.StatusCode} {text}");

            var members = response.StatusCode == HttpStatusCode.BadRequest
                ? " " + string.Join(", ", JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject().Select(static member => member.Name).Order(StringComparer.Ordinal))
                : string.Empty;
            if ($"{(int)response.StatusCode}{members}" != expected)
            {
                report.Fail($"AddValidation {method} {path}", $"expected {expected}, answered {(int)response.StatusCode}{members}");
            }
        }
    }

    private static void Check(Report report, string subject, ValidationAttribute attribute, object? value, string? code)
    {
        var results = new List<AnnotationResult>();
        var context = new ValidationContext(new object(), "Member", null, null) { MemberName = "Member" };
        Validator.TryValidateValue(value!, context, results, [attribute]);

        var refusal = results.OfType<ValueObjectValidationResult>().SingleOrDefault();
        report.Line(
            $"DataAnnotations {subject} {Underlying.Quote(Show(value))}",
            refusal is null ? $"accepted ({results.Count} results)" : $"{refusal.ErrorCode} \"{refusal.ErrorMessage}\" ({Names.Of(refusal.ValueObjectType)})");

        if (refusal?.ErrorCode != code || results.Count != (code is null ? 0 : 1))
        {
            report.Fail($"DataAnnotations {subject}", $"expected {code ?? "acceptance"}, reported {refusal?.ErrorCode ?? $"{results.Count} results"}");
        }
    }

    private static void Misuse(Report report, string subject, ValidationAttribute attribute, object? value)
    {
        try
        {
            attribute.GetValidationResult(value, new ValidationContext(new object(), "Member", null, null));
            report.Fail($"DataAnnotations {subject}", "validated a value object nothing registered");
        }
        catch (InvalidOperationException exception)
        {
            report.Line($"DataAnnotations {subject}", $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Show(object? value)
        => value switch
        {
            null => "(null)",
            string text => text,
            IEnumerable<string?> texts => string.Join(",", texts.Select(static text => text ?? "(null)")),
            _ => value.ToString() ?? string.Empty,
        };
}
