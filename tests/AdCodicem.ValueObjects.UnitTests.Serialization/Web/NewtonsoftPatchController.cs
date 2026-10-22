using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Actions applying JSON Patch documents MVC reads with Newtonsoft.Json to a fresh account, for the in-memory MVC
/// applications of <see cref="NewtonsoftJsonPatchTests"/>.
/// </summary>
[ApiController]
[Route("newtonsoft/patches")]
public sealed class NewtonsoftPatchController : ControllerBase
{
    /// <summary>Applies a document through the rules, answering a refusal with the codes passed as extensions.</summary>
    /// <param name="patch">The document.</param>
    /// <returns>The account patched, or the validation problem.</returns>
    [HttpPatch("try")]
    public IActionResult Try([FromBody] JsonPatchDocument<PatchedAccount> patch)
    {
        var account = PatchedAccount.Fresh();

        return patch.TryApplyTo(account, ModelState, HttpContext)
            ? Ok(account.Summary())
            : ValidationProblem(extensions: new Dictionary<string, object?>
            {
                [ValueObjectProblemDetails.ExtensionName] = ValueObjectProblemDetails.GetErrorCodes(HttpContext),
            });
    }

    /// <summary>
    /// Applies a document through the rules, answering a refusal with the response the invalid model state factory
    /// builds, as the automatic 400 does.
    /// </summary>
    /// <param name="patch">The document.</param>
    /// <returns>The account patched, or the response of the invalid model state factory.</returns>
    [HttpPatch("try-automatic")]
    public IActionResult TryAutomatic([FromBody] JsonPatchDocument<PatchedAccount> patch)
    {
        var account = PatchedAccount.Fresh();

        return patch.TryApplyTo(account, ModelState, HttpContext)
            ? Ok(account.Summary())
            : HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory(ControllerContext);
    }

    /// <summary>Applies a document through the rules, answering a refusal with a validation problem passing nothing.</summary>
    /// <param name="patch">The document.</param>
    /// <returns>The account patched, or the validation problem.</returns>
    [HttpPatch("try-plain-problem")]
    public IActionResult TryPlainProblem([FromBody] JsonPatchDocument<PatchedAccount> patch)
    {
        var account = PatchedAccount.Fresh();

        return patch.TryApplyTo(account, ModelState, HttpContext) ? Ok(account.Summary()) : ValidationProblem();
    }

    /// <summary>Applies a document through the rules, keying each error after a prefix.</summary>
    /// <param name="patch">The document.</param>
    /// <returns>The account patched, or the response of the invalid model state factory.</returns>
    [HttpPatch("try-prefix")]
    public IActionResult TryPrefix([FromBody] JsonPatchDocument<PatchedAccount> patch)
    {
        var account = PatchedAccount.Fresh();

        return patch.TryApplyTo(account, ModelState, HttpContext, "patch")
            ? Ok(account.Summary())
            : HttpContext.RequestServices.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory(ControllerContext);
    }

    /// <summary>Applies a document through MVC's own <c>ApplyTo</c>, as an application does without the package.</summary>
    /// <param name="patch">The document.</param>
    /// <returns>The account patched, or the validation problem.</returns>
    [HttpPatch("mvc")]
    public IActionResult Mvc([FromBody] JsonPatchDocument<PatchedAccount> patch)
    {
        var account = PatchedAccount.Fresh();
        patch.ApplyTo(account, ModelState);

        return ModelState.IsValid ? Ok(account.Summary()) : ValidationProblem();
    }
}

/// <summary>An account holding value objects in every shape a JSON Patch document reaches, and members of other types.</summary>
public sealed class PatchedAccount
{
    /// <summary>Gets or sets the account's IBAN, normalized and checked by a pattern.</summary>
    public Iban Iban { get; set; }

    /// <summary>Gets or sets an IBAN that may be missing.</summary>
    public Iban? Backup { get; set; }

    /// <summary>Gets or sets an IBAN left at its default instance, which its type refuses.</summary>
    public Iban Unset { get; set; }

    /// <summary>Gets or sets a number of lines, bounded, whose default is valid.</summary>
    public Quantity Lines { get; set; }

    /// <summary>Gets or sets another number of lines.</summary>
    public Quantity Lines2 { get; set; }

