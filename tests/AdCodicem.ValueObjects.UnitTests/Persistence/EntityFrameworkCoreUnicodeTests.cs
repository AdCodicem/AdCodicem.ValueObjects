using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The Unicode facet the Entity Framework Core conventions and per-property mapping give a value object declaring
/// <c>Unicode = false</c>, which SQL Server reads as <c>varchar</c>.
/// </summary>
/// <remarks>
/// The models are built on Npgsql and SQLite, which open no connection to build one and store text alike whatever the
/// facet: what SQL Server makes of it, the columns, the parameters and the CHECK constraints, is checked in the
/// integration suite. Entity Framework Core builds a model once per context type, so each shape has a context type of its
/// own.
/// </remarks>
public sealed class EntityFrameworkCoreUnicodeTests
{
    /// <summary>
    /// <c>ConfigureValueObjects</c> maps a value object declaring its text non-Unicode, and its optional form, to
    /// non-Unicode text, beside its length, and leaves every other value object to the provider. PostgreSQL stores both
    /// alike.
    /// </summary>
    [Fact]
    public void The_convention_maps_a_value_object_declaring_its_text_non_Unicode_to_non_Unicode_text()
    {
        using var context = new RemittanceContext();
        var remittance = DesignTimeModel(context).FindEntityType(typeof(Remittance))!;

        var bic = remittance.FindProperty(nameof(Remittance.Bic))!;
        bic.IsUnicode().Should().BeFalse("the value object declares Unicode = false");
        bic.GetMaxLength().Should().Be(11);
        bic.GetColumnType().Should().Be("character varying(11)", "PostgreSQL has no distinct non-Unicode text");
        remittance.FindProperty(nameof(Remittance.Intermediary))!.IsUnicode().Should().BeFalse("the optional property takes the facet too");

        var iban = remittance.FindProperty(nameof(Remittance.Iban))!;
        iban.IsUnicode().Should().BeNull("a value object that declares nothing leaves the column to the provider");
        iban.GetColumnType().Should().Be("character varying(34)");
        remittance.FindProperty(nameof(Remittance.Label))!.IsUnicode().Should().BeNull();
    }

    /// <summary>
    /// SQLite stores every text as <c>TEXT</c>, whatever the facet, so a model declaring the option serves it as it is.
    /// </summary>
    [Fact]
    public void On_SQLite_a_non_Unicode_value_object_is_text_as_any_other()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = new SqliteRemittanceContext(connection);
        var remittance = DesignTimeModel(context).FindEntityType(typeof(Remittance))!;

