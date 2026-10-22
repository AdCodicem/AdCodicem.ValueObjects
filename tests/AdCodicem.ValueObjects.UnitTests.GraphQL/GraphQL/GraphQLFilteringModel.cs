using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.HotChocolate.Data;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Relay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// HotChocolate infers the fields of a query type from the instance members of its class.
#pragma warning disable CA1822

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>The owner of the references a filter row holds, which nothing else names.</summary>
public sealed class FilterOwner;

/// <summary>The owner of a reference nothing registers before a filter meets it, which nothing else names.</summary>
public sealed class ResolvedOwner;

/// <summary>A part of a filter row, a complex type of its own, holding a value object.</summary>
public sealed class FilterInner
{
    public Iban Iban { get; set; }
}

/// <summary>A row whose columns are value objects of every shape a filter meets.</summary>
public sealed class FilterRow
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public Iban Iban { get; set; }

    public Quantity Qty { get; set; }

    public EmailAddress Email { get; set; }

    public EmailAddress? Backup { get; set; }

    public Amount? Balance { get; set; }

    public CustomerId Customer { get; set; }

    public BirthDate? Born { get; set; }

    public CountryCode Country { get; set; }

    public Consent Consent { get; set; }

    public Reference<FilterOwner> Ref { get; set; }

    public List<Iban> Previous { get; set; } = [];

    public FilterInner Inner { get; set; } = new();
}

/// <summary>The rows every filtering test reads, in memory or from SQLite.</summary>
public static class FilterRows
{
    public const string French = "FR7630006000011234567890189";

    public const string German = "DE89370400440532013000";

    public static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static List<FilterRow> Seed() =>
    [
        new()
        {
            Id = 1,
            Name = "first",
            Iban = Iban.Create(French),
            Qty = Quantity.Create(3),
            Email = EmailAddress.Create("ada@example.com"),
            Backup = EmailAddress.Create("ada@backup.org"),
            Balance = Amount.Create(12.5m),
            Customer = CustomerId.Create(First),
            Born = BirthDate.Create(new DateOnly(1980, 5, 17)),
            Country = CountryCode.France,
            Consent = Consent.Create(true),
            Ref = Reference<FilterOwner>.Create("PO-1"),
            Previous = [Iban.Create(German)],
            Inner = new() { Iban = Iban.Create(French) },
        },
        new()
        {
            Id = 2,
            Name = null,
            Iban = Iban.Create(German),
            Qty = Quantity.Create(9),
            Email = EmailAddress.Create("bob@test.net"),
            Backup = null,
            Balance = null,
            Customer = CustomerId.Create(Second),
            Born = null,
            Country = CountryCode.Belgium,
            Consent = Consent.Create(false),
            Ref = Reference<FilterOwner>.Create("PO-2"),
            Previous = [],
            Inner = new() { Iban = Iban.Create(German) },
        },
    ];
}

