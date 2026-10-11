using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.UnitTests.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real database engine, that a pattern <c>AddValueObjectCheckConstraints</c> writes in the engine's
/// dialect matches what .NET matches, and that the CHECK constraint it makes accepts every value the value object
/// accepts and refuses another writer's row.
/// </summary>
/// <remarks>
/// PostgreSQL runs <c>~</c>; SQL Server runs <c>REGEXP_LIKE</c> from its 2025 version on, in a database at compatibility
/// level 170, which a new database of SQL Server 2025 is: SQL Server 2022 knows no such function.
/// </remarks>
/// <typeparam name="TFixture">Database under test.</typeparam>
public abstract class CheckConstraintPatternTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : DatabaseFixture
{
    /// <summary>The patterns of the differential: those the domain declares, and shapes of each construct written.</summary>
    private static readonly string[] Corpus =
    [
        "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
        @"^\+[0-9]{6,15}$",
        "^[A-Z]{3}$",
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        "^[A-Z]{3}-[0-9]{4}$",
        "^a|b$",
        @"\Aab\z|^cd\Z",
        @"^[\]\[\\\-\^]$",
        @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\$",
        "^ !\"#%&',/:;<=>@_`~}$",
        @"^é\x41\t$",
        "^é[à-ÿ]+$",
        "^a+?b*?c??d{2,3}?e{2,}?$",
        "^(?<year>[0-9]{4})-(?:[0-9]{2})$",
        "^x{0,255}$",
        "^(?:a{0,10}){0,10}$",
        "^$",
        "abc",
    ];

    /// <summary>The values each pattern is matched against, on the engine and in .NET.</summary>
    private static readonly string[] Inputs =
    [
        string.Empty, "a", "b", "ab", "cd", "ab\n", "cd\n", "xab", "abx", "FR7630006000011234567890189",
        "FR7630006000011234567890189\n", "fr7630006000011234567890189", "+33612345678", "+33612345678\n", "+3361", "my-slug-1",
        "my--slug", "ABC-1234", "ABC-123", "]", "[", "\\", "-", "^", "$.^|?*+()[]{}\\", " !\"#%&',/:;<=>@_`~}", "éA\t",
        "éàÿ", "Éàÿ", "abbccdd", "aaddeee", "add", "aaddee", "2024-01", "abc", "xabcx", "ABC", "ABC\n", "123",
        "١٢٣", new string('x', 255), new string('x', 256), new string('a', 100), "\U0001F600", "aa",
    ];

    /// <summary>The table of each fixture, created once.</summary>
    private static readonly ConditionalWeakTable<DatabaseFixture, Task> Tables = new();

    private static int _lastId;

    /// <summary>Gets the dialect of the engine.</summary>
    protected abstract ValueObjectPatternDialect Dialect { get; }

