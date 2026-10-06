using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Actions taking request bodies only Newtonsoft.Json reads as intended, for the in-memory MVC applications of
/// <see cref="NewtonsoftJsonModelBindingTests"/>.
/// </summary>
[ApiController]
[Route("newtonsoft")]
public sealed class NewtonsoftProbeController : ControllerBase
{
    /// <summary>Echoes the reference of a parcel read from a JSON body.</summary>
    /// <param name="parcel">The parcel.</param>
    /// <returns>Its reference.</returns>
    [HttpPost("parcels")]
    public IActionResult Parcel([FromBody] NewtonsoftParcel parcel) => Ok(parcel.Reference.Value);

    /// <summary>Echoes a moment and an amount read from a JSON body, as their text.</summary>
    /// <param name="reading">The reading.</param>
    /// <returns>Both values.</returns>
    [HttpPost("readings")]
    public IActionResult Reading([FromBody] NewtonsoftReading reading)
        => Ok($"{reading.At.Value:O}|{reading.Total.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

    /// <summary>Echoes a body, written back with the settings it was read with.</summary>
    /// <param name="answer">The body.</param>
    /// <returns>The same body.</returns>
    [HttpPost("answers")]
    public IActionResult Answer([FromBody] NewtonsoftAnswer answer) => Ok(answer);

    /// <summary>Takes a body whose converter fails with an exception no JSON explains.</summary>
    /// <param name="faulty">The body.</param>
    /// <returns>Never anything: the body cannot be read.</returns>
    [HttpPost("faulty")]
    public IActionResult Faulty([FromBody] NewtonsoftFaulty faulty) => Ok(faulty.Name);
}

/// <summary>A request body holding value objects of every kind, as properties Newtonsoft.Json sets one by one.</summary>
public sealed class NewtonsoftParcel
{
    /// <summary>Gets or sets the reference of the parcel.</summary>
    public Ordering.OrderReference Reference { get; set; }

    /// <summary>Gets or sets the country it ships to, if any.</summary>
    public CountryCode? Country { get; set; }

    /// <summary>Gets or sets the number of items.</summary>
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets the address notified, if any.</summary>
    public EmailAddress? Email { get; set; }

    /// <summary>Gets or sets the lines of the parcel.</summary>
    public List<NewtonsoftLine>? Lines { get; set; }

    /// <summary>Gets or sets references keyed by other names.</summary>
    public Dictionary<string, Ordering.OrderReference>? Aliases { get; set; }

    /// <summary>Gets or sets names keyed by a reference, a key Newtonsoft.Json reads through the type converter.</summary>
    public Dictionary<Ordering.OrderReference, string>? Keyed { get; set; }

    /// <summary>Gets or sets the order the parcel ships, a record Newtonsoft.Json builds through its constructor.</summary>
    public ProbeOrder? Order { get; set; }

    /// <summary>Gets or sets the reference of the purchase order, a construction of a generic value object.</summary>
    public Reference<PurchaseOrder>? Purchase { get; set; }

    /// <summary>Gets or sets a code, a value object written by hand.</summary>
    public HandWrittenCode? Code { get; set; }

    /// <summary>
    /// Gets or sets a link, a value object written by hand over a type whose value Newtonsoft.Json reads itself.
    /// </summary>
    public HandWrittenLink? Link { get; set; }

    /// <summary>Gets or sets a reference read by a converter of the application, which creates it.</summary>
    [JsonConverter(typeof(CreatingConverter))]
    public Ordering.OrderReference? Created { get; set; }

    /// <summary>Gets or sets references keyed by other names, each read by a converter of the application.</summary>
    [JsonProperty(ItemConverterType = typeof(CreatingConverter))]
    public Dictionary<string, Ordering.OrderReference?>? CreatedAliases { get; set; }
}

/// <summary>A line of a parcel.</summary>
public sealed class NewtonsoftLine
{
    /// <summary>Gets or sets the reference of the line.</summary>
    public Ordering.OrderReference Reference { get; set; }
}

/// <summary>A request body holding the two value objects Newtonsoft.Json's defaults read wrong.</summary>
public sealed class NewtonsoftReading
{
    /// <summary>Gets or sets when the reading was taken, with its offset.</summary>
    public OccurredAt At { get; set; }

    /// <summary>Gets or sets the amount read, with its scale.</summary>
    public Amount Total { get; set; }
}

/// <summary>
/// A body holding a number and a boolean, which the converter and the type converter write apart, and a member of
/// no particular type.
/// </summary>
public sealed class NewtonsoftAnswer
{
    /// <summary>Gets or sets a number of items.</summary>
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets whether the customer agreed.</summary>
    public Consent Consent { get; set; }

    /// <summary>Gets or sets anything else the client sent.</summary>
    public object? Extra { get; set; }
}

/// <summary>A request body whose converter fails with an exception that is no refusal of the JSON.</summary>
public sealed class NewtonsoftFaulty
{
    /// <summary>Gets or sets a name, never read.</summary>
    [JsonConverter(typeof(FailingConverter))]
    public string? Name { get; set; }
}

/// <summary>
/// Reads an order reference through <see cref="Ordering.OrderReference.Create(string)"/>, as a converter of an
/// application might: a refusal is a <see cref="ValueObjectException"/>, which Newtonsoft.Json raises as it is.
/// </summary>
public sealed class CreatingConverter : JsonConverter<Ordering.OrderReference?>
{
    /// <inheritdoc />
    public override Ordering.OrderReference? ReadJson(
        JsonReader reader,
        Type objectType,
        Ordering.OrderReference? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
        => reader.Value is string text ? Ordering.OrderReference.Create(text) : null;

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, Ordering.OrderReference? value, JsonSerializer serializer)
        => writer.WriteValue(value?.Value);
}

/// <summary>A converter failing every read with an <see cref="InvalidOperationException"/>.</summary>
public sealed class FailingConverter : JsonConverter<string?>
{
    /// <inheritdoc />
    public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
        => throw new InvalidOperationException("The converter is broken.");

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
        => writer.WriteValue(value);
}
