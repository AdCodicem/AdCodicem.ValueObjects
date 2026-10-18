using System.Collections;
using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using JasperFx;
using Marten;
using Marten.Linq;
using NpgsqlTypes;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// Document stores over a server nobody listens on: Marten writes its SQL, plans its compiled queries and scripts its
/// tables without a connection, so the unit suite reads all three offline; the integration suite runs them on PostgreSQL.
/// </summary>
internal static class MartenHarness
{
    /// <summary>A connection string to a port nothing listens on, with a timeout of a second, should anything connect.</summary>
    public const string Unreachable = "Host=127.0.0.1;Port=1;Database=offline;Username=offline;Password=offline;Timeout=1";

    /// <summary>Gets a store taught every value object the registry describes, shared by the tests reading SQL only.</summary>
    public static DocumentStore Store { get; } = Offline(static options => options.UseValueObjects());

    /// <summary>Creates a store over <see cref="Unreachable"/>, which creates no schema object.</summary>
    /// <param name="configure">The rest of its configuration.</param>
    /// <returns>The store.</returns>
    public static DocumentStore Offline(Action<StoreOptions> configure)
        => DocumentStore.For(options =>
        {
            options.Connection(Unreachable);
            options.AutoCreateSchemaObjects = AutoCreate.None;
            configure(options);
        });

    /// <summary>Gets the SQL and the parameters Marten sends for a query.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>Its command.</returns>
    public static MartenCommand Sql<T>(IQueryable<T> query)
        where T : notnull
        => MartenCommand.Of(query.ToCommand(FetchType.FetchMany));

    /// <summary>Gets the SQL and the parameters Marten sends for a query of the shared store.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="query">The query, over the store's documents of that type.</param>
    /// <returns>Its command.</returns>
    public static MartenCommand Sql<T>(Func<IQueryable<T>, IQueryable<T>> query)
        where T : notnull
        => Sql(Store, query);

    /// <summary>Gets the SQL and the parameters Marten sends for a query of a store.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="query">The query, over the store's documents of that type.</param>
    /// <returns>Its command.</returns>
    public static MartenCommand Sql<T>(IDocumentStore store, Func<IQueryable<T>, IQueryable<T>> query)
        where T : notnull
    {
        using var session = store.QuerySession();
        return Sql(query(session.Query<T>()));
    }

    /// <summary>Gets the command Marten plans for a compiled query, and binds this call's values into.</summary>
    /// <typeparam name="TDocument">The document type.</typeparam>
    /// <typeparam name="TResult">What the query returns.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="query">The compiled query, with this call's values.</param>
    /// <returns>Its command.</returns>
    public static MartenCommand Preview<TDocument, TResult>(IDocumentStore store, ICompiledQuery<TDocument, TResult> query)
        where TDocument : notnull
        where TResult : notnull
        => MartenCommand.Of(store.Diagnostics.PreviewCommand(query));

    /// <summary>Gets the lines of the script that creates a store's tables that name one of the columns.</summary>
    /// <param name="store">The store.</param>
    /// <param name="columns">The columns, by name.</param>
    /// <returns>Each column's definition, trimmed, a trailing comma removed.</returns>
    public static IReadOnlyList<string> Columns(IDocumentStore store, params string[] columns)
        => [.. store.Storage.ToDatabaseScript()
            .Split('\n')
            .Select(static line => line.Trim().TrimEnd(','))
            .Where(line => columns.Any(column => line.StartsWith(column + " ", StringComparison.Ordinal)))];
}

/// <summary>The SQL of a command and its parameters, each as its value and its database type.</summary>
/// <param name="Text">The SQL.</param>
/// <param name="Parameters">The parameters, in order.</param>
internal sealed record MartenCommand(string Text, IReadOnlyList<MartenParameter> Parameters)
{
    /// <summary>Gets the SQL followed by each parameter, as <see cref="MartenParameter.Description"/> writes it.</summary>
    public string Description => string.Join(" | ", [Text, .. Parameters.Select(static parameter => parameter.Description)]);