    /// <summary>
    /// Every pattern of the corpus is written in the engine's dialect, and matches, on the engine, exactly what it matches
    /// in .NET, for every value: a final line feed, a character outside the Basic Multilingual Plane, digits of another
    /// script, every metacharacter escaped.
    /// </summary>
    [Fact]
    public async Task Each_pattern_written_matches_on_the_engine_what_it_matches_in_dotnet()
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<PatternContext>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var text = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(string))!;
        var differences = new List<string>();

        foreach (var pattern in Corpus)
        {
            SqlPattern.TryWrite(pattern, Dialect, out var written, out var refusal).Should().BeTrue(refusal);
            var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            await using var command = connection.CreateCommand();
            command.CommandText = MatchSql(text.GenerateSqlLiteral(written));
            var parameter = command.CreateParameter();
            parameter.ParameterName = "value";
            command.Parameters.Add(parameter);
            foreach (var input in Inputs)
            {
                parameter.Value = input;
                var matched = Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1;
                if (matched != regex.IsMatch(input))
                {
                    differences.Add($"{pattern} as {written} on '{input}': the engine {matched}, .NET {!matched}");
                }
            }
        }

        differences.Should().BeEmpty();
    }

    /// <summary>
    /// The constraints are created from the patterns of the model, but on the fixed-length column of an entity identifier,
    /// and accept every value the value objects accept, a phone number ending with a line feed included, which .NET's
    /// <c>$</c> accepts.
    /// </summary>
    [Fact]
    public async Task The_pattern_constraints_accept_every_value_the_value_objects_accept()
    {
        await EnsureTablesAsync();

        await using (var write = CreateContext<PatternContext>())
        {
            write.Rows.AddRange(
                new PatternValues { Id = Next(), Iban = Iban.Create("FR7630006000011234567890189"), Phone = PhoneNumber.Create("+33612345678"), Slug = Slug.Create("my-slug-1"), Ticket = TicketCode.Create("ABC-1234"), Payment = PaymentId.New() },
                new PatternValues { Id = Next(), Phone = PhoneNumber.Create("+33612345678\n"), Slug = Slug.Create("a") },
                new PatternValues { Id = Next() });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = CreateContext<PatternContext>();
        var sql = $"SELECT constraint_name AS {Q("Value")} FROM information_schema.check_constraints WHERE constraint_name LIKE 'CK_pattern_values%'";
        var listed = await read.Database.SqlQueryRaw<string>(sql).ToListAsync(TestContext.Current.CancellationToken);
        listed.Should().BeEquivalentTo(
            "CK_pattern_values_Iban_MinLength",
            "CK_pattern_values_Iban_Pattern",
            "CK_pattern_values_Phone_Pattern",
            "CK_pattern_values_Slug_Pattern",
            "CK_pattern_values_Ticket_Pattern",
            "CK_pattern_values_Payment_MinLength");
    }

    /// <summary>A row another writer stores with a value a pattern refuses is refused by the engine, the constraint named.</summary>
    /// <param name="column">The column written.</param>
    /// <param name="literal">The value, as SQL.</param>
    [Theory]
    [InlineData("Iban", "'fr7630006000011234567890189'")]
    [InlineData("Phone", "'0612345678'")]
    [InlineData("Slug", "'my--slug'")]
    [InlineData("Ticket", "'abc-1234'")]
    [InlineData("Ticket", "'ABC-123'")]
    public async Task Another_writers_row_breaking_a_pattern_is_refused_by_the_engine(string column, string literal)
    {
        await EnsureTablesAsync();
        await using var context = CreateContext<PatternContext>();
        var insert = $"INSERT INTO {Q("pattern_values")} ({Q("Id")}, {Q(column)}) VALUES ({Next()}, {literal})";

        var write = () => context.Database.ExecuteSqlRawAsync(insert, TestContext.Current.CancellationToken);

        Refused((await write.Should().ThrowAsync<DbException>()).Which, $"CK_pattern_values_{column}_Pattern");
    }

    /// <summary>Gets the query matching the parameter <c>@value</c> against a pattern, as 1 or 0.</summary>
    /// <param name="pattern">The pattern, as a literal of the dialect.</param>
    /// <returns>The query.</returns>
    protected abstract string MatchSql(string pattern);

    /// <summary>Asserts that the engine refused a row on the constraint named, as the provider reports it.</summary>
    /// <param name="refusal">What the provider threw.</param>
    /// <param name="constraint">The constraint.</param>
    protected abstract void Refused(DbException refusal, string constraint);

    /// <summary>Runs what the engine needs before the table is created.</summary>
    /// <param name="context">A context bound to the engine.</param>
    /// <returns>A task that completes once it ran.</returns>
    protected virtual Task PrepareAsync(DbContext context) => Task.CompletedTask;

    /// <summary>Creates a context bound to the running container.</summary>
    /// <typeparam name="TContext">The context.</typeparam>
    /// <returns>A new context.</returns>
    protected TContext CreateContext<TContext>()
        where TContext : DbContext
        => Create<TContext>(fixture);

    /// <summary>Creates the table once per fixture.</summary>
    /// <returns>A task that completes once the table exists.</returns>
    protected Task EnsureTablesAsync() => Tables.GetValue(fixture, database => CreateTablesAsync(database));

    /// <summary>Gets a key no row of the table holds yet.</summary>
    /// <returns>The key.</returns>
    protected static int Next() => Interlocked.Increment(ref _lastId);

    /// <summary>Quotes an identifier as the engine does.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The quoted identifier.</returns>
    protected string Q(string identifier) => fixture.Quote(identifier);

    private static TContext Create<TContext>(DatabaseFixture database)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        database.Configure(builder, database.ConnectionString);

        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }

    private async Task CreateTablesAsync(DatabaseFixture database)
    {
        await using var context = Create<PatternContext>(database);
        await PrepareAsync(context);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    /// <summary>Value objects with a pattern, each optional, so that a row sets one at a time.</summary>
    protected sealed class PatternValues
    {
        public int Id { get; set; }

        public Iban? Iban { get; set; }

        public PhoneNumber? Phone { get; set; }

        public Slug? Slug { get; set; }

        public TicketCode? Ticket { get; set; }

        public PaymentId? Payment { get; set; }
    }

    /// <summary>The values, mapped by the conventions, with the check constraints of their patterns.</summary>
    /// <param name="options">Options bound to the running container.</param>
    protected sealed class PatternContext(DbContextOptions<PatternContext> options) : DbContext(options)
    {
        public DbSet<PatternValues> Rows => Set<PatternValues>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder
                .ConfigureValueObjects(typeof(Iban).Assembly, typeof(Slug).Assembly)
                .ConfigureEntityIds(typeof(PaymentId).Assembly)
                .AddValueObjectCheckConstraints(checks => checks.Patterns = Database.IsNpgsql()
                    ? ValueObjectPatternDialect.PostgreSql
                    : ValueObjectPatternDialect.SqlServer2025);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PatternValues>(values =>
            {
                values.ToTable("pattern_values");
                values.Property(static entity => entity.Id).ValueGeneratedNever();
                if (Database.IsNpgsql())
                {
                    // A nondeterministic collation, which PostgreSQL's ~ refuses unless the constraint sets another.
                    values.Property(static entity => entity.Slug).UseCollation(CaseInsensitive);
                }
            });
    }

    /// <summary>The case-insensitive collation of PostgreSQL the guide recommends for a key compared ignoring case.</summary>
    protected const string CaseInsensitive = "case_insensitive";
}