        var bic = remittance.FindProperty(nameof(Remittance.Bic))!;
        bic.IsUnicode().Should().BeFalse();
        bic.GetColumnType().Should().Be("TEXT");
    }

    /// <summary>
    /// Each construction of a generic value object declaring the option gets the facet from the convention that closes its
    /// converter over it, as the model meets it, and a construction of one that does not declare it stays Unicode. The
    /// constructions are this test's own, which nothing has resolved before its model is built.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_declaring_the_option_is_non_Unicode_text()
    {
        ValueObjectRegistry.TryGet(typeof(AsciiReference<Consignment>), out _).Should().BeFalse("only the convention meets it");

        using var context = new ConsignmentContext();
        var consignment = DesignTimeModel(context).FindEntityType(typeof(Consignment))!;

        var reference = consignment.FindProperty(nameof(Consignment.Reference))!;
        reference.GetValueConverter().Should().BeOfType<ValueObjectConverter<AsciiReference<Consignment>, string>>();
        reference.IsUnicode().Should().BeFalse();
        reference.GetMaxLength().Should().Be(12);
        consignment.FindProperty(nameof(Consignment.Return))!.IsUnicode().Should().BeFalse();
        consignment.FindProperty(nameof(Consignment.Order))!.IsUnicode().Should().BeNull();
    }

    /// <summary>
    /// The elements of a collection of value objects declaring the option are non-Unicode text, an optional element and
    /// a construction of a generic one included, which SQL Server reads its JSON as; PostgreSQL keeps an array of its
    /// element's column type.
    /// </summary>
    [Fact]
    public void The_elements_of_a_collection_of_value_objects_declaring_the_option_are_non_Unicode_text()
    {
        using var context = new RemittanceContext();
        var remittance = DesignTimeModel(context).FindEntityType(typeof(Remittance))!;

        Element(remittance, nameof(Remittance.Correspondents)).IsUnicode().Should().BeFalse();
        Element(remittance, nameof(Remittance.OptionalCorrespondents)).IsUnicode().Should().BeFalse();
        Element(remittance, nameof(Remittance.References)).IsUnicode().Should().BeFalse();
        Element(remittance, nameof(Remittance.Ibans)).IsUnicode().Should().BeNull();
        remittance.FindProperty(nameof(Remittance.Correspondents))!.GetColumnType().Should().Be("character varying(11)[]");
    }

    /// <summary>
    /// A value object declaring the option and no length is non-Unicode text of no length: <c>varchar(max)</c> on SQL
    /// Server, <c>text</c> on PostgreSQL.
    /// </summary>
    [Fact]
    public void A_value_object_declaring_the_option_and_no_length_is_non_Unicode_text_of_no_length()
    {
        using var context = new RemittanceContext();
        var tag = DesignTimeModel(context).FindEntityType(typeof(Remittance))!.FindProperty(nameof(Remittance.Tag))!;

        tag.IsUnicode().Should().BeFalse();
        tag.GetMaxLength().Should().BeNull();
        tag.GetColumnType().Should().Be("text");
    }

    /// <summary>
    /// The per-property mapping reads the facet off the type as it reads the length, a value object written by hand
    /// included, and leaves a value object that declares nothing to the provider.
    /// </summary>
    [Fact]
    public void The_per_property_mapping_reads_the_facet_off_the_type()
    {
        using var context = new ExplicitContext();
        var entity = DesignTimeModel(context).FindEntityType(typeof(ExplicitRemittance))!;

        entity.FindProperty(nameof(ExplicitRemittance.Bic))!.IsUnicode().Should().BeFalse();
        entity.FindProperty(nameof(ExplicitRemittance.Declared))!.IsUnicode().Should().BeFalse();
        entity.FindProperty(nameof(ExplicitRemittance.Declared))!.GetMaxLength().Should().Be(20);
        entity.FindProperty(nameof(ExplicitRemittance.Iban))!.IsUnicode().Should().BeNull();
    }

    /// <summary>
    /// The convention reads the facet off the descriptor, as it reads the length: a registration made by hand declaring
    /// it maps non-Unicode text a value object whose type declares Unicode text, and a value object written by hand,
    /// described by reflection once resolved, is mapped as its schema states. Both are this test's own, since the registry
    /// is shared by the whole process.
    /// </summary>
    [Fact]
    public void The_convention_reads_the_facet_off_the_descriptor()
    {
        ValueObjectRegistry.Register<RegisteredMark, string>(new ValueObjectSchema { MaxLength = 12, IsUnicode = false });
        ValueObjectRegistry.TryResolve(typeof(Declared<RegisteredAsciiDeclaration, string>), out _).Should().BeTrue();

        using var context = new RegisteredContext();
        var entity = DesignTimeModel(context).FindEntityType(typeof(RegisteredRemittance))!;

        RegisteredMark.Schema.IsUnicode.Should().BeNull("the type declares Unicode text");
        entity.FindProperty(nameof(RegisteredRemittance.Mark))!.IsUnicode().Should().BeFalse();
        entity.FindProperty(nameof(RegisteredRemittance.Declared))!.IsUnicode().Should().BeFalse();
        entity.FindProperty(nameof(RegisteredRemittance.Declared))!.GetMaxLength().Should().Be(20);
    }

    /// <summary>
    /// A schema stating its text Unicode leaves the column to the provider, as one stating nothing does: through the
    /// convention, for a construction of a generic value object registered by hand whose type declares the option, for the
    /// elements of a collection, and through the per-property mapping. Only <c>false</c> is acted on. The value objects
    /// are this test's own, since the registry is shared by the whole process.
    /// </summary>
    [Fact]
    public void A_schema_stating_Unicode_text_leaves_the_column_to_the_provider()
    {
        ValueObjectRegistry.TryResolve(typeof(Declared<RegisteredUnicodeDeclaration, string>), out _).Should().BeTrue();
        ValueObjectRegistry.Register<AsciiReference<Ledger>, string>(new ValueObjectSchema { MaxLength = 12, IsUnicode = true });

        using var context = new LedgerContext();
        var ledger = DesignTimeModel(context).FindEntityType(typeof(Ledger))!;

        var declared = ledger.FindProperty(nameof(Ledger.Declared))!;
        declared.IsUnicode().Should().BeNull();
        declared.GetMaxLength().Should().Be(20);
        ledger.FindProperty(nameof(Ledger.Reference))!.IsUnicode().Should().BeNull("the descriptor's schema wins over the type's");
        Element(ledger, nameof(Ledger.Entries)).IsUnicode().Should().BeNull();
        ledger.FindProperty(nameof(Ledger.Explicit))!.IsUnicode().Should().BeNull();
        ledger.FindProperty(nameof(Ledger.Explicit))!.GetMaxLength().Should().Be(20);
    }

    /// <summary>
    /// An application keeps Unicode text where it wants it: for every property of a value object with
    /// <c>Properties&lt;TSelf&gt;().AreUnicode(true)</c> after <c>ConfigureValueObjects</c>, or for one with
    /// <c>IsUnicode(true)</c>, which outranks a convention.
    /// </summary>
    [Fact]
    public void An_application_keeps_Unicode_text_where_it_says_so()
    {
        using var everyProperty = new UnicodeAgainContext();
        var remittance = DesignTimeModel(everyProperty).FindEntityType(typeof(Remittance))!;
        remittance.FindProperty(nameof(Remittance.Bic))!.IsUnicode().Should().BeTrue();
        remittance.FindProperty(nameof(Remittance.Intermediary))!.IsUnicode().Should().BeTrue("the optional property takes the configuration of the value object");

        using var oneProperty = new OnePropertyUnicodeContext();
        remittance = DesignTimeModel(oneProperty).FindEntityType(typeof(Remittance))!;
        remittance.FindProperty(nameof(Remittance.Bic))!.IsUnicode().Should().BeTrue();
        remittance.FindProperty(nameof(Remittance.Intermediary))!.IsUnicode().Should().BeFalse();
    }

    private static IModel DesignTimeModel(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static IReadOnlyElementType Element(IReadOnlyEntityType entity, string name)
        => entity.FindProperty(name)!.GetElementType()!;

    /// <summary>A payment order, holding value objects declaring their text non-Unicode in each shape, and others.</summary>
    private sealed class Remittance
    {
        public int Id { get; set; }

        public Bic Bic { get; set; }

        public Bic? Intermediary { get; set; }

        public Iban Iban { get; set; }

        public Label Label { get; set; }

        public AsciiTag Tag { get; set; }

        public List<Bic> Correspondents { get; set; } = [];

        public List<Bic?> OptionalCorrespondents { get; set; } = [];

        public List<AsciiReference<Remittance>> References { get; set; } = [];

        public List<Iban> Ibans { get; set; } = [];
    }

    /// <summary>A consignment, holding constructions of generic value objects no other test resolves.</summary>
    private sealed class Consignment
    {
        public int Id { get; set; }

        public AsciiReference<Consignment> Reference { get; set; }

        public AsciiReference<Consignment>? Return { get; set; }

        public Reference<Consignment> Order { get; set; }
    }

    /// <summary>The consignment, on PostgreSQL, whose model only the generic construction's test builds.</summary>
    private sealed class ConsignmentContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Consignment>();
    }

    /// <summary>The payment order, on PostgreSQL.</summary>
    private class RemittanceContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Remittance>();
    }

    /// <summary>The payment order, on SQLite.</summary>
    /// <param name="connection">An in-memory database, never opened.</param>
    private sealed class SqliteRemittanceContext(SqliteConnection connection) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite(connection);

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Remittance>();
    }

    /// <summary>The payment order, Unicode again for every property of a BIC.</summary>
    private sealed class UnicodeAgainContext : RemittanceContext
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<Bic>().AreUnicode(true);
        }
    }

    /// <summary>The payment order, Unicode again for one property.</summary>
    private sealed class OnePropertyUnicodeContext : RemittanceContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Remittance>().Property(static remittance => remittance.Bic).IsUnicode(true);
    }

    /// <summary>A payment order whose value objects are mapped one property at a time.</summary>
    private sealed class ExplicitRemittance
    {
        public int Id { get; set; }

        public Bic Bic { get; set; }

        public Declared<AsciiDeclaration, string> Declared { get; set; }

        public Iban Iban { get; set; }
    }

    /// <summary>The per-property mapping, without the convention.</summary>
    private sealed class ExplicitContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitRemittance>(remittance =>
            {
                remittance.Property(static entity => entity.Bic).HasValueObjectConversion<Bic, string>();
                remittance.Property(static entity => entity.Declared).HasValueObjectConversion<Declared<AsciiDeclaration, string>, string>();
                remittance.Property(static entity => entity.Iban).HasValueObjectConversion<Iban, string>();
            });
    }

    /// <summary>A ledger holding value objects whose schemas state their text Unicode.</summary>
    private sealed class Ledger
    {
        public int Id { get; set; }

        public Declared<RegisteredUnicodeDeclaration, string> Declared { get; set; }

        public AsciiReference<Ledger> Reference { get; set; }

        public List<Declared<RegisteredUnicodeDeclaration, string>> Entries { get; set; } = [];

        public Declared<UnicodeDeclaration, string> Explicit { get; set; }
    }

    /// <summary>The ledger, mapped by the convention and, for one property, by hand.</summary>
    private sealed class LedgerContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(typeof(Bic).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>()
                .Property(static ledger => ledger.Explicit)
                .HasValueObjectConversion<Declared<UnicodeDeclaration, string>, string>();
    }

    /// <summary>A payment order holding value objects registered by hand.</summary>
    private sealed class RegisteredRemittance
    {
        public int Id { get; set; }

        public RegisteredMark Mark { get; set; }

        public Declared<RegisteredAsciiDeclaration, string> Declared { get; set; }
    }

    /// <summary>A model mapping every value object already registered.</summary>
    private sealed class RegisteredContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<RegisteredRemittance>();
    }
}