    /// <summary>Gets or sets an amount, rounded to the cent.</summary>
    public Amount Total { get; set; }

    /// <summary>Gets or sets a limit, a decimal kept with its scale.</summary>
    public TransferLimit Limit { get; set; }

    /// <summary>Gets or sets a country, from a closed set.</summary>
    public CountryCode Country { get; set; }

    /// <summary>Gets or sets a status, from a closed set compared ignoring case.</summary>
    public DocumentStatus Status { get; set; }

    /// <summary>Gets or sets a date of birth.</summary>
    public BirthDate Birth { get; set; }

    /// <summary>Gets or sets when something happened, with its offset.</summary>
    public OccurredAt At { get; set; }

    /// <summary>Gets or sets when something else happened.</summary>
    public OccurredAt At2 { get; set; }

    /// <summary>Gets or sets whether the customer agreed.</summary>
    public Consent Consent { get; set; }

    /// <summary>Gets or sets the customer.</summary>
    public CustomerId Customer { get; set; }

    /// <summary>Gets or sets a reference, a construction of a generic value object.</summary>
    public Reference<PatchedAccount> Reference { get; set; }

    /// <summary>Gets or sets a code, a value object written by hand.</summary>
    public HandWrittenCode Code { get; set; }

    /// <summary>Gets or sets another code.</summary>
    public HandWrittenCode Code2 { get; set; }

    /// <summary>Gets or sets a link, written by hand over a type Newtonsoft.Json reads itself.</summary>
    public HandWrittenLink Link { get; set; }

    /// <summary>Gets or sets a link that may be missing.</summary>
    public HandWrittenLink? MaybeLink { get; set; }

    /// <summary>Gets or sets a note, free text.</summary>
    public Label Note { get; set; }

    /// <summary>Gets or sets other IBANs.</summary>
    public List<Iban> Ibans { get; set; } = [];

    /// <summary>Gets or sets former IBANs, some missing.</summary>
    public List<Iban?> Previous { get; set; } = [];

    /// <summary>Gets or sets notes, a list holding no value object.</summary>
    public List<string> Notes { get; set; } = [];

    /// <summary>Gets or sets IBANs, none so far.</summary>
    public List<Iban> NoIbans { get; set; } = [];

    /// <summary>Gets or sets notes, none so far.</summary>
    public List<string> NoNotes { get; set; } = [];

    /// <summary>Gets or sets scores, an array, which JSON Patch does not patch.</summary>
    public int[] Scores { get; set; } = [];

    /// <summary>Gets or sets quantities by name.</summary>
    public Dictionary<string, Quantity> Stock { get; set; } = [];

    /// <summary>Gets or sets IBANs by name, some missing.</summary>
    public Dictionary<string, Iban?> Optional { get; set; } = [];

    /// <summary>Gets or sets quantities by country.</summary>
    public Dictionary<CountryCode, Quantity> PerCountry { get; set; } = [];

    /// <summary>Gets or sets names by country.</summary>
    public Dictionary<CountryCode, string> Names { get; set; } = [];

    /// <summary>Gets or sets numbers by name, a dictionary holding no value object.</summary>
    public Dictionary<string, int> Plain { get; set; } = [];

    /// <summary>Gets or sets notes by limit, a dictionary keyed by a decimal value object.</summary>
    public Dictionary<TransferLimit, string> ByLimit { get; set; } = [];

    /// <summary>Gets or sets notes by date of birth, a dictionary keyed by a date value object.</summary>
    public Dictionary<BirthDate, string> ByBirth { get; set; } = [];

    /// <summary>Gets or sets the home address.</summary>
    public PatchedAddress? Home { get; set; }

    /// <summary>Gets or sets an address left missing.</summary>
    public PatchedAddress? Nowhere { get; set; }

    /// <summary>Gets an IBAN that cannot be written.</summary>
    public Iban ReadOnly { get; } = Iban.Create("DE89370400440532013000");

    /// <summary>Sets an IBAN that cannot be read.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "A member JSON Patch cannot read.")]
    public Iban WriteOnly
    {
        set => Code2 = HandWrittenCode.Create(value.CountryCode);
    }

    /// <summary>Gets or sets a count, no value object.</summary>
    public int PlainCount { get; set; }

    /// <summary>Gets or sets an object Newtonsoft.Json cannot build back, which JSON Patch cannot copy.</summary>
    public Unrebuildable? Odd { get; set; }