    /// <summary>Reads an Npgsql command.</summary>
    /// <param name="command">The command.</param>
    /// <returns>Its SQL and parameters.</returns>
    public static MartenCommand Of(Npgsql.NpgsqlCommand command)
        => new(command.CommandText, [.. command.Parameters.Select(static parameter => new MartenParameter(parameter.Value, parameter.NpgsqlDbType))]);
}

/// <summary>A parameter of a command.</summary>
/// <param name="Value">Its value.</param>
/// <param name="Type">Its database type.</param>
internal sealed record MartenParameter(object? Value, NpgsqlDbType Type)
{
    /// <summary>Gets the values of a parameter holding a list, as objects.</summary>
    public IReadOnlyList<object?> Values => [.. ((IEnumerable)Value!).Cast<object?>()];

    /// <summary>
    /// Gets the parameter as text, in the invariant culture: its value, its type and its database type,
    /// <c>3 Int32 Integer</c>, or, for a list or an array, its elements, their type and its database type,
    /// <c>[FR, BE] String Array|Text</c>.
    /// </summary>
    public string Description
        => Value is IEnumerable values and not string
            ? $"[{string.Join(", ", values.Cast<object?>().Select(Text))}] {ElementType(values).Name} {TypeName}"
            : $"{Text(Value)} {Value?.GetType().Name} {TypeName}";

    private string TypeName
        => Type.HasFlag(NpgsqlDbType.Array) ? $"Array|{Type & ~NpgsqlDbType.Array}" : Type.ToString();

    private static Type ElementType(IEnumerable values)
        => values.GetType() is { IsArray: true } array ? array.GetElementType()! : values.GetType().GetGenericArguments().Single();

    private static string? Text(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>An order holding a value object of each family, the suite's own document, apart from <c>Domain/</c>.</summary>
public sealed class MartenOrder
{
    public Guid Id { get; set; }

    public Iban Iban { get; set; }

    public Iban? Backup { get; set; }

    public PageNumber Page { get; set; }

    public PageNumber? MaybePage { get; set; }

    public Quantity Quantity { get; set; }

    public FileSize Size { get; set; }

    public Amount Amount { get; set; }

    public Latitude Latitude { get; set; }

    public CustomerId Customer { get; set; }

    public Consent Consent { get; set; }

    public BirthDate Born { get; set; }

    public OccurredAt At { get; set; }

    public Grade Grade { get; set; }

    public CountryCode Country { get; set; }

    public CountryCode Origin { get; set; }

    public Label Label { get; set; }

    public Reference<MartenOwner> Reference { get; set; }

    public Reference<MartenCompiledOwner> CompiledReference { get; set; }

    public Reference<MartenUnknownOwner> UnknownReference { get; set; }

    public Declared<MartenCodeDeclaration, string> Code { get; set; }

    public Port Port { get; set; }

    public ushort PortNumber { get; set; }

    public short Count { get; set; }

    public char Letter { get; set; }

    public List<CountryCode> Countries { get; set; } = [];

    public CountryCode[] CountryArray { get; set; } = [];

    public HashSet<CountryCode> CountrySet { get; set; } = [];

    public List<CountryCode?> MaybeCountries { get; set; } = [];

    public Dictionary<CountryCode, int> Stock { get; set; } = [];

    public List<MartenLine> Lines { get; set; } = [];

    public string Note { get; set; } = string.Empty;

    public int Index { get; set; }
}

/// <summary>A line of a <see cref="MartenOrder"/>, with a value object over each type C# compares as an <c>int</c> and its primitive.</summary>
public sealed class MartenLine
{
    public Label Tag { get; set; }

    public Quantity Quantity { get; set; }

    public CountryCode Country { get; set; }

    public Score Score { get; set; }

    public Adjustment Adjustment { get; set; }

    public Grade Grade { get; set; }

    public short Small { get; set; }

    public byte Points { get; set; }

    public sbyte Degrees { get; set; }

    public char Letter { get; set; }
}

/// <summary>The owner of the references a <see cref="MartenOrder"/> holds, which no other test closes over.</summary>
public sealed class MartenOwner;

/// <summary>The declaration of the value object written by hand a <see cref="MartenOrder"/> holds.</summary>
public sealed class MartenCodeDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };
}
