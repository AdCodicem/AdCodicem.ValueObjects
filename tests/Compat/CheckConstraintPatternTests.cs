using System.Data.Common;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The patterns of the value objects, written as CHECK constraints in PostgreSQL's dialect by the package built against
/// the current major, in a model of Npgsql's provider for the next one, a violated one carrying the code of its rule.
/// </summary>
/// <param name="fixture">PostgreSQL container.</param>
[Trait("Requires", "Docker")]
public sealed class CheckConstraintPatternTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task A_pattern_constraint_takes_the_applications_row_and_refuses_another_writers()
    {
        await using var context = new PatternedContext(Options<PatternedContext>());
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(TestContext.Current.CancellationToken);
        context.Accounts.Add(new PatternedAccount { Id = 1, Iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189") });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var write = () => context.Database.ExecuteSqlRawAsync(
            """INSERT INTO "patterned_accounts" ("Id", "Iban") VALUES (2, 'fr7630006000011234567890189')""",
            TestContext.Current.CancellationToken);

        var refusal = (await write.Should().ThrowAsync<PostgresException>()).Which;
        refusal.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        refusal.ConstraintName.Should().Be("CK_patterned_accounts_Iban_Pattern");
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// A refusal in the second statement of a batch, an <c>INSERT … RETURNING</c> reading back the key PostgreSQL
    /// generates, reaches no command interceptor under the next major either: the save interceptor adds the code.
    /// </summary>
    [Fact]
    public async Task A_refusal_in_a_later_statement_of_a_batch_carries_the_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var recorder = new FailureRecorder();
        await using var context = new EntriesContext(
            new DbContextOptionsBuilder<EntriesContext>().UseNpgsql(fixture.ConnectionString).AddInterceptors(recorder).UseValueObjectCheckErrors().Options);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(ct);
        context.Entries.AddRange(
            new PatternedEntry { Iban = Iban.Create("FR7630006000011234567890189") },
            new PatternedEntry { Iban = Iban.CreateUnchecked("fr7630006000011234567890189") });

        var save = () => context.SaveChangesAsync(ct);

        var update = (await save.Should().ThrowAsync<DbUpdateException>()).Which;
        recorder.Failures.Should().Be(0);
        var refusal = update.InnerException.Should().BeOfType<PostgresException>().Which;
        refusal.ConstraintName.Should().Be("CK_patterned_entries_Iban_Pattern");
        ValueObjectErrors.TryGetCode(update, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    [Fact]
    public void A_pattern_the_dialect_reads_apart_fails_the_model_unless_its_value_object_is_left_out()
    {
        using var refused = new MailedContext(Options<MailedContext>());
        using var excluded = new ExcludedMailedContext(Options<ExcludedMailedContext>());

        var build = () => refused.Model;

        build.Should().Throw<InvalidOperationException>().WithMessage("*pattern of EmailAddress for PostgreSql*ExcludePattern<EmailAddress>()*");
        excluded.Model.FindEntityType(typeof(MailedAccount)).Should().NotBeNull();
    }

    private DbContextOptions<TContext> Options<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().UseNpgsql(fixture.ConnectionString).UseValueObjectCheckErrors().Options;

    /// <summary>Counts the errors Entity Framework Core hands a command interceptor.</summary>
    private sealed class FailureRecorder : DbCommandInterceptor
    {
        public int Failures { get; private set; }

        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Failures++;

        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Failures++;

            return Task.CompletedTask;
        }
    }
}

/// <summary>An entry, whose key PostgreSQL generates and whose IBAN's pattern it checks.</summary>
public sealed class PatternedEntry
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public Iban Iban { get; set; }
}

/// <summary>The entries, with the patterns written as CHECK constraints in PostgreSQL's dialect.</summary>
/// <param name="options">Options.</param>
public sealed class EntriesContext(DbContextOptions<EntriesContext> options) : DbContext(options)
{
    /// <summary>Gets the entries.</summary>
    public DbSet<PatternedEntry> Entries => Set<PatternedEntry>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder
            .ConfigureValueObjects(typeof(Iban).Assembly)
            .AddValueObjectCheckConstraints(checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<PatternedEntry>().ToTable("patterned_entries");
}

/// <summary>An account, whose IBAN's pattern the database checks.</summary>
public sealed class PatternedAccount
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public Iban Iban { get; set; }
}

/// <summary>An account reached by email, whose pattern PostgreSQL would read apart from .NET.</summary>
public sealed class MailedAccount
{
    /// <summary>Gets or sets the key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the address.</summary>
    public EmailAddress Email { get; set; }
}

/// <summary>The accounts, with the patterns written as CHECK constraints in PostgreSQL's dialect.</summary>
/// <param name="options">Options.</param>
public sealed class PatternedContext(DbContextOptions<PatternedContext> options) : DbContext(options)
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<PatternedAccount> Accounts => Set<PatternedAccount>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder
            .ConfigureValueObjects(typeof(Iban).Assembly)
            .AddValueObjectCheckConstraints(checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<PatternedAccount>(account =>
        {
            account.ToTable("patterned_accounts");
            account.Property(entity => entity.Id).ValueGeneratedNever();
        });
}

/// <summary>The accounts reached by email, with patterns in PostgreSQL's dialect.</summary>
/// <param name="options">Options.</param>
public class MailedContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<MailedAccount> Accounts => Set<MailedAccount>();

    /// <summary>Gets a value indicating whether the email address's pattern is left out.</summary>
    protected virtual bool Excluded => false;

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder
            .ConfigureValueObjects(typeof(Iban).Assembly)
            .AddValueObjectCheckConstraints(checks =>
            {
                checks.Patterns = ValueObjectPatternDialect.PostgreSql;
                if (Excluded)
                {
                    checks.ExcludePattern<EmailAddress>();
                }
            });
}

/// <summary>The same model, the email address's pattern left out.</summary>
/// <param name="options">Options.</param>
public sealed class ExcludedMailedContext(DbContextOptions options) : MailedContext(options)
{
    /// <inheritdoc />
    protected override bool Excluded => true;
}