    /// <summary>Gets or sets anything else, as JSON.</summary>
    public JObject? Extra { get; set; }

    /// <summary>Creates the account every test starts from.</summary>
    /// <returns>The account.</returns>
    public static PatchedAccount Fresh() => new()
    {
        Iban = Iban.Create("FR7630006000011234567890189"),
        Lines = Quantity.Create(3),
        Lines2 = Quantity.Create(4),
        Total = Amount.Create(12.5m),
        Limit = TransferLimit.Create(12.50m),
        Country = CountryCode.France,
        Status = DocumentStatus.Final,
        Birth = BirthDate.Create(new DateOnly(1980, 5, 17)),
        At = OccurredAt.Create(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))),
        Consent = Consent.Create(false),
        Customer = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")),
        Reference = Reference<PatchedAccount>.Create("PO-1"),
        Code = HandWrittenCode.Create("ABC"),
        Link = HandWrittenLink.Create(new Uri("https://a.example/")),
        Note = Label.Create("start"),
        Ibans = [Iban.Create("DE89370400440532013000"), Iban.Create("GB29NWBK60161331926819")],
        Previous = [null, Iban.Create("DE89370400440532013000")],
        Notes = ["a"],
        Scores = [1, 2],
        Stock = new() { ["apples"] = Quantity.Create(1) },
        Optional = new() { ["none"] = null, ["one"] = Iban.Create("DE89370400440532013000") },
        PerCountry = new() { [CountryCode.France] = Quantity.Create(1) },
        Names = new() { [CountryCode.France] = "France" },
        Plain = new() { ["a"] = 1 },
        Home = new PatchedAddress { Reference = Ordering.OrderReference.Create("Main"), Floor = Quantity.Create(2) },
        Odd = new Unrebuildable(1),
        Extra = new JObject { ["note"] = "x" },
    };

    /// <summary>Describes the members a response shows, as text, a default instance as <see langword="null"/>.</summary>
    /// <returns>The text of each member.</returns>
    public Dictionary<string, string?> Summary() => new()
    {
        ["iban"] = IsUnset<Iban, string>(Iban) ? null : Iban.Value,
        ["backup"] = Backup?.Value,
        ["lines"] = Lines.Value.ToString(CultureInfo.InvariantCulture),
        ["total"] = Total.Value.ToString(CultureInfo.InvariantCulture),
        ["birth"] = IsUnset<BirthDate, DateOnly>(Birth) ? null : Birth.Value.ToString("O", CultureInfo.InvariantCulture),
        ["at"] = At.Value.ToString("O", CultureInfo.InvariantCulture),
        ["at2"] = IsUnset<OccurredAt, DateTimeOffset>(At2) ? null : At2.Value.ToString("O", CultureInfo.InvariantCulture),
        ["code"] = Code.Value,
        ["code2"] = IsUnset<HandWrittenCode, string>(Code2) ? null : Code2.Value,
        ["country"] = Country.Value,
        ["ibans"] = string.Join(",", Ibans.Select(static iban => iban.Value)),
        ["perCountry"] = string.Join(",", PerCountry.Select(static entry => $"{entry.Key.Value}={entry.Value.Value}")),
    };

    /// <summary>Tells a default instance, which a value object hides from its members.</summary>
    private static bool IsUnset<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => value.IsDefault;
}

/// <summary>An address, an object value a document may replace whole.</summary>
public sealed class PatchedAddress
{
    /// <summary>Gets or sets the reference of the address.</summary>
    public Ordering.OrderReference Reference { get; set; }

    /// <summary>Gets or sets the floor.</summary>
    public Quantity Floor { get; set; }
}

/// <summary>An object with two constructors and none without parameters, which Newtonsoft.Json cannot build back.</summary>
public sealed class Unrebuildable
{
    /// <summary>Initializes a new instance of the <see cref="Unrebuildable"/> class from a number.</summary>
    /// <param name="number">The number.</param>
    public Unrebuildable(int number) => Number = number;

    /// <summary>Initializes a new instance of the <see cref="Unrebuildable"/> class from text.</summary>
    /// <param name="text">The number, as text.</param>
    public Unrebuildable(string text) => Number = int.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>Gets the number.</summary>
    public int Number { get; }
}
