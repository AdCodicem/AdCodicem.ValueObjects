using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.Blazor;

/// <summary>An order, every field set, which the page of a static form starts from.</summary>
public sealed class OrderForm
{
    /// <summary>Gets or sets the account.</summary>
    public AccountNumber Account { get; set; } = AccountNumber.Create("FR7630006000011234567890189");

    /// <summary>Gets or sets the quantity.</summary>
    public OrderQuantity Quantity { get; set; } = OrderQuantity.Create(3);

    /// <summary>Gets or sets the amount, whose text depends on the culture.</summary>
    public OrderAmount Amount { get; set; } = OrderAmount.Create(12.5m);

    /// <summary>Gets or sets a count its rules accept at zero.</summary>
    public ItemCount Count { get; set; } = ItemCount.Create(0);

    /// <summary>Gets or sets the due date.</summary>
    public DueDate Due { get; set; } = DueDate.Create(new DateOnly(2026, 10, 14));

    /// <summary>Gets or sets the pickup time, to the fraction of a second.</summary>
    public PickupTime Pickup { get; set; } = PickupTime.Create(new TimeOnly(10, 30, 15, 250));

    /// <summary>Gets or sets when the order was placed, in UTC, to the fraction of a second.</summary>
    public PlacedAt Placed { get; set; } = PlacedAt.Create(new DateTime(2026, 10, 14, 10, 30, 15, 250, DateTimeKind.Utc));

    /// <summary>Gets or sets when the order was confirmed, with its offset.</summary>
    public ConfirmedAt Confirmed { get; set; } = ConfirmedAt.Create(new DateTimeOffset(2026, 10, 14, 10, 30, 15, 250, TimeSpan.FromHours(2)));

    /// <summary>Gets or sets how long the order is held, to the millisecond.</summary>
    public HoldTime Hold { get; set; } = HoldTime.Create(new TimeSpan(1, 1, 1, 1, 1));
}

/// <summary>A form a static page posts, with an optional account and the text of one held to its rules.</summary>
public sealed class OptionalForm
{
    /// <summary>Gets or sets an optional account, which the framework's form mapping refuses empty.</summary>
    public AccountNumber? Optional { get; set; }

    /// <summary>Gets or sets the text of an account, held to its rules by DataAnnotations.</summary>
    [ParsesAs(typeof(AccountNumber))]
    public string? Text { get; set; }
}

/// <summary>A form whose account nobody may leave untouched.</summary>
public sealed class AnnotatedOrder
{
    /// <summary>Gets or sets the account, refused at its default by <see cref="NotDefaultAttribute"/>.</summary>
    [NotDefault]
    public AccountNumber Account { get; set; }
}