/// <summary>The rows, mapped by the Entity Framework Core package's conventions.</summary>
/// <param name="options">The options of the test's database.</param>
public sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
{
    public DbSet<FilterRow> Rows => Set<FilterRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureValueObjects(typeof(Iban).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<FilterRow>(row =>
        {
            row.Property(static entity => entity.Id).ValueGeneratedNever();
            row.ComplexProperty(static entity => entity.Inner);
        });
}

/// <summary>
/// The rows, filtered, sorted and projected by HotChocolate.Data, read from the context when the application registers
/// one, and else from the rows it holds in memory.
/// </summary>
public class FilterQuery
{
    [UseFiltering]
    [UseSorting]
    public IQueryable<FilterRow> Rows(IResolverContext context) => Source(context);

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<FilterRow> Projected(IResolverContext context) => Source(context);

    internal static IQueryable<FilterRow> Source(IResolverContext context)
        => context.Services.GetService<FilterContext>()?.Rows ?? context.Services.GetRequiredService<List<FilterRow>>().AsQueryable();
}

/// <summary>
/// The rows, also filtered through a filter input type of the application's own, which HotChocolate then gives every
/// field filtering the rows.
/// </summary>
public class ExplicitFilterQuery : FilterQuery
{
    [UseFiltering(typeof(ExplicitRowFilter))]
    public IQueryable<FilterRow> Explicit(IResolverContext context) => Source(context);
}

/// <summary>
/// The rows, filtered through the default filter convention and a named one; with no projection, which HotChocolate
/// refuses beside a named convention filtering a part of the row.
/// </summary>
public class ScopedFilterQuery
{
    [UseFiltering]
    public IQueryable<FilterRow> Rows(IResolverContext context) => FilterQuery.Source(context);

    [UseFiltering(Scope = "s")]
    public IQueryable<FilterRow> Scoped(IResolverContext context) => FilterQuery.Source(context);
}

/// <summary>Rows of any kind, held in memory.</summary>
/// <typeparam name="TRow">The kind of row.</typeparam>
public class RowsQuery<TRow>
{
    [UseFiltering]
    public IQueryable<TRow> Rows([Service] List<TRow> rows) => rows.AsQueryable();
}

/// <summary>A filter of the application's own over the rows, which types two of its fields itself.</summary>
public class ExplicitRowFilter : FilterInputType<FilterRow>
{
    protected override void Configure(IFilterInputTypeDescriptor<FilterRow> descriptor)
    {
        descriptor.Name("ExplicitRowFilter");
        descriptor.BindFieldsExplicitly();
        descriptor.Field(static row => row.Iban);
        descriptor.Field(static row => row.Backup);
        descriptor.Field(static row => row.Qty).Type<QuantityEqOnly>();
        descriptor.Field(static row => row.Name).Type(new NamedTypeNode("StringOperationFilterInput"));
    }
}

/// <summary>An operation input of the application's own for <see cref="Quantity"/>, with <c>eq</c> alone.</summary>
public class QuantityEqOnly : ComparableOperationFilterInputType<Quantity>
{
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        descriptor.Name("QuantityEqOnlyFilterInput");
        descriptor.Operation(DefaultFilterOperations.Equals).Type(typeof(Quantity));
        descriptor.AllowAnd(false).AllowOr(false);
    }
}

/// <summary>The package's operation input for <see cref="Quantity"/>, under a name of the application's own.</summary>
public class QuantityRange : ValueObjectOperationFilterInputType<Quantity>
{
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        base.Configure(descriptor);
        descriptor.Name("QuantityRange");
    }
}

/// <summary>The package's operation input for <see cref="Iban"/>, without <c>gt</c> and <c>ngt</c>.</summary>
public class NarrowIban : ValueObjectOperationFilterInputType<Iban>
{
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        base.Configure(descriptor);
        descriptor.Ignore(DefaultFilterOperations.GreaterThan).Ignore(DefaultFilterOperations.NotGreaterThan);
    }
}

/// <summary>A filter convention of the application's own, with HotChocolate's defaults.</summary>
public sealed class ShopFilterConvention : FilterConvention
{
    protected override void Configure(IFilterConventionDescriptor descriptor) => descriptor.AddDefaults();
}

/// <summary>A tag, written by hand over <see cref="string"/>, which nothing registers: one to eight characters.</summary>
public sealed class FilterTag : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { Description = "A tag.", MaxLength = 8 };

    public static ValidationResult Validate(string value)
        => value is { Length: > 0 and <= 8 } ? ValidationResult.Success : ValidationResult.OutOfRange("A tag holds one to eight characters.");

    public static bool TryRead(string text, out string value)
    {
        value = text;
        return true;
    }

}

/// <summary>Value objects nothing registers before a filter meets them, and a struct that is no value object, alone and in a list.</summary>
public sealed class ResolvedRow
{
    public int Id { get; set; }

    public Declared<FilterTag, string> Tag { get; set; }

    public Reference<ResolvedOwner> Ref { get; set; }

