namespace AdCodicem.ValueObjects.CompiledModel;

/// <summary>
/// An entity holding every value object of the domain the convention maps, each required and optional: every
/// underlying type but <see cref="Int128"/> and <see cref="UInt128"/>, which the convention leaves to the application, a
/// construction of a generic value object, an entity identifier as its key, and collections of value objects, over text
/// and over a value type, of optional ones and of a construction, which the convention maps as primitive collections.
/// </summary>
/// <remarks>
/// Not sealed: a query Entity Framework Core 10 precompiles casts each entity it materializes to an interface of its
/// own, which the compiler refuses for a sealed class (CS0030).
/// </remarks>
#pragma warning disable CA1852 // Sealing it is what breaks the precompiled queries of the native AOT variant.
public class Shipment
#pragma warning restore CA1852
{
    public OrderId Id { get; set; }

    public OrderId? Previous { get; set; }

    public Consent Consent { get; set; }

    public Consent? OptionalConsent { get; set; }

    public Grade Grade { get; set; }

    public Grade? OptionalGrade { get; set; }

    public Adjustment Adjustment { get; set; }

    public Adjustment? OptionalAdjustment { get; set; }

    public Score Score { get; set; }

    public Score? OptionalScore { get; set; }

    public Quantity Quantity { get; set; }

    public Quantity? OptionalQuantity { get; set; }

    public Port Port { get; set; }

    public Port? OptionalPort { get; set; }

    public PageNumber Page { get; set; }

    public PageNumber? OptionalPage { get; set; }

    public SequenceNumber Sequence { get; set; }

    public SequenceNumber? OptionalSequence { get; set; }

    public FileSize Size { get; set; }

    public FileSize? OptionalSize { get; set; }

    public ByteCount Transferred { get; set; }

    public ByteCount? OptionalTransferred { get; set; }

    public Ratio Ratio { get; set; }

    public Ratio? OptionalRatio { get; set; }

    public Latitude Latitude { get; set; }

    public Latitude? OptionalLatitude { get; set; }

    public Amount Amount { get; set; }

    public Amount? OptionalAmount { get; set; }

    public VatRate Rate { get; set; }

    public VatRate? OptionalRate { get; set; }

    public CustomerId Customer { get; set; }

    public CustomerId? OptionalCustomer { get; set; }

    public RecordedAt Recorded { get; set; }

    public RecordedAt? OptionalRecorded { get; set; }

    public OccurredAt Occurred { get; set; }

    public OccurredAt? OptionalOccurred { get; set; }

    public EffectiveDate Effective { get; set; }

    public EffectiveDate? OptionalEffective { get; set; }

    public OpeningTime Opens { get; set; }

    public OpeningTime? OptionalOpens { get; set; }

    public Duration Duration { get; set; }

    public Duration? OptionalDuration { get; set; }

    public EmailAddress Email { get; set; }

    public EmailAddress? OptionalEmail { get; set; }

    public DocumentStatus Status { get; set; }

    public DocumentStatus? OptionalStatus { get; set; }

    public Label Label { get; set; }

    public Label? OptionalLabel { get; set; }

    public DocumentNumber<PurchaseOrder> Number { get; set; }

    public DocumentNumber<PurchaseOrder>? OptionalNumber { get; set; }

    public List<EmailAddress> Contacts { get; set; } = [];

    public List<EmailAddress?> OptionalContacts { get; set; } = [];

    public Quantity[] Batches { get; set; } = [];

    public List<Quantity?> OptionalBatches { get; set; } = [];

    public List<DocumentNumber<PurchaseOrder>> Related { get; set; } = [];
}
