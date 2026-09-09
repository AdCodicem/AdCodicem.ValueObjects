using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

/// <summary>
/// A real database, started in a container, against which the value object mapping is verified.
/// </summary>
/// <remarks>
/// Value objects are only interesting once the values reach actual columns: the point of these tests is to see
/// an IBAN land in a bounded text column, a customer identifier in the provider's native identifier type, and a
/// comparison on a value object translated into SQL rather than evaluated on the client.
/// </remarks>
public abstract class DatabaseFixture : IAsyncLifetime
{
    /// <summary>Gets the connection string of the running container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Gets the name the sample application uses for this provider.</summary>
    public abstract string ProviderName { get; }

    /// <summary>Starts the container and creates the schema.</summary>
    /// <returns>A task that completes once the database is ready.</returns>
    public async ValueTask InitializeAsync()
    {
        ConnectionString = await StartAsync();

        await using var database = CreateContext();
        await database.Database.EnsureCreatedAsync();
    }

    /// <summary>Stops the container.</summary>
    /// <returns>A task that completes once the container is gone.</returns>
    public abstract ValueTask DisposeAsync();

    /// <summary>Creates a context bound to the running container.</summary>
    /// <returns>A new context.</returns>
    public BankingDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<BankingDbContext>();
        Configure(builder, ConnectionString);

        return new BankingDbContext(builder.Options);
    }

    /// <summary>Opens a raw connection to the running container, for the Dapper checks.</summary>
    /// <returns>An open connection.</returns>
    public abstract DbConnection CreateConnection();

    /// <summary>Quotes an identifier the way this provider expects.</summary>
    /// <param name="identifier">Identifier to quote.</param>
    /// <returns>The quoted identifier.</returns>
    public abstract string Quote(string identifier);

    /// <summary>Gets the name this provider gives a bounded text column in its catalog.</summary>
    public abstract string BoundedTextType { get; }

    /// <summary>Configures the provider under test.</summary>
    /// <param name="builder">Options builder.</param>
    /// <param name="connectionString">Connection string of the running container.</param>
    public abstract void Configure(DbContextOptionsBuilder builder, string connectionString);

    /// <summary>Starts the container.</summary>
    /// <returns>The connection string.</returns>
    protected abstract Task<string> StartAsync();
}

/// <summary>
/// PostgreSQL, whose provider is strict about typing and therefore a good detector of mapping mistakes.
/// </summary>
public sealed class PostgreSqlFixture : DatabaseFixture
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <inheritdoc />
    public override string ProviderName => "PostgreSql";

    /// <inheritdoc />
    public override void Configure(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseNpgsql(connectionString);

    /// <inheritdoc />
    public override DbConnection CreateConnection() => new NpgsqlConnection(ConnectionString);

    /// <inheritdoc />
    public override string Quote(string identifier) => $"\"{identifier}\"";

    /// <inheritdoc />
    public override string BoundedTextType => "character varying";

    /// <inheritdoc />
    public override async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <inheritdoc />
    protected override async Task<string> StartAsync()
    {
        await _container.StartAsync();

        return _container.GetConnectionString();
    }
}

/// <summary>
/// SQL Server, which differs from PostgreSQL on identifier storage and on default collation.
/// </summary>
public sealed class SqlServerFixture : DatabaseFixture
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <inheritdoc />
    public override string ProviderName => "SqlServer";

    /// <inheritdoc />
    public override void Configure(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseSqlServer(connectionString);

    /// <inheritdoc />
    public override DbConnection CreateConnection() => new SqlConnection(ConnectionString);

    /// <inheritdoc />
    public override string Quote(string identifier) => $"[{identifier}]";

    /// <inheritdoc />
    public override string BoundedTextType => "nvarchar";

    /// <inheritdoc />
    public override async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <inheritdoc />
    protected override async Task<string> StartAsync()
    {
        await _container.StartAsync();

        return _container.GetConnectionString();
    }
}
