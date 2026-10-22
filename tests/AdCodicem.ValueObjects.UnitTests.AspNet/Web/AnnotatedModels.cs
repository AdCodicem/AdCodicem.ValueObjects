using System.ComponentModel.DataAnnotations;
using AdCodicem.ValueObjects.UnitTests.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// A transfer whose members hold the text of value objects, as a record: the attributes go on the parameters of its
/// primary constructor, where MVC requires them.
/// </summary>
/// <param name="Iban">An account.</param>
/// <param name="Quantity">A quantity, held through the generic form.</param>
/// <param name="Country">A country.</param>
/// <param name="Ibans">Accounts.</param>
/// <param name="Localized">A quantity whose message is read from the resource by the code of its rule.</param>
public sealed record RawTransfer(
    [ParsesAs(typeof(Iban))] string? Iban,
    [ParsesAs<Quantity, short>] string? Quantity,
    [ParsesAs(typeof(CountryCode))] string? Country,
    [ParsesAs(typeof(Iban))] List<string>? Ibans,
    [ParsesAs(typeof(Quantity), ErrorMessageResourceType = typeof(ValidationMessages))] string? Localized);

/// <summary>
/// A record whose attributes target its properties, which MVC refuses on a record it validates, and which a minimal API
/// takes, under <c>[AsParameters]</c> as for a body.
/// </summary>
/// <param name="Iban">An account.</param>
/// <param name="Quantity">A quantity.</param>
public sealed record PropertyTargetedSearch(
    [property: ParsesAs(typeof(Iban))] string? Iban,
    [property: ParsesAs<Quantity, short>] string? Quantity);

/// <summary>A search whose criteria are the text of value objects, bound from the query string as one model.</summary>
public sealed class RawSearch
{
    [ParsesAs(typeof(Iban))]
    public string? Iban { get; set; }

    [ParsesAs<Quantity, short>]
    public string? Quantity { get; set; }

    [ParsesAs(typeof(CountryCode))]
    public string[]? Countries { get; set; }
}

/// <summary>
/// The same search, as a record bound from the query string as one model, its attributes on its constructor's parameters.
/// </summary>
/// <param name="Iban">An account.</param>
/// <param name="Quantity">A quantity.</param>
public sealed record RawSearchRecord([ParsesAs(typeof(Iban))] string? Iban, [ParsesAs<Quantity, short>] string? Quantity);

/// <summary>
/// An MVC controller binding the text of value objects from bodies, the query string and models, and value objects
/// <c>[NotDefault]</c> refuses at their default.
/// </summary>
[ApiController]
[Route("annotations")]
public sealed class AnnotatedTransfersController : ControllerBase
{
    /// <summary>Accepts a transfer as a record.</summary>
    /// <param name="transfer">The transfer.</param>
    /// <returns>No content.</returns>
    [HttpPost("record")]
    public IActionResult Record(RawTransfer transfer) => NoContent();

    /// <summary>Accepts a transfer as a class.</summary>
    /// <param name="form">The transfer.</param>
    /// <returns>No content.</returns>
    [HttpPost("class")]
    public IActionResult Class(TransferForm form) => NoContent();

    /// <summary>Accepts a record whose attributes target its properties, which MVC refuses.</summary>
    /// <param name="search">The search.</param>
    /// <returns>No content.</returns>
    [HttpPost("property-targeted")]
    public IActionResult PropertyTargeted(PropertyTargetedSearch search) => NoContent();

    /// <summary>Accepts the text of an account from the query string.</summary>
    /// <param name="iban">The account.</param>
    /// <returns>No content.</returns>
    [HttpGet("query")]
    public IActionResult Query([FromQuery, ParsesAs(typeof(Iban))] string? iban) => NoContent();

    /// <summary>Accepts a search bound from the query string as one model, a class.</summary>
    /// <param name="search">The search.</param>
    /// <returns>No content.</returns>
    [HttpGet("search")]
    public IActionResult Search([FromQuery] RawSearch search) => NoContent();

    /// <summary>Accepts a search bound from the query string as one model, a record.</summary>
    /// <param name="search">The search.</param>
    /// <returns>No content.</returns>
    [HttpGet("search-record")]
    public IActionResult SearchRecord([FromQuery] RawSearchRecord search) => NoContent();

    /// <summary>Accepts value objects, which must not be left at their default.</summary>
    /// <param name="command">The command.</param>
    /// <returns>No content.</returns>
    [HttpPost("typed")]
    public IActionResult Typed(SettleCommand command) => NoContent();
}

