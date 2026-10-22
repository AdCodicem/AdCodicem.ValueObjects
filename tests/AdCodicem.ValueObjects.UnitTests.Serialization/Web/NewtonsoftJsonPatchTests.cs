using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// JSON Patch documents applied through <see cref="ValueObjectJsonPatchExtensions.TryApplyTo{TModel}(JsonPatchDocument{TModel}, TModel, Action{JsonPatchError, string?})"/>:
/// over an MVC application served in memory, keyed by the operation's path with the codes in the problem details, and
/// through the handler, operation by operation, against what JSON Patch's own <c>ApplyTo</c> does.
/// </summary>
public sealed class NewtonsoftJsonPatchTests : IAsyncLifetime
{
    private const string FrenchIban = "FR7630006000011234567890189";
    private const string GermanIban = "DE89370400440532013000";
    private const string BritishIban = "GB29NWBK60161331926819";

    /// <summary>The settings MVC reads a document under once <c>AddValueObjectsNewtonsoftJson()</c> is in place.</summary>
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings().AddValueObjects();

    /// <summary>A document every value of which the rules refuse, or the patch, beside a test that fails.</summary>
    private const string Refused = """
        [{"op":"replace","path":"/lines","value":1001},
         {"op":"replace","path":"/iban","value":"XX"},
         {"op":"add","path":"/ibans/-","value":"XX"},
         {"op":"add","path":"/perCountry/XX","value":3},
         {"op":"test","path":"/country","value":"BE"},
         {"op":"remove","path":"/birth"},
         {"op":"move","from":"/iban","path":"/backup"},
         {"op":"copy","from":"/at","path":"/lines2"}]
        """;

    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _application = await StartAsync();
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    /// <summary>
    /// A refused document is answered with every error under the path of its operation, as the client sent it, and the
    /// code of each rule a value broke, the source of a <c>move</c> refused there keyed by its <c>from</c>: a key refused
    /// twice keeps both messages and its first code, and a <c>test</c> that fails has its message and no code.
    /// </summary>
    /// <param name="action">
    /// The action, which answers with the codes passed as extensions or with the response of the invalid model state
    /// factory.
    /// </param>
    [Theory]
    [InlineData("try")]
    [InlineData("try-automatic")]
    public async Task A_refused_document_is_answered_under_the_paths_of_its_operations_with_their_codes(string action)
    {
        var problem = await PatchProblemAsync(_client, action, Refused);

        problem.Codes.Should().Equal(new Dictionary<string, string>
        {
            ["/lines"] = ValueObjectErrorCodes.OutOfRange,
            ["/iban"] = ValueObjectErrorCodes.TooShort,
            ["/ibans/-"] = ValueObjectErrorCodes.TooShort,
            ["/perCountry/XX"] = ValueObjectErrorCodes.NotAKnownValue,
            ["/birth"] = ValueObjectErrorCodes.Required,
            ["/lines2"] = ValueObjectErrorCodes.NotParsable,
        });
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["/lines"] = ["The value is not a valid Quantity: The value must be less than or equal to 1000."],
            ["/iban"] =
            [
                "The value is not a valid Iban: The value must be at least 15 characters long.",
                "The value cannot be removed: a valid Iban is required.",
            ],
            ["/ibans/-"] = ["The value is not a valid Iban: The value must be at least 15 characters long."],
            ["/perCountry/XX"] = ["The key is not a valid CountryCode: The value is not one of the accepted values."],
            ["/country"] = ["The current value at path 'country' is not equal to the test value."],
            ["/birth"] = ["The value cannot be removed: a valid BirthDate is required."],
            ["/lines2"] = ["The value could not be read as Quantity."],
        });
    }

    /// <summary>
    /// An action answering with a validation problem that passes nothing goes through MVC's problem details factory,
    /// which knows no code: the codes are recorded, and the response carries none.
    /// </summary>
    [Fact]
    public async Task A_validation_problem_passing_nothing_carries_no_code()
    {
        using var body = new StringContent(Refused, Encoding.UTF8, "application/json-patch+json");
        using var response = await _client.PatchAsync("/newtonsoft/patches/try-plain-problem", body, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
        problem.GetProperty("errors").GetProperty("/lines").GetArrayLength().Should().Be(1);
    }

    /// <summary>A prefix is put before the path of each key, with no separator, since the path starts with a slash.</summary>
    [Fact]
    public async Task A_prefix_is_put_before_the_path_of_each_key()
    {
        var problem = await PatchProblemAsync(_client, "try-prefix", """[{"op":"replace","path":"/lines","value":1001}]""");

        problem.Codes.Should().Equal(new Dictionary<string, string> { ["patch/lines"] = ValueObjectErrorCodes.OutOfRange });
        problem.Errors.Keys.Should().Equal("patch/lines");
    }

    /// <summary>
    /// An accepted document is applied with the values as the client sent them, which MVC read under the package's
    /// settings: the digits of a decimal, an offset and text, normalized by their rules, and <c>copy</c> of a value object
    /// written by hand and of a <c>DateTimeOffset</c>, which JSON Patch's own copy loses.
    /// </summary>
    [Fact]
    public async Task An_accepted_document_keeps_the_values_the_client_sent()
    {
        using var body = new StringContent(
            """
            [{"op":"replace","path":"/code","value":"abc"},{"op":"replace","path":"/at","value":"2026-10-14T10:00:00+02:00"},
             {"op":"replace","path":"/total","value":1234567890123456789.12},{"op":"copy","from":"/code","path":"/code2"},
             {"op":"copy","from":"/at","path":"/at2"},{"op":"replace","path":"/iban","value":"de89 3704 0044 0532 0130 00"},
             {"op":"add","path":"/perCountry/be","value":7}]
            """,
            Encoding.UTF8,
            "application/json-patch+json");
        using var response = await _client.PatchAsync("/newtonsoft/patches/try", body, TestContext.Current.CancellationToken);
        var summary = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summary.Should().Contain(new Dictionary<string, string?>
        {
            ["code"] = "ABC",
            ["code2"] = "ABC",
            ["at"] = "2026-10-14T10:00:00.0000000+02:00",
            ["at2"] = "2026-10-14T10:00:00.0000000+02:00",
            ["total"] = "1234567890123456789.12",
            ["iban"] = GermanIban,
            ["perCountry"] = "FR=1,BE=7",
        });
    }

    /// <summary>
    /// MVC's own <c>ApplyTo</c> puts every error under the type name of the object, in JSON Patch's words, which quote the
    /// value, records no code, and removes a value object that cannot be null by setting its default instance.
    /// </summary>
    [Fact]
    public async Task MVC_s_own_ApplyTo_keys_every_error_by_the_type_quotes_the_value_and_records_no_code()
    {
        using var body = new StringContent(
            """[{"op":"replace","path":"/lines","value":1001},{"op":"add","path":"/perCountry/XX","value":3}]""",
            Encoding.UTF8,
            "application/json-patch+json");
        using var response = await _client.PatchAsync("/newtonsoft/patches/mvc", body, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        using var removal = new StringContent("""[{"op":"remove","path":"/birth"}]""", Encoding.UTF8, "application/json-patch+json");
        using var removed = await _client.PatchAsync("/newtonsoft/patches/mvc", removal, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
        problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>().Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            [nameof(PatchedAccount)] =
            [
                "The value '1001' is invalid for target location.",
                "The provided path segment 'XX' cannot be converted to the target type.",
            ],
        });
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await removed.Content.ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken))!["birth"].Should().BeNull();
    }

    /// <summary>
    /// An application allowing fewer errors than a document refuses values gets one code per error it keeps, and none
    /// beyond, so that every code has its message.
    /// </summary>
    [Fact]
    public async Task A_document_refusing_more_values_than_errors_are_allowed_gets_a_code_per_error_kept()
    {
        await using var application = await StartAsync(static options => options.MaxModelValidationErrors = 3);
        using var client = application.GetTestClient();

        var problem = await PatchProblemAsync(
            client,
            "try",
            """
            [{"op":"replace","path":"/lines","value":1001},{"op":"replace","path":"/iban","value":"XX"},
             {"op":"replace","path":"/lines2","value":1001},{"op":"replace","path":"/code","value":"1"}]
            """);

        problem.Codes.Keys.Should().Equal("/lines", "/iban");
        problem.Errors.Keys.Should().Contain(problem.Codes.Keys);
    }

    /// <summary>
    /// Every value object, read from the text the converter writes, is patched as a body reads it: replaced, added to a
    /// nullable member, a list and a dictionary, as a dictionary key, tested, copied and moved; its refused text is
    /// refused with the code the converter gives it, and removing it is refused.
    /// </summary>
    /// <param name="type">The value object.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_patched_as_a_body_reads_it(string type) => Samples.All[type].PatchesThroughItsRules();

    /// <summary>
    /// A member's value is read as the document holds it, through the rules: normalized, its digits, its offset and its
    /// text kept, a null refused where the value object cannot be null, a number with a fraction or beyond a long refused
    /// for an integer, each refusal with the converter's message, which never quotes the value, and its code.
    /// </summary>
    [Fact]
    public void A_member_s_value_is_read_through_the_rules()
    {
        Apply(Replace("/iban", "\"fr76 3000 6000 0112 3456 7890 189\"")).Account.Iban.Value.Should().Be(FrenchIban);
        Apply(Replace("/iban", "\"XX\"")).ShouldRefuse("The value is not a valid Iban: The value must be at least 15 characters long.", ValueObjectErrorCodes.TooShort);
        Apply(Replace("/iban", "null")).ShouldRefuse("Cannot convert null to 'Iban'.", ValueObjectErrorCodes.Required);
        Apply(Replace("/backup", "null")).ShouldApply().Backup.Should().BeNull();
        Apply(Replace("/backup", $"\"{GermanIban}\"")).ShouldApply().Backup.Should().Be(Iban.Create(GermanIban));
        Apply(Replace("/lines", "1001")).ShouldRefuse("The value is not a valid Quantity: The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange);
        Apply(Replace("/lines", "7.0")).ShouldRefuse("The value could not be read as Quantity.", ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/lines", "99999999999999999999")).ShouldRefuse("The value could not be read as Quantity.", ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/total", "1234567890123456789.12")).ShouldApply().Total.Value.Should().Be(1234567890123456789.12m);
        Apply(Replace("/at", "\"2026-10-14T10:00:00+02:00\"")).ShouldApply().At.Value.Offset.Should().Be(TimeSpan.FromHours(2));
        Apply(Replace("/note", "\"2026-10-14T10:00:00Z\"")).ShouldApply().Note.Value.Should().Be("2026-10-14T10:00:00Z");
        Apply(Replace("/consent", "\"true\"")).ShouldRefuse("Expected a JSON boolean for Consent but found String.", ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/customer", "\"0192f4a0-0000-7000-8000-000000000002\"")).ShouldApply().Customer.Value.Should()
            .Be(Guid.Parse("0192f4a0-0000-7000-8000-000000000002"));
        Apply(Replace("/reference", "\"PO-1042-0001-X\"")).ShouldRefuse(
            "The value is not a valid Reference`1: The value must be at most 12 characters long.",
            ValueObjectErrorCodes.TooLong);
        Apply(Replace("/reference", "\" po-9 \"")).ShouldApply().Reference.Value.Should().Be("PO-9");
    }

    /// <summary>
    /// A value object written by hand is read through its rules, with its code; one over a type the generator does not
    /// support has its value read by Newtonsoft.Json, whose refusal quotes the value and carries no code: it is refused as
    /// not parsable, nullable or not.
    /// </summary>
    [Fact]
    public void A_value_object_written_by_hand_is_read_through_its_rules()
    {
        Apply(Replace("/code", "\"xyz\"")).ShouldApply().Code.Should().Be(HandWrittenCode.Create("XYZ"));
        Apply(Replace("/code", "\"abc1\"")).ShouldRefuse("The value is not a valid HandWrittenCode: A code holds ASCII letters only.", ValueObjectErrorCodes.InvalidFormat);
        Apply(Replace("/link", "42")).ShouldRefuse("The value could not be read as HandWrittenLink.", ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/link", "{}")).ShouldRefuse("The value could not be read as HandWrittenLink.", ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/maybeLink", "42")).ShouldRefuse("The value could not be read as HandWrittenLink.", ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/link", "\"https://b.example/x\"")).ShouldApply().Link.Value.Should().Be(new Uri("https://b.example/x"));
    }

    /// <summary>
    /// A value object inside an object value is converted with the object, by JSON Patch, in its words and with no code;
    /// its own path is read through the rules. A member of another type is JSON Patch's, and so is a location under a
    /// null, or a JSON object's.
    /// </summary>
    [Fact]
    public void An_object_value_and_a_member_of_another_type_are_JSON_Patch_s()
    {
        var whole = Apply(Replace("/home", """{"reference":"x"}"""));
        whole.Applied.Should().BeFalse();
        whole.Errors.Should().ContainSingle().Which.Should().Match<(string Message, string? Code)>(error => error.Message.StartsWith("The value '{", StringComparison.Ordinal) && error.Code == null);

        Apply(Replace("/home/reference", "\"x\"")).ShouldRefuse(
            "The value is not a valid OrderReference: The value must be at least 3 characters long.",
            ValueObjectErrorCodes.TooShort);
        Apply(Replace("/nowhere/reference", "\"abc\"")).ShouldRefuse(
            "For operation 'replace', the target location specified by path '/nowhere/reference' was not found.",
            null);
        Apply(Replace("/plainCount", "\"abc\"")).ShouldRefuse("The value 'abc' is invalid for target location.", null);
        Apply(Replace("/plainCount", "7")).ShouldApply().PlainCount.Should().Be(7);
        Apply("""[{"op":"add","path":"/extra/other","value":"y"}]""").ShouldApply().Extra!["other"]!.ToString().Should().Be("y");
    }

    /// <summary>A list element is read through the rules of the list's value object, a list of anything else as JSON Patch reads it.</summary>
    [Fact]
    public void A_list_element_is_read_through_the_rules()
    {
        Apply("""[{"op":"add","path":"/ibans/-","value":"XX"}]""").ShouldRefuse(
            "The value is not a valid Iban: The value must be at least 15 characters long.",
            ValueObjectErrorCodes.TooShort);
        Apply("""[{"op":"add","path":"/ibans/-","value":null}]""").ShouldRefuse("Cannot convert null to 'Iban'.", ValueObjectErrorCodes.Required);
        Apply($$"""[{"op":"add","path":"/ibans/-","value":"{{FrenchIban}}"}]""").ShouldApply().Ibans.Should()
            .Equal(Iban.Create(GermanIban), Iban.Create(BritishIban), Iban.Create(FrenchIban));
        Apply("""[{"op":"add","path":"/previous/-","value":null}]""").ShouldApply().Previous.Should().HaveCount(3).And.EndWith((Iban?)null);
        Apply("""[{"op":"replace","path":"/ibans/0","value":"gb29 nwbk 6016 1331 9268 19"}]""").ShouldApply().Ibans[0].Should()
            .Be(Iban.Create(BritishIban));
        Apply("""[{"op":"add","path":"/notes/-","value":"b"}]""").ShouldApply().Notes.Should().Equal("a", "b");
    }

    /// <summary>
    /// A dictionary's value object value is read through its rules, and a value object key from its text as a route value
    /// is, normalized, refused with its code wherever the path names it: as the target, while traversing, in a test. A
    /// dictionary holding no value object is JSON Patch's.
    /// </summary>
    [Fact]
    public void A_dictionary_key_and_value_are_read_through_the_rules()
    {
        const string UnknownCountry = "The key is not a valid CountryCode: The value is not one of the accepted values.";

        Apply("""[{"op":"add","path":"/stock/pears","value":1001}]""").ShouldRefuse(
            "The value is not a valid Quantity: The value must be less than or equal to 1000.",
            ValueObjectErrorCodes.OutOfRange);
        Apply("""[{"op":"add","path":"/stock/pears","value":5}]""").ShouldApply().Stock["pears"].Should().Be(Quantity.Create(5));
        Apply("""[{"op":"add","path":"/optional/two","value":null}]""").ShouldApply().Optional.Should().ContainKey("two").WhoseValue.Should().BeNull();
        Apply("""[{"op":"add","path":"/perCountry/XX","value":3}]""").ShouldRefuse(UnknownCountry, ValueObjectErrorCodes.NotAKnownValue);
        Apply("""[{"op":"add","path":"/perCountry/be","value":3}]""").ShouldApply().PerCountry[CountryCode.Belgium].Should().Be(Quantity.Create(3));
        Apply("""[{"op":"add","path":"/names/lu","value":"Luxembourg"}]""").ShouldApply().Names[CountryCode.Luxembourg].Should().Be("Luxembourg");
        Apply("""[{"op":"add","path":"/names/XX","value":"x"}]""").ShouldRefuse(UnknownCountry, ValueObjectErrorCodes.NotAKnownValue);
        Apply("""[{"op":"replace","path":"/perCountry/XX/x","value":1}]""").ShouldRefuse(UnknownCountry, ValueObjectErrorCodes.NotAKnownValue);
        Apply("""[{"op":"test","path":"/perCountry/XX","value":1}]""").ShouldRefuse(UnknownCountry, ValueObjectErrorCodes.NotAKnownValue);
        Apply("""[{"op":"add","path":"/plain/b","value":2}]""").ShouldApply().Plain["b"].Should().Be(2);
        Apply("""[{"op":"add","path":"/plain/b","value":"x"}]""").ShouldRefuse("The value 'x' is invalid for target location.", null);
    }

    /// <summary>
    /// A dictionary key is read in the invariant culture whatever the current one, as a route value is: under French, a
    /// point separates the decimals of a decimal key, a comma is refused, and a date key is read in English, never in
    /// French.
    /// </summary>
    [Fact]
    public void A_dictionary_key_is_read_in_the_invariant_culture_whatever_the_current_one()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            Apply("""[{"op":"add","path":"/byLimit/12.5","value":"x"}]""").ShouldApply().ByLimit.Keys.Should().Equal(TransferLimit.Create(12.5m));
            Apply("""[{"op":"add","path":"/byLimit/12,5","value":"x"}]""").ShouldRefuse(
                "The key is not a valid TransferLimit: The text is not a valid decimal.",
                ValueObjectErrorCodes.NotParsable);
            Apply("""[{"op":"add","path":"/byBirth/1980-05-17","value":"x"}]""").ShouldApply().ByBirth.Keys.Should()
                .Equal(BirthDate.Create(new DateOnly(1980, 5, 17)));
            Apply("""[{"op":"add","path":"/byBirth/May 17, 1980","value":"x"}]""").ShouldApply().ByBirth.Keys.Should()
                .Equal(BirthDate.Create(new DateOnly(1980, 5, 17)));
            Apply("""[{"op":"add","path":"/byBirth/17 mai 1980","value":"x"}]""").ShouldRefuse(
                "The key is not a valid BirthDate: The text is not a valid System.DateOnly.",
                ValueObjectErrorCodes.NotParsable);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    /// <summary>
    /// Members and dictionary keys are found through the document's contract resolver, MVC's when MVC read it: under a
    /// snake-case resolver, by their snake-case names, a copy and a move included, and a dictionary key under a resolver
    /// that changes the keys, as the <c>test</c> of a value finds it.
    /// </summary>
    [Fact]
    public void Members_and_keys_are_found_through_the_document_s_contract_resolver()
    {
        var snakeCase = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() };
        var camelCase = new CamelCasePropertyNamesContractResolver();

        Apply("""[{"op":"add","path":"/per_country/BE","value":3}]""", snakeCase).ShouldApply().PerCountry[CountryCode.Belgium].Should()
            .Be(Quantity.Create(3));
        Apply("""[{"op":"replace","path":"/maybe_link","value":"https://b.example/x"}]""", snakeCase).ShouldApply().MaybeLink.Should()
            .Be(HandWrittenLink.Create(new Uri("https://b.example/x")));
        Apply("""[{"op":"copy","from":"/per_country/FR","path":"/lines2"}]""", snakeCase).ShouldApply().Lines2.Should().Be(Quantity.Create(1));
        var moved = Apply("""[{"op":"move","from":"/per_country/FR","path":"/stock/pears"}]""", snakeCase).ShouldApply();
        moved.PerCountry.Should().BeEmpty();
        moved.Stock["pears"].Should().Be(Quantity.Create(1));
        Apply("""[{"op":"replace","path":"/per_country/XX","value":1}]""", snakeCase).ShouldRefuseAt(
            "/per_country/XX",
            "The key is not a valid CountryCode: The value is not one of the accepted values.",
            ValueObjectErrorCodes.NotAKnownValue);
        Apply("""[{"op":"replace","path":"/maybeLink","value":"https://b.example/x"}]""", snakeCase).ShouldRefuse(
            "The target location specified by path segment 'maybeLink' was not found.",
            null);

        Apply("""[{"op":"test","path":"/stock/Apples","value":1}]""", camelCase).ShouldApply();
        Apply("""[{"op":"test","path":"/stock/Apples","value":2}]""", camelCase).ShouldRefuse("The current value at path 'Apples' is not equal to the test value.", null);
    }

    /// <summary>
    /// A value is read through the document's contract resolver too: a converter it puts on a value object's contract
    /// reads the value, as one a <c>[JsonConverter]</c> on the type names would.
    /// </summary>
    [Fact]
    public void A_value_is_read_through_the_document_s_contract_resolver()
    {
        var resolver = new QuantityAsTextResolver();

        Apply(Replace("/lines", "\"Q7\""), resolver).ShouldApply().Lines.Should().Be(Quantity.Create(7));
        Apply(Replace("/lines", "7"), resolver).ShouldRefuse("The value could not be read as Quantity.", ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// A test of a value object compares through the value object's equality: a decimal at another scale, an instant at
    /// another offset, a closed set in another case, a null against a null and a value object written by hand all match,
    /// a null against a value, either way, does not, and one that does not match names neither value and carries no
    /// code. A refused test value is refused with its code.
    /// </summary>
    [Fact]
    public void A_test_compares_through_the_value_object_s_equality()
    {
        Apply("""[{"op":"test","path":"/lines","value":4}]""").ShouldRefuse("The current value at path 'lines' is not equal to the test value.", null);
        Apply("""[{"op":"test","path":"/lines","value":1001}]""").ShouldRefuse(
            "The value is not a valid Quantity: The value must be less than or equal to 1000.",
            ValueObjectErrorCodes.OutOfRange);
        Apply("""[{"op":"test","path":"/limit","value":12.5}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/at","value":"2025-12-31T23:00:00Z"}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/status","value":"FINAL"}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/backup","value":null}]""").ShouldApply();
        Apply($$"""[{"op":"replace","path":"/backup","value":"{{GermanIban}}"},{"op":"test","path":"/backup","value":null}]""").ShouldRefuse(
            "The current value at path 'backup' is not equal to the test value.",
            null);
        Apply($$"""[{"op":"test","path":"/backup","value":"{{GermanIban}}"}]""").ShouldRefuse("The current value at path 'backup' is not equal to the test value.", null);
        Apply("""[{"op":"test","path":"/code","value":"abc"}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/lines","value":4},{"op":"replace","path":"/lines","value":9}]""").ShouldRefuse(
            "The current value at path 'lines' is not equal to the test value.",
            null).Lines.Should().Be(Quantity.Create(9), "every operation runs");
    }

    /// <summary>
    /// A test of a list element or a dictionary value compares through the value object's equality too, a null matching a
    /// null alone, and the end of a list holds no element to test, where JSON Patch throws. A list or a dictionary holding
    /// no value object, an array, a member that cannot be read, another type and a location not found are JSON Patch's.
    /// </summary>
    [Fact]
    public void A_test_of_an_element_or_an_entry_compares_through_the_value_object_s_equality()
    {
        Apply($$"""[{"op":"test","path":"/ibans/1","value":"{{GermanIban}}"}]""").ShouldRefuse("The current value at position '1' is not equal to the test value.", null);
        Apply($$"""[{"op":"test","path":"/ibans/0","value":"{{GermanIban.ToLowerInvariant()}}"}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/ibans/0","value":"XX"}]""").ShouldRefuse(
            "The value is not a valid Iban: The value must be at least 15 characters long.",
            ValueObjectErrorCodes.TooShort);
        Apply($$"""[{"op":"test","path":"/ibans/-","value":"{{BritishIban}}"}]""").ShouldRefuse(
            "The index value provided by path segment '-' is out of bounds of the array size.",
            null);
        Apply($$"""[{"op":"test","path":"/ibans/5","value":"{{BritishIban}}"}]""").ShouldRefuse(
            "The index value provided by path segment '5' is out of bounds of the array size.",
            null);
        Apply("""[{"op":"test","path":"/previous/0","value":null}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/previous/1","value":null}]""").ShouldRefuse("The current value at position '1' is not equal to the test value.", null);
        Apply($$"""[{"op":"test","path":"/previous/0","value":"{{GermanIban}}"}]""").ShouldRefuse("The current value at position '0' is not equal to the test value.", null);
        Apply("""[{"op":"test","path":"/stock/apples","value":2}]""").ShouldRefuse("The current value at path 'apples' is not equal to the test value.", null);
        Apply("""[{"op":"test","path":"/stock/apples","value":1}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/stock/apples","value":1001}]""").ShouldRefuse(
            "The value is not a valid Quantity: The value must be less than or equal to 1000.",
            ValueObjectErrorCodes.OutOfRange);
        Apply("""[{"op":"test","path":"/optional/none","value":null}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/optional/one","value":null}]""").ShouldRefuse("The current value at path 'one' is not equal to the test value.", null);
        Apply($$"""[{"op":"test","path":"/optional/none","value":"{{GermanIban}}"}]""").ShouldRefuse("The current value at path 'none' is not equal to the test value.", null);
        Apply("""[{"op":"test","path":"/stock/pears","value":1}]""").ShouldRefuse("The target location specified by path segment 'pears' was not found.", null);
        Apply("""[{"op":"test","path":"/names/FR","value":"France"}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/notes/0","value":"a"}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/scores/0","value":1}]""").Errors.Should().ContainSingle().Which.Code.Should().BeNull();
        Apply($$"""[{"op":"test","path":"/writeOnly","value":"{{GermanIban}}"}]""").ShouldRefuse("The property at 'writeOnly' could not be read.", null);
        Apply("""[{"op":"test","path":"/plainCount","value":0}]""").ShouldApply();
        Apply("""[{"op":"test","path":"/missing","value":0}]""").ShouldRefuse("The target location specified by path segment 'missing' was not found.", null);
    }

    /// <summary>
    /// The end of a list, <c>-</c>, holds no element to get, remove or replace when the list is empty, nor any to test,
    /// where JSON Patch reads the element at index -1 and throws: each is reported as an index out of bounds, in every
    /// list, and the end of a list that holds elements is its last.
    /// </summary>
    [Fact]
    public void The_end_of_a_list_is_reported_where_it_holds_no_element()
    {
        const string OutOfBounds = "The index value provided by path segment '-' is out of bounds of the array size.";

        Apply("""[{"op":"remove","path":"/noIbans/-"}]""").ShouldRefuseAt("/noIbans/-", OutOfBounds, null);
        Apply("""[{"op":"remove","path":"/noNotes/-"}]""").ShouldRefuseAt("/noNotes/-", OutOfBounds, null);
        Apply($$"""[{"op":"replace","path":"/noIbans/-","value":"{{GermanIban}}"}]""").ShouldRefuseAt("/noIbans/-", OutOfBounds, null);
        Apply("""[{"op":"copy","from":"/noIbans/-","path":"/backup"}]""").ShouldRefuseAt("/noIbans/-", OutOfBounds, null);
        Apply("""[{"op":"move","from":"/noNotes/-","path":"/notes/-"}]""").ShouldRefuseAt("/noNotes/-", OutOfBounds, null);
        Apply("""[{"op":"test","path":"/notes/-","value":"a"}]""").ShouldRefuseAt("/notes/-", OutOfBounds, null);
        Apply("""[{"op":"test","path":"/noNotes/0","value":"a"}]""").ShouldRefuseAt(
            "/noNotes/0",
            "The index value provided by path segment '0' is out of bounds of the array size.",
            null);

        Apply("""[{"op":"remove","path":"/ibans/-"}]""").ShouldApply().Ibans.Should().Equal(Iban.Create(GermanIban));
        Apply($$"""[{"op":"replace","path":"/ibans/-","value":"{{FrenchIban}}"}]""").ShouldApply().Ibans.Should()
            .Equal(Iban.Create(GermanIban), Iban.Create(FrenchIban));
        Apply("""[{"op":"copy","from":"/ibans/-","path":"/backup"}]""").ShouldApply().Backup.Should().Be(Iban.Create(BritishIban));

        var jsonPatch = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedAccount>>("""[{"op":"remove","path":"/noIbans/-"}]""", Settings)!;
        FluentActions.Invoking(() => jsonPatch.ApplyTo(PatchedAccount.Fresh(), static _ => { })).Should()
            .Throw<ArgumentOutOfRangeException>("JSON Patch's own ApplyTo reads the element at index -1");
    }

    /// <summary>
    /// A value object member that cannot be null cannot be removed, whose default JSON Patch would set, a type whose
    /// default is valid included: refused as required. A nullable member, a list element, a dictionary entry and a member
    /// of another type are removed as JSON Patch removes them, and a member it cannot write or find is its error.
    /// </summary>
    [Fact]
    public void Removing_a_value_object_that_cannot_be_null_is_refused()
    {
        Apply("""[{"op":"remove","path":"/iban"}]""").ShouldRefuse("The value cannot be removed: a valid Iban is required.", ValueObjectErrorCodes.Required);
        Apply("""[{"op":"remove","path":"/lines"}]""").ShouldRefuse("The value cannot be removed: a valid Quantity is required.", ValueObjectErrorCodes.Required);
        Apply("""[{"op":"remove","path":"/reference"}]""").ShouldRefuse("The value cannot be removed: a valid Reference`1 is required.", ValueObjectErrorCodes.Required);
        Apply($$"""[{"op":"replace","path":"/backup","value":"{{GermanIban}}"},{"op":"remove","path":"/backup"}]""").ShouldApply().Backup.Should().BeNull();
        Apply("""[{"op":"remove","path":"/ibans/0"}]""").ShouldApply().Ibans.Should().Equal(Iban.Create(BritishIban));
        Apply("""[{"op":"remove","path":"/stock/apples"}]""").ShouldApply().Stock.Should().BeEmpty();
        Apply("""[{"op":"replace","path":"/plainCount","value":5},{"op":"remove","path":"/plainCount"}]""").ShouldApply().PlainCount.Should().Be(0);
        Apply("""[{"op":"remove","path":"/readOnly"}]""").ShouldRefuse("The property at path 'readOnly' could not be updated.", null);
        Apply("""[{"op":"remove","path":"/missing"}]""").ShouldRefuse("The target location specified by path segment 'missing' was not found.", null);
    }

    /// <summary>
    /// A value object is copied as the instance it is, a <c>DateTimeOffset</c> and a value object written by hand
    /// included, which JSON Patch's own copy loses, and read through the rules of a target of another type; a default
    /// instance its type refuses is refused at the target. Anything else is copied as JSON Patch copies it.
    /// </summary>
    [Fact]
    public void Copy_takes_a_value_object_as_the_instance_it_is()
    {
        var instant = Apply("""[{"op":"copy","from":"/at","path":"/at2"}]""").ShouldApply();
        instant.At2.Should().Be(instant.At);
        Apply("""[{"op":"copy","from":"/code","path":"/code2"}]""").ShouldApply().Code2.Should().Be(HandWrittenCode.Create("ABC"));
        Apply("""[{"op":"copy","from":"/code","path":"/note"}]""").ShouldApply().Note.Should().Be(Label.Create("ABC"));
        Apply("""[{"op":"copy","from":"/iban","path":"/lines"}]""").ShouldRefuse("The value could not be read as Quantity.", ValueObjectErrorCodes.NotParsable);
        Apply("""[{"op":"copy","from":"/unset","path":"/iban"}]""").ShouldRefuse("The value is not a valid Iban: The value must not be empty.", ValueObjectErrorCodes.Required);
        Apply("""[{"op":"copy","from":"/backup","path":"/iban"}]""").ShouldRefuse("Cannot convert null to 'Iban'.", ValueObjectErrorCodes.Required);
        Apply("""[{"op":"copy","from":"/ibans/1","path":"/previous/0"}]""").ShouldApply().Previous[0].Should().Be(Iban.Create(BritishIban));

        var home = Apply("""[{"op":"copy","from":"/home","path":"/nowhere"}]""").ShouldApply();
        home.Nowhere.Should().NotBeSameAs(home.Home).And.BeEquivalentTo(home.Home);
    }

    /// <summary>
    /// A <c>copy</c> that fails at its source is keyed by its <c>from</c>, an object JSON Patch cannot copy included, and
    /// one that fails at its target by its <c>path</c>; a location under a null takes JSON Patch's message.
    /// </summary>
    [Fact]
    public void Copy_reports_a_failure_at_the_location_it_concerns()
    {
        Apply("""[{"op":"copy","from":"/missing","path":"/iban"}]""").ShouldRefuseAt("/missing", "The target location specified by path segment 'missing' was not found.", null);
        Apply("""[{"op":"copy","from":"/perCountry/XX","path":"/lines"}]""").ShouldRefuseAt(
            "/perCountry/XX",
            "The key is not a valid CountryCode: The value is not one of the accepted values.",
            ValueObjectErrorCodes.NotAKnownValue);
        Apply("""[{"op":"copy","from":"/odd","path":"/extra"}]""").ShouldRefuseAt("/odd", "The property at '/odd' could not be copied.", null);
        Apply("""[{"op":"copy","from":"/nowhere/reference","path":"/code"}]""").ShouldRefuseAt(
            "/nowhere/reference",
            "For operation 'copy', the target location specified by path '/nowhere/reference' was not found.",
            null);
        Apply("""[{"op":"copy","from":"/home/reference","path":"/nowhere/reference"}]""").ShouldRefuseAt(
            "/nowhere/reference",
            "For operation 'copy', the target location specified by path '/nowhere/reference' was not found.",
            null);
        Apply("""[{"op":"copy","from":"/iban","path":"/readOnly"}]""").ShouldRefuseAt("/readOnly", "The property at path 'readOnly' could not be updated.", null);
        Apply("""[{"op":"copy","from":"/iban","path":"/lines"}]""").ShouldRefuseAt("/lines", "The value could not be read as Quantity.", ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// A <c>move</c> from a value object that cannot be null is refused at its source, and adds nothing at its target,
    /// where JSON Patch's adds the value and leaves the default behind. A <c>move</c> refused at its target has removed
    /// its source, as JSON Patch's does; one to its own location changes nothing.
    /// </summary>
    [Fact]
    public void Move_refused_at_its_source_adds_nothing()
    {
        var refused = Apply("""[{"op":"move","from":"/iban","path":"/backup"}]""");
        refused.ShouldRefuseAt("/iban", "The value cannot be removed: a valid Iban is required.", ValueObjectErrorCodes.Required);
        refused.Account.Iban.Should().Be(Iban.Create(FrenchIban));
        refused.Account.Backup.Should().BeNull();

        var jsonPatch = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedAccount>>("""[{"op":"move","from":"/iban","path":"/backup"}]""", Settings)!;
        var account = PatchedAccount.Fresh();
        jsonPatch.ApplyTo(account);
        ((IValueObject<Iban, string>)account.Iban).IsDefault.Should().BeTrue("JSON Patch's own move leaves the default behind");
        account.Backup.Should().Be(Iban.Create(FrenchIban));

        Apply("""[{"op":"move","from":"/backup","path":"/iban"}]""").ShouldRefuseAt("/iban", "Cannot convert null to 'Iban'.", ValueObjectErrorCodes.Required);
        var toMember = Apply("""[{"op":"move","from":"/ibans/0","path":"/backup"}]""").ShouldApply();
        toMember.Ibans.Should().Equal(Iban.Create(BritishIban));
        toMember.Backup.Should().Be(Iban.Create(GermanIban));
        Apply("""[{"op":"move","from":"/ibans/0","path":"/ibans/-"}]""").ShouldApply().Ibans.Should().Equal(Iban.Create(BritishIban), Iban.Create(GermanIban));
        Apply("""[{"op":"move","from":"/ibans/0","path":"/iban"}]""").ShouldApply().Iban.Should().Be(Iban.Create(GermanIban));

        var atTarget = Apply("""[{"op":"move","from":"/ibans/0","path":"/lines"}]""");
        atTarget.ShouldRefuseAt("/lines", "The value could not be read as Quantity.", ValueObjectErrorCodes.NotParsable);
        atTarget.Account.Ibans.Should().Equal(Iban.Create(BritishIban));

        Apply("""[{"op":"move","from":"/iban","path":"/iban"}]""").ShouldApply().Iban.Should().Be(Iban.Create(FrenchIban));
        Apply("""[{"op":"move","from":"/stock/apples","path":"/stock/pears"}]""").ShouldApply().Stock.Should().Equal(
            new Dictionary<string, Quantity> { ["pears"] = Quantity.Create(1) });
        Apply("""[{"op":"move","from":"/missing","path":"/backup"}]""").ShouldRefuseAt("/missing", "The target location specified by path segment 'missing' was not found.", null);
        Apply("""[{"op":"move","from":"/readOnly","path":"/backup"}]""").ShouldRefuseAt("/readOnly", "The property at path 'readOnly' could not be updated.", null);
    }

    /// <summary>
    /// An operation naming no <c>path</c>, or a <c>copy</c> or a <c>move</c> naming no <c>from</c>, is an error, where
    /// JSON Patch throws. An operation JSON Patch cannot read stops the document, as its own <c>ApplyTo</c> does, keyed by
    /// the <c>from</c> it cannot read, or else by its <c>path</c>.
    /// </summary>
    [Fact]
    public void A_malformed_operation_is_an_error()
    {
        Apply("""[{"op":"replace","value":1}]""").ShouldRefuseAt(string.Empty, "The 'replace' operation has no 'path'.", null);
        Apply("""[{"op":"copy","path":"/lines2"}]""").ShouldRefuseAt("/lines2", "The 'copy' operation has no 'from'.", null);
        Apply("""[{"op":"move","path":"/lines2"}]""").ShouldRefuseAt("/lines2", "The 'move' operation has no 'from'.", null);
        Apply("""[{"op":"add","value":1}]""").ShouldRefuseAt(string.Empty, "The 'add' operation has no 'path'.", null);
        Apply("""[{"op":"remove"}]""").ShouldRefuseAt(string.Empty, "The 'remove' operation has no 'path'.", null);
        Apply("""[{"op":"test","value":1}]""").ShouldRefuseAt(string.Empty, "The 'test' operation has no 'path'.", null);

        var unknown = Apply("""[{"op":"frob","path":"/lines"},{"op":"replace","path":"/lines","value":9}]""");
        unknown.ShouldRefuseAt("/lines", "Invalid JsonPatch operation 'frob'.", null);
        unknown.Account.Lines.Should().Be(Quantity.Create(3), "JSON Patch stops at an operation it cannot read");
        Apply("""[{"op":"replace","path":"/q~2ty","value":1}]""").ShouldRefuseAt("/q~2ty", "The provided string '/q~2ty' is an invalid path.", null);
        var unreadableFrom = Apply("""[{"op":"move","from":"/a~2","path":"/lines"},{"op":"replace","path":"/lines","value":9}]""");
        unreadableFrom.ShouldRefuseAt("/a~2", "The provided string '/a~2' is an invalid path.", null);
        unreadableFrom.Account.Lines.Should().Be(Quantity.Create(3), "JSON Patch stops at a from it cannot read");
        Apply("""[{"op":"copy","from":"/a~2","path":"/b~2"}]""").ShouldRefuseAt("/a~2", "The provided string '/a~2' is an invalid path.", null);
        Apply("""[{"op":"copy","from":"/lines","path":"/b~2"}]""").ShouldRefuseAt("/b~2", "The provided string '/b~2' is an invalid path.", null);
        Apply("""[{"op":"frob"}]""").ShouldRefuseAt(string.Empty, "Invalid JsonPatch operation 'frob'.", null);
        Apply("""[{"op":"replace","path":"/lines","value":1001},{"op":"frob","path":"/lines2"}]""").Errors.Should().Equal(
            ("The value is not a valid Quantity: The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange),
            ("Invalid JsonPatch operation 'frob'.", null));
    }

    /// <summary>
    /// A document read under Newtonsoft.Json's defaults has lost a string that looks like a date and the digits of a
    /// decimal before any value object sees it: an offset and date-like text are refused as not parsable, a UTC instant
    /// is read, and a decimal keeps a double's digits.
    /// </summary>
    [Fact]
    public void A_document_read_under_Newtonsoft_s_defaults_has_lost_dates_and_digits()
    {
        var defaults = new JsonSerializerSettings();
        const string DateLost = "Newtonsoft.Json read the string as a date before the converter saw it, and {0} cannot be read back from that date. Set DateParseHandling to None in the serializer settings.";

        Apply(Replace("/at", "\"2026-10-14T10:00:00+02:00\""), defaults).ShouldRefuse(string.Format(System.Globalization.CultureInfo.InvariantCulture, DateLost, "OccurredAt"), ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/at", "\"2026-10-14T08:00:00Z\""), defaults).ShouldApply().At.Value.Should().Be(new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero));
        Apply(Replace("/note", "\"2026-10-14T10:00:00Z\""), defaults).ShouldRefuse(string.Format(System.Globalization.CultureInfo.InvariantCulture, DateLost, "Label"), ValueObjectErrorCodes.NotParsable);
        Apply(Replace("/total", "1234567890123456789.12"), defaults).ShouldApply().Total.Value.Should().Be(1234567890123456800m);
    }

    /// <summary>
    /// A document built in code holds the value objects themselves, kept as they are, but for a default instance its type
    /// refuses, refused as a writer refuses it; every other operation is applied.
    /// </summary>
    [Fact]
    public void A_document_built_in_code_keeps_its_instances_but_a_refused_default()
    {
        var document = new JsonPatchDocument<PatchedAccount>()
            .Replace(account => account.Lines, Quantity.Create(9))
#pragma warning disable VO0010 // The uninitialized instance is what the patch refuses.
            .Replace(account => account.Iban, default)
#pragma warning restore VO0010
            .Replace(account => account.Backup, Iban.Create(GermanIban));
        var account = PatchedAccount.Fresh();
        var errors = new List<(string Message, string? Code)>();

        var applied = document.TryApplyTo(account, (error, code) => errors.Add((error.ErrorMessage, code)));

        applied.Should().BeFalse();
        errors.Should().Equal(("The value is not a valid Iban: The value must not be empty.", ValueObjectErrorCodes.Required));
        account.Lines.Should().Be(Quantity.Create(9));
        account.Iban.Should().Be(Iban.Create(FrenchIban));
        account.Backup.Should().Be(Iban.Create(GermanIban));
    }

    /// <summary>An empty document applies, and each argument is required.</summary>
    [Fact]
    public void An_empty_document_applies_and_each_argument_is_required()
    {
        var document = new JsonPatchDocument<PatchedAccount>();
        var account = PatchedAccount.Fresh();
        var modelState = new ModelStateDictionary();
        var httpContext = new DefaultHttpContext();
        Action<JsonPatchError, string?> log = static (_, _) => { };

        document.TryApplyTo(account, log).Should().BeTrue();
        document.TryApplyTo(account, modelState, httpContext).Should().BeTrue();
        modelState.IsValid.Should().BeTrue();

        FluentActions.Invoking(() => ((JsonPatchDocument<PatchedAccount>)null!).TryApplyTo(account, log)).Should().Throw<ArgumentNullException>().WithParameterName("patchDocument");
        FluentActions.Invoking(() => document.TryApplyTo(null!, log)).Should().Throw<ArgumentNullException>().WithParameterName("objectToApplyTo");
        FluentActions.Invoking(() => document.TryApplyTo(account, null!)).Should().Throw<ArgumentNullException>().WithParameterName("logError");
        FluentActions.Invoking(() => ((JsonPatchDocument<PatchedAccount>)null!).TryApplyTo(account, modelState, httpContext)).Should().Throw<ArgumentNullException>().WithParameterName("patchDocument");
        FluentActions.Invoking(() => document.TryApplyTo(null!, modelState, httpContext)).Should().Throw<ArgumentNullException>().WithParameterName("objectToApplyTo");
        FluentActions.Invoking(() => document.TryApplyTo(account, null!, httpContext)).Should().Throw<ArgumentNullException>().WithParameterName("modelState");
        FluentActions.Invoking(() => document.TryApplyTo(account, modelState, null!)).Should().Throw<ArgumentNullException>().WithParameterName("httpContext");
    }

    /// <summary>Writes a document of one <c>replace</c>.</summary>
    private static string Replace(string path, string value) => $$"""[{"op":"replace","path":"{{path}}","value":{{value}}}]""";

    /// <summary>Reads a document under the settings MVC reads it under, or others, and applies it to a fresh account through the handler.</summary>
    private static Outcome Apply(string json, JsonSerializerSettings? settings = null)
    {
        var document = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedAccount>>(json, settings ?? Settings)!;

        return Apply(document);
    }

    /// <summary>
    /// Reads a document under the settings MVC reads it under, with a contract resolver, which MVC hands the document as
    /// its own, and applies it to a fresh account through the handler.
    /// </summary>
    private static Outcome Apply(string json, IContractResolver resolver)
    {
        var settings = new JsonSerializerSettings { ContractResolver = resolver }.AddValueObjects();
        var document = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedAccount>>(json, settings)!;
        document.ContractResolver = resolver;

        return Apply(document);
    }

    /// <summary>Applies a document to a fresh account through the handler, and to another through the model state.</summary>
    private static Outcome Apply(JsonPatchDocument<PatchedAccount> document)
    {
        var account = PatchedAccount.Fresh();
        var errors = new List<(string Message, string? Code)>();
        var keyed = new List<string>();
        var modelState = new ModelStateDictionary();
        var httpContext = new DefaultHttpContext();

        var applied = document.TryApplyTo(account, (error, code) => errors.Add((error.ErrorMessage, code)));
        document.TryApplyTo(PatchedAccount.Fresh(), modelState, httpContext).Should().Be(applied, "both overloads apply alike");

        return new Outcome(applied, account, errors, modelState, ValueObjectProblemDetails.GetErrorCodes(httpContext));
    }

    private static async Task<WebApplication> StartAsync(Action<MvcOptions>? mvc = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddControllers(options => mvc?.Invoke(options))
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new ControllerPart(typeof(NewtonsoftPatchController)));
            })
            .AddNewtonsoftJson()
            .AddValueObjectsNewtonsoftJson();
        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

        var application = builder.Build();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private static async Task<ReadProblem> PatchProblemAsync(HttpClient client, string action, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json-patch+json");
        using var response = await client.PatchAsync($"/newtonsoft/patches/{action}", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return new ReadProblem(
            problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!,
            problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>()!);
    }

    private sealed record ReadProblem(Dictionary<string, string[]> Errors, Dictionary<string, string> Codes);

    /// <summary>What applying a document through both overloads did.</summary>
    /// <param name="Applied">Whether every operation applied.</param>
    /// <param name="Account">The account patched through the handler.</param>
    /// <param name="Errors">The message and the code of each error the handler was given.</param>
    /// <param name="ModelState">The model state of another account patched through the other overload.</param>
    /// <param name="Codes">The codes that overload recorded.</param>
    private sealed record Outcome(
        bool Applied,
        PatchedAccount Account,
        List<(string Message, string? Code)> Errors,
        ModelStateDictionary ModelState,
        IReadOnlyDictionary<string, string> Codes)
    {
        public PatchedAccount ShouldApply()
        {
            Errors.Should().BeEmpty();
            Applied.Should().BeTrue();
            ModelState.IsValid.Should().BeTrue();
            return Account;
        }

        public PatchedAccount ShouldRefuse(string message, string? code)
        {
            Applied.Should().BeFalse();
            Errors.Should().Equal((message, code));
            return Account;
        }

        public PatchedAccount ShouldRefuseAt(string key, string message, string? code)
        {
            ShouldRefuse(message, code);
            ModelState.Keys.Should().Equal(key);
            ModelState[key]!.Errors.Select(static error => error.ErrorMessage).Should().Equal(message);
            if (code is null)
            {
                Codes.Should().BeEmpty();
            }
            else
            {
                Codes.Should().Equal(new Dictionary<string, string> { [key] = code });
            }

            return Account;
        }
    }

    /// <summary>A resolver putting a converter of the application's own on the contract of <see cref="Quantity"/>.</summary>
    private sealed class QuantityAsTextResolver : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            var contract = base.CreateContract(objectType);
            if (objectType == typeof(Quantity))
            {
                contract.Converter = new QuantityAsText();
            }

            return contract;
        }
    }

    /// <summary>Reads a quantity from text with a prefix, <c>Q7</c>, and writes it so.</summary>
    private sealed class QuantityAsText : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Quantity);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
            => reader.Value is string text && text.StartsWith('Q')
                ? Quantity.Create(short.Parse(text[1..], CultureInfo.InvariantCulture))
                : throw new JsonSerializationException("A quantity is written Q followed by its number.");

        public override void WriteJson(JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
            => writer.WriteValue($"Q{((Quantity)value!).Value.ToString(CultureInfo.InvariantCulture)}");
    }

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