    public Point Point { get; set; }

    public List<Point> Points { get; set; } = [];
}

/// <summary>Two value objects of one name, whose scalars the application sets apart.</summary>
public sealed class CodeRow
{
    public int Id { get; set; }

    public Billing.Code Billing { get; set; }

    public Shipping.Code Shipping { get; set; }
}

/// <summary>A value object and a list of them, which an operation input of the application's own may cover.</summary>
public sealed class QuantityRow
{
    public int Id { get; set; }

    public Quantity Qty { get; set; }

    public List<Quantity> Counts { get; set; } = [];
}

/// <summary>A construction of a generic value object, its nullable type and a list of them, which a typed binding covers.</summary>
public sealed class ReferenceRow
{
    public int Id { get; set; }

    public Reference<FilterOwner> Ref { get; set; }

    public Reference<FilterOwner>? Maybe { get; set; }

    public List<Reference<FilterOwner>> Refs { get; set; } = [];
}

/// <summary>A value object written by hand without operators, its value implemented explicitly.</summary>
public sealed class OperatorlessRow
{
    public int Id { get; set; }

    public OperatorlessCode Code { get; set; }
}

/// <summary>A value object HotChocolate's <c>[ID]</c> describes.</summary>
public sealed class IdRow
{
    [ID]
    public CustomerId Customer { get; set; }
}

/// <summary>
/// A code written by hand, which declares no operator and implements its value explicitly: HotChocolate.Data's
/// comparable handlers compare through operators, its <c>in</c> through equality.
/// </summary>
#pragma warning disable CA1000, CA1036, CA2231 // The members of a value object are static, and the operators are what it lacks.
public readonly struct OperatorlessCode : IValueObject<OperatorlessCode, string>
{
    private readonly string? _value;

    private OperatorlessCode(string value) => _value = value;

    public static ValueObjectSchema Schema { get; } = new() { Description = "A code without operators." };

    string IValueObject<string>.Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value is null ? value! : value.Trim().ToUpperInvariant();

    public static ValidationResult Validate(in string value)
        => string.IsNullOrEmpty(value) ? ValidationResult.Required("A code is required.") : ValidationResult.Success;

    public static OperatorlessCode Create(string value)
    {
        var normalized = Normalize(value);
        Validate(normalized).ThrowIfInvalid(typeof(OperatorlessCode), value);
        return new(normalized);
    }

    public static bool TryCreate(string value, out OperatorlessCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out OperatorlessCode result, out ValidationResult validation)
    {
        var normalized = Normalize(value);
        validation = Validate(normalized);
        result = validation.IsValid ? new(normalized) : default;
        return validation.IsValid;
    }

    public static OperatorlessCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out OperatorlessCode result, out ValidationResult validation)
        => TryCreate(text.ToString(), out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out OperatorlessCode result) => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out OperatorlessCode result) => TryParse(s.AsSpan(), provider, out result, out _);

    public static OperatorlessCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static OperatorlessCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(OperatorlessCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is OperatorlessCode other && Equals(other);

    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public int CompareTo(OperatorlessCode other) => string.CompareOrdinal(_value, other._value);

    public override string ToString() => _value ?? string.Empty;

    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString();
        charsWritten = text.TryCopyTo(destination) ? text.Length : 0;
        return charsWritten == text.Length;
    }
}
#pragma warning restore CA1000, CA1036, CA2231

/// <summary>Registers what a filtering test reads its rows from.</summary>
internal static class FilterServices
{
    /// <summary>Registers the rows in memory.</summary>
    public static IServiceCollection AddFilterRows(this IServiceCollection services, List<FilterRow>? rows = null)
        => services.AddSingleton(rows ?? FilterRows.Seed());

    /// <summary>Registers rows of another kind in memory.</summary>
    public static IServiceCollection AddRows<TRow>(this IServiceCollection services, params TRow[] rows)
        => services.AddSingleton(rows.ToList());
}
