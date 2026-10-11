using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.UnitTests.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// The CHECK constraints <c>AddValueObjectCheckConstraints</c> writes for SQL Server, read off a model, which needs no
/// server: the unit suite references no SQL Server provider. <see cref="SqlServerCheckConstraintTests"/> runs them.
/// </summary>
public sealed class SqlServerCheckConstraintScriptTests
{
    /// <summary>
    /// SQL Server: a minimum length counted in UTF-16 code units on a Unicode column, <c>DATALENGTH / 2</c>, which counts a
    /// trailing space where <c>LEN</c> does not, and in bytes on another; characters compared under a binary collation;
    /// the bounds of every type as the provider writes its literals, an instant with its offset included. Left out: a
    /// duration outside a day, which <c>time</c> holds without its days; a decimal bound finer than the column's scale,
    /// <c>decimal(18,2)</c> by default; an instant's bound finer than the column's precision, and any on <c>datetime</c> and
    /// <c>smalldatetime</c>, which round to fractions no tick count states.
    /// </summary>
    [Fact]
    public void SQL_Server_gets_each_rule_it_compares_as_dotnet_does_and_none_of_the_others()
    {
        using var context = new ScriptedContext();

        var checks = context.GetService<IDesignTimeModel>().Model.GetRelationalModel()
            .FindTable("scripted", null)!.CheckConstraints.ToDictionary(static check => check.Name!, static check => check.Sql);

        checks.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CK_scripted_Adjustment_Maximum"] = "[Adjustment] <= CAST(10 AS smallint)",
            ["CK_scripted_Adjustment_Minimum"] = "[Adjustment] >= CAST(-10 AS smallint)",
            ["CK_scripted_AsciiCode_MinLength"] = "DATALENGTH([AsciiCode]) >= 5",
            ["CK_scripted_Code_MinLength"] = "DATALENGTH([Code]) / 2 >= 5",
            ["CK_scripted_Counter_Maximum"] = "[Counter] <= 18446744073709551614.0",
            ["CK_scripted_Counter_Minimum"] = "[Counter] >= 1.0",
            ["CK_scripted_Country_KnownValues"] = "[Country] IN (N'FR', N'BE', N'LU', N'DE')",
            ["CK_scripted_Country_MinLength"] = "DATALENGTH([Country]) / 2 >= 2",
            ["CK_scripted_Deadline_Maximum"] = "[Deadline] <= '2030-01-01T00:00:00.0000000-05:00'",
            ["CK_scripted_Deadline_Minimum"] = "[Deadline] >= '2000-01-01T00:00:00.0000000+02:00'",
            ["CK_scripted_Duration_Minimum"] = "[Duration] >= '00:00:00'",
            ["CK_scripted_Effective_Minimum"] = "[Effective] >= '2000-01-01'",
            ["CK_scripted_Factor_KnownValues"] = "[Factor] IN (0.10000000149011612E0, 0.5E0)",
            ["CK_scripted_FeeInFourDecimals_Maximum"] = "[FeeInFourDecimals] <= 9.995",
            ["CK_scripted_FeeInFourDecimals_Minimum"] = "[FeeInFourDecimals] >= 0.001",
            ["CK_scripted_Fraction_Maximum"] = "[Fraction] <= 0.69999998807907104E0",
            ["CK_scripted_Fraction_Minimum"] = "[Fraction] >= 0.10000000149011612E0",
            ["CK_scripted_Grade_Maximum"] = "[Grade] COLLATE Latin1_General_BIN2 <= N'F'",
            ["CK_scripted_Grade_Minimum"] = "[Grade] COLLATE Latin1_General_BIN2 >= N'A'",
            ["CK_scripted_Iban_MinLength"] = "DATALENGTH([Iban]) / 2 >= 15",
            ["CK_scripted_Latitude_Maximum"] = "[Latitude] <= 90.0E0",
            ["CK_scripted_Latitude_Minimum"] = "[Latitude] >= -90.0E0",
            ["CK_scripted_Milestone_KnownValues"] = "[Milestone] IN ('2020-01-01T00:00:00.0000000+00:00')",
            ["CK_scripted_Opening_Maximum"] = "[Opening] <= '12:00:00'",
            ["CK_scripted_Opening_Minimum"] = "[Opening] >= '06:00:00'",
            ["CK_scripted_Payment_MinLength"] = "DATALENGTH([Payment]) >= 25",
            ["CK_scripted_Port_Minimum"] = "[Port] >= 1",
            ["CK_scripted_Rate_KnownValues"] = "[Rate] IN (0.0, 0.5, 1.0)",
            ["CK_scripted_Recorded_Maximum"] = "[Recorded] <= '2099-12-31T00:00:00.0000000'",
            ["CK_scripted_Recorded_Minimum"] = "[Recorded] >= '2000-01-01T00:00:00.0000000'",
            ["CK_scripted_Score_Maximum"] = "[Score] <= CAST(100 AS tinyint)",
            ["CK_scripted_Stamp_Minimum"] = "[Stamp] >= '2000-01-01T00:00:00.0000001'",
            ["CK_scripted_Unit_KnownValues"] = "[Unit] IN (N'KG', N'L')",
        });
    }

    /// <summary>
    /// SQL Server keeps a constraint name per schema, so a name another table of the schema would take too ends with the
    /// hash of its table and its column, the rule kept whole: two tables whose names read alike once joined with a
    /// column's, <c>clash_a_b</c> with <c>C</c> and <c>clash_a</c> with <c>B_C</c>, a complex property's column and a table
    /// named after it, and a table whose name the application gave a constraint of another table. A table of another
    /// schema keeps its plain name. The script creates them all, as a migration does.
    /// </summary>
    [Fact]
    public void A_name_another_table_of_the_schema_takes_ends_with_a_hash()
    {
        using var context = new ClashContext();
        var model = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        Name(model, "clash_a_b").Should().MatchRegex("^CK_clash_a_b_C_Maximum_[0-9A-F]{8}$");
        Name(model, "clash_a").Should().MatchRegex("^CK_clash_a_B_C_Maximum_[0-9A-F]{8}$");
        Name(model, "clash_orders").Should().MatchRegex("^CK_clash_orders_Line_Score_Maximum_[0-9A-F]{8}$");
        Name(model, "clash_orders_Line").Should().MatchRegex("^CK_clash_orders_Line_Score_Maximum_[0-9A-F]{8}$");
        Name(model, "clash_declared").Should().MatchRegex("^CK_clash_declared_Score_Maximum_[0-9A-F]{8}$");
        Name(model, "clash_a_b", "archive").Should().Be("CK_clash_a_b_C_Maximum");
        var names = model.Tables.Where(static table => table.Schema is null).SelectMany(static table => table.CheckConstraints)
            .Select(static check => check.Name).ToList();
        names.Should().HaveCount(6).And.OnlyHaveUniqueItems().And.Contain("CK_clash_declared_Score_Maximum");

        var script = context.Database.GenerateCreateScript();
        foreach (var name in names)
        {
            script.Should().Contain($"CONSTRAINT [{name}] CHECK (");
        }

        script.Should().Contain("CONSTRAINT [CK_clash_a_b_C_Maximum] CHECK ([C] <= CAST(100 AS tinyint))");

        static string Name(IRelationalModel model, string table, string? schema = null)
            => model.FindTable(table, schema)!.CheckConstraints.Should().ContainSingle().Subject.Name!;
    }

    /// <summary>A value object of each kind the rules concern, each in the column type it is checked against.</summary>
    private sealed class Scripted
    {
        public int Id { get; set; }

        public Iban Iban { get; set; }

        public TrackingCode Code { get; set; }

        public TrackingCode AsciiCode { get; set; }

        public PaymentId Payment { get; set; }

        public Grade Grade { get; set; }

        public Adjustment Adjustment { get; set; }

        public Score Score { get; set; }

        public Port Port { get; set; }

        public Counter Counter { get; set; }

        public Fraction Fraction { get; set; }

        public Latitude Latitude { get; set; }

        public Fee Fee { get; set; }

        public Fee FeeInFourDecimals { get; set; }

        public OpeningTime Opening { get; set; }

        public EffectiveDate Effective { get; set; }

        public RecordedAt Recorded { get; set; }

        public RecordedAt RecordedAsDatetime { get; set; }

        public RecordedAt RecordedAsSmalldatetime { get; set; }

        public Stamp Stamp { get; set; }

        public Stamp StampInMilliseconds { get; set; }

        public Deadline Deadline { get; set; }

        public Duration Duration { get; set; }

        public Window Window { get; set; }

        public CountryCode Country { get; set; }

        public MeasureUnit Unit { get; set; }

        public Rate Rate { get; set; }

        public Factor Factor { get; set; }

        public Milestone Milestone { get; set; }
    }

    /// <summary>The values, on SQL Server, with the check constraints.</summary>
    private sealed class ScriptedContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlServer("Server=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder
                .ConfigureValueObjects(typeof(Iban).Assembly, typeof(Grade).Assembly)
                .ConfigureEntityIds(typeof(PaymentId).Assembly)
                .AddValueObjectCheckConstraints();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Scripted>(scripted =>
            {
                scripted.ToTable("scripted");
                scripted.Property(static entity => entity.AsciiCode).IsUnicode(false);
                scripted.Property(static entity => entity.FeeInFourDecimals).HasPrecision(18, 4);
                scripted.Property(static entity => entity.RecordedAsDatetime).HasColumnType("datetime");
                scripted.Property(static entity => entity.RecordedAsSmalldatetime).HasColumnType("smalldatetime");
                scripted.Property(static entity => entity.StampInMilliseconds).HasPrecision(3);
            });
    }

    /// <summary>A score in column <c>C</c> of table <c>clash_a_b</c>.</summary>
    private sealed class ClashAB
    {
        public int Id { get; set; }

        public Score C { get; set; }
    }

    /// <summary>A score in column <c>B_C</c> of table <c>clash_a</c>, whose constraint reads as <see cref="ClashAB"/>'s.</summary>
    private sealed class ClashA
    {
        public int Id { get; set; }

        public Score BC { get; set; }
    }

    /// <summary>An order whose complex line has its score in column <c>Line_Score</c>.</summary>
    private sealed class ClashOrder
    {
        public int Id { get; set; }

        public ClashLine Line { get; set; } = new();
    }

    private sealed class ClashLine
    {
        public Score Score { get; set; }
    }

    /// <summary>A score in table <c>clash_orders_Line</c>, whose constraint reads as <see cref="ClashOrder"/>'s.</summary>
    private sealed class ClashOrderLine
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>A score whose constraint takes the name the application gave a constraint of another table.</summary>
    private sealed class ClashDeclared
    {
        public int Id { get; set; }

        public Score Score { get; set; }
    }

    /// <summary>The table that holds the constraint of the application's own.</summary>
    private sealed class ClashDeclarer
    {
        public int Id { get; set; }

        public int Count { get; set; }
    }

    /// <summary>A score in column <c>C</c> of a table of another schema, named as <see cref="ClashAB"/>'s.</summary>
    private sealed class ClashArchived
    {
        public int Id { get; set; }

        public Score C { get; set; }
    }

    /// <summary>Tables whose constraint names read alike, on SQL Server.</summary>
    private sealed class ClashContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlServer("Server=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Grade).Assembly).AddValueObjectCheckConstraints();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClashAB>().ToTable("clash_a_b");
            modelBuilder.Entity<ClashA>(clash =>
            {
                clash.ToTable("clash_a");
                clash.Property(static entity => entity.BC).HasColumnName("B_C");
            });
            modelBuilder.Entity<ClashOrder>(order =>
            {
                order.ToTable("clash_orders");
                order.ComplexProperty(static entity => entity.Line);
            });
            modelBuilder.Entity<ClashOrderLine>().ToTable("clash_orders_Line");
            modelBuilder.Entity<ClashDeclared>().ToTable("clash_declared");
            modelBuilder.Entity<ClashDeclarer>().ToTable(
                "clash_declarer",
                static table => table.HasCheckConstraint("CK_clash_declared_Score_Maximum", "[Count] >= 0"));
            modelBuilder.Entity<ClashArchived>().ToTable("clash_a_b", "archive");
        }
    }
}