/// <summary>Runs the pattern constraints against PostgreSQL.</summary>
/// <param name="fixture">PostgreSQL container.</param>
public sealed class PostgreSqlCheckConstraintPatternTests(PostgreSqlFixture fixture)
    : CheckConstraintPatternTests<PostgreSqlFixture>(fixture)
{
    /// <inheritdoc />
    protected override ValueObjectPatternDialect Dialect => ValueObjectPatternDialect.PostgreSql;

    /// <summary>
    /// A column under a nondeterministic collation, which PostgreSQL's <c>~</c> refuses with
    /// <c>0A000: nondeterministic collations are not supported for regular expressions</c>, still takes a value: the
    /// constraint matches it under the C collation.
    /// </summary>
    [Fact]
    public async Task A_column_under_a_nondeterministic_collation_takes_a_value_its_pattern_accepts()
    {
        await EnsureTablesAsync();

        await using var context = CreateContext<PatternContext>();
        context.Rows.Add(new PatternValues { Id = Next(), Slug = Slug.Create("under-a-collation") });

        (await context.SaveChangesAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <inheritdoc />
    protected override string MatchSql(string pattern) => $"SELECT CASE WHEN @value COLLATE \"C\" ~ {pattern} THEN 1 ELSE 0 END";

    /// <inheritdoc />
    protected override void Refused(DbException refusal, string constraint)
    {
        var postgres = refusal.Should().BeOfType<PostgresException>().Which;
        postgres.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        postgres.ConstraintName.Should().Be(constraint);
    }

    /// <inheritdoc />
    protected override async Task PrepareAsync(DbContext context)
    {
        var collation = $"CREATE COLLATION IF NOT EXISTS {CaseInsensitive} (provider = icu, locale = 'und-u-ks-level2', deterministic = false)";
        await context.Database.ExecuteSqlRawAsync(collation);
    }
}

/// <summary>Runs the pattern constraints against SQL Server 2025.</summary>
/// <param name="fixture">SQL Server 2025 container.</param>
public sealed class SqlServer2025CheckConstraintPatternTests(SqlServer2025Fixture fixture)
    : CheckConstraintPatternTests<SqlServer2025Fixture>(fixture)
{
    /// <inheritdoc />
    protected override ValueObjectPatternDialect Dialect => ValueObjectPatternDialect.SqlServer2025;

    /// <inheritdoc />
    protected override string MatchSql(string pattern) => $"SELECT CASE WHEN REGEXP_LIKE(@value, {pattern}) THEN 1 ELSE 0 END";

    /// <inheritdoc />
    protected override void Refused(DbException refusal, string constraint)
    {
        var sqlServer = refusal.Should().BeOfType<SqlException>().Which;
        sqlServer.Number.Should().Be(547);
        sqlServer.Message.Should().Contain($"CHECK constraint \"{constraint}\"");
    }
}