/// <summary>
/// A request body holding the text of each value object of <see cref="EveryValueObject"/>, held to its rules, which the
/// documents describe as that text.
/// </summary>
public sealed record EveryValueObjectAsText(
    [ParsesAs(typeof(Iban))] string? Iban,
    [ParsesAs(typeof(EmailAddress))] string? Email,
    [ParsesAs(typeof(Amount))] string? Amount,
    [ParsesAs(typeof(Percentage))] string? Percentage,
    [ParsesAs(typeof(CustomerId))] string? Customer,
    [ParsesAs(typeof(CountryCode))] string? Country,
    [ParsesAs(typeof(BirthDate))] string? Born,
    [ParsesAs(typeof(Quantity))] string? Quantity,
    [ParsesAs(typeof(Ordering.OrderReference))] string? Order,
    [ParsesAs(typeof(AccountId))] string? Account,
    [ParsesAs(typeof(LedgerEntryId))] string? LedgerEntry,
    [ParsesAs(typeof(Consent))] string? Consent,
    [ParsesAs(typeof(Grade))] string? Grade,
    [ParsesAs(typeof(Adjustment))] string? Adjustment,
    [ParsesAs(typeof(Score))] string? Score,
    [ParsesAs(typeof(Port))] string? Port,
    [ParsesAs(typeof(PageNumber))] string? Page,
    [ParsesAs(typeof(SequenceNumber))] string? Sequence,
    [ParsesAs(typeof(FileSize))] string? Size,
    [ParsesAs(typeof(ByteCount))] string? Bytes,
    [ParsesAs(typeof(LedgerBalance))] string? Balance,
    [ParsesAs(typeof(Fingerprint))] string? Fingerprint,
    [ParsesAs(typeof(Latitude))] string? Latitude,
    [ParsesAs(typeof(Ratio))] string? Ratio,
    [ParsesAs(typeof(OpeningTime))] string? Opens,
    [ParsesAs(typeof(RecordedAt))] string? Recorded,
    [ParsesAs(typeof(OccurredAt))] string? Occurred,
    [ParsesAs(typeof(Duration))] string? Took,
    [ParsesAs(typeof(PhoneNumber))] string? Phone,
    [ParsesAs(typeof(Label))] string? Label,
    [ParsesAs(typeof(DocumentStatus))] string? Status,
    [ParsesAs(typeof(Mass))] string? Mass,
    [ParsesAs(typeof(TransferLimit))] string? Limit,
    [ParsesAs(typeof(Luminance))] string? Luminance,
    [ParsesAs(typeof(Priority))] string? Priority,
    [ParsesAs(typeof(StorageQuota))] string? Quota,
    [ParsesAs(typeof(VatRate))] string? VatRate,
    [ParsesAs(typeof(VoteWeight))] string? VoteWeight,
    [ParsesAs(typeof(Opacity))] string? Opacity,
    [ParsesAs(typeof(TermsAccepted))] string? Terms,
    [ParsesAs(typeof(HttpStatus))] string? HttpStatus,
    [ParsesAs(typeof(BlockSize))] string? BlockSize,
    [ParsesAs(typeof(CutOffDate))] string? CutOff,
    [ParsesAs(typeof(ShiftStart))] string? Shift,
    [ParsesAs(typeof(LaunchMoment))] string? Launch,
    [ParsesAs(typeof(Answer))] string? Answer,
    [ParsesAs(typeof(NoticeChannel<AccountFilter>))] string? Notice,
    [ParsesAs<Quantity, short>] string? GenericQuantity,
    [ParsesAs(typeof(Iban))] List<string>? Alternates,
    string? Plain);

/// <summary>
/// A body holding the text of value objects as the properties of a class, which no constructor names: one required, and
/// one whose own attribute states a rule stricter than its value object's, which both generators write before the value
/// object's.
/// </summary>
public sealed class TextMembers
{
    [Required]
    [ParsesAs(typeof(CountryCode))]
    public string? Country { get; set; }

    [ParsesAs(typeof(Iban))]
    [StringLength(20)]
    public string? Bounded { get; set; }

    public string? Note { get; set; }
}

/// <summary>Criteria gathered with <c>[AsParameters]</c>, as a class, the text of each held to its rules.</summary>
public sealed class TextCriteria
{
    [ParsesAs(typeof(Iban))]
    [FromQuery(Name = "account")]
    public string? Account { get; set; }

    [ParsesAs<Quantity, short>]
    [FromHeader(Name = "X-Quantity")]
    public string? Quantity { get; set; }
}
