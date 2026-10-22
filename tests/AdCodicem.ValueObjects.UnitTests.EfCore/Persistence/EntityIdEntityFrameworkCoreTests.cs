using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The column the Entity Framework Core mapping of entity identifiers asks for: fixed width, non-Unicode, and in
/// the collation given.
/// </summary>
/// <remarks>
/// That the engines create such a column, and sort it in minting order, is checked in the integration suite.
/// </remarks>
public class EntityIdEntityFrameworkCoreTests
{
    [Fact]
    public void Identifiers_mapped_by_convention_are_fixed_width_non_unicode_and_take_the_collation_given()
    {
        var account = DesignTimeModel(new CollatedContext())
            .FindEntityType(typeof(Ledger))!
            .FindProperty(nameof(Ledger.Account))!;

        account.GetValueConverter().Should().BeOfType<ValueObjectConverter<AccountId, string>>();
        account.GetValueComparer().Should().BeOfType<ValueObjectComparer<AccountId>>();
        account.GetMaxLength().Should().Be(AccountId.Length);
        account.IsFixedLength().Should().BeTrue();
        account.IsUnicode().Should().BeFalse();
        account.GetCollation().Should().Be(IdCollations.PostgreSql);
    }

    /// <summary>
    /// An optional identifier takes the column of the identifier, and a converter of its own, which stores an identifier
    /// that never went through <c>New</c> or <c>Create</c> as <c>NULL</c> where a required one refuses it.
    /// </summary>
    [Fact]
    public void An_optional_identifier_takes_the_column_of_the_identifier_and_a_converter_storing_a_refused_one_as_NULL()
    {
        var replaced = DesignTimeModel(new CollatedContext())
            .FindEntityType(typeof(Ledger))!
            .FindProperty(nameof(Ledger.Replaced))!;

        replaced.GetValueConverter().Should().BeOfType<NullableValueObjectConverter<AccountId>>();
        replaced.GetMaxLength().Should().Be(AccountId.Length);
        replaced.IsFixedLength().Should().BeTrue();
        replaced.IsUnicode().Should().BeFalse();
        replaced.GetCollation().Should().Be(IdCollations.PostgreSql);
        replaced.IsNullable.Should().BeTrue();
    }

    /// <summary>
    /// An optional identifier is compared by a comparer of its nullable type, which the compiled model
    /// <c>dotnet ef dbcontext optimize</c> writes as it stands, where it cannot write the wrapping Entity Framework Core
    /// would otherwise give the comparer of the identifier.
    /// </summary>
    [Fact]
    public void An_optional_identifier_is_compared_by_a_comparer_a_compiled_model_can_write()
    {
        var replaced = DesignTimeModel(new CollatedContext())
            .FindEntityType(typeof(Ledger))!
            .FindProperty(nameof(Ledger.Replaced))!;

        replaced.FindAnnotation("ValueComparerType")!.Value.Should().Be(typeof(NullableValueObjectComparer<AccountId>));
        replaced.GetValueComparer().Should().BeOfType<NullableValueObjectComparer<AccountId>>();
    }

    [Fact]
    public void Identifiers_mapped_by_convention_keep_the_database_collation_when_none_is_given()
    {
        var ledger = DesignTimeModel(new DefaultContext()).FindEntityType(typeof(Ledger))!;
        var account = ledger.FindProperty(nameof(Ledger.Account))!;

        account.GetValueConverter().Should().BeOfType<ValueObjectConverter<AccountId, string>>();
        account.GetMaxLength().Should().Be(AccountId.Length);
        account.IsFixedLength().Should().BeTrue();
        account.GetCollation().Should().BeNull();

        ledger.FindProperty(nameof(Ledger.Event))!.GetMaxLength()
            .Should().Be(EventId.Length, "each identifier is sized from its own profile");
    }

    [Fact]
    public void An_identifier_mapped_one_by_one_is_fixed_width_non_unicode_and_takes_the_collation_given()
    {
        var builder = new ModelBuilder();
        builder.Entity<Ledger>(ledger =>
        {
            ledger.Property(entity => entity.Account).HasEntityIdConversion();
            ledger.Property(entity => entity.Event).HasEntityIdConversion(IdCollations.SqlServer);
        });

        var model = builder.Model.FindEntityType(typeof(Ledger))!;

        var account = model.FindProperty(nameof(Ledger.Account))!;
        account.GetValueConverter().Should().BeOfType<ValueObjectConverter<AccountId, string>>();
        account.GetValueComparer().Should().BeOfType<ValueObjectComparer<AccountId>>();
        account.GetMaxLength().Should().Be(AccountId.Length);
        account.IsFixedLength().Should().BeTrue();
        account.IsUnicode().Should().BeFalse();
        account.GetCollation().Should().BeNull();

        var eventId = model.FindProperty(nameof(Ledger.Event))!;
        eventId.GetMaxLength().Should().Be(EventId.Length);
        eventId.GetCollation().Should().Be(IdCollations.SqlServer);
    }

    /// <summary>
    /// Whichever call runs last sets the converter of the identifiers, so a context reading strictly says so to both:
    /// the identifiers are then validated on read like every other value object.
    /// </summary>
    [Fact]
    public void Identifiers_mapped_by_a_strict_convention_validate_what_they_read()
    {
        var ledger = DesignTimeModel(new StrictContext()).FindEntityType(typeof(Ledger))!;

        ledger.FindProperty(nameof(Ledger.Account))!.GetValueConverter()
            .Should().BeOfType<StrictValueObjectConverter<AccountId, string>>();
        ledger.FindProperty(nameof(Ledger.Event))!.GetValueConverter()
            .Should().BeOfType<StrictValueObjectConverter<EventId, string>>();
        ledger.FindProperty(nameof(Ledger.Replaced))!.GetValueConverter()
            .Should().BeOfType<StrictNullableValueObjectConverter<AccountId>>();
        ledger.FindProperty(nameof(Ledger.Account))!.IsFixedLength().Should().BeTrue();
    }

    [Fact]
    public void An_identifier_mapped_one_by_one_can_validate_what_it_reads()
    {
        var builder = new ModelBuilder();
        builder.Entity<Ledger>(ledger =>
        {
            ledger.Property(entity => entity.Account).HasEntityIdConversion(strict: true);
            ledger.Property(entity => entity.Event).HasEntityIdConversion(IdCollations.SqlServer, strict: true);
        });

        var model = builder.Model.FindEntityType(typeof(Ledger))!;

        model.FindProperty(nameof(Ledger.Account))!.GetValueConverter()
            .Should().BeOfType<StrictValueObjectConverter<AccountId, string>>();
        model.FindProperty(nameof(Ledger.Event))!.GetValueConverter()
            .Should().BeOfType<StrictValueObjectConverter<EventId, string>>();
        model.FindProperty(nameof(Ledger.Event))!.GetCollation().Should().Be(IdCollations.SqlServer);
    }

    /// <summary>
    /// An identifier written by hand and registered as an identifier alone, which the value object registry has never
    /// heard of, is mapped as a generated one is: the convention visits the descriptor the registry describes for it by
    /// reflection, under the JIT, the only place a model is built. The registry keeps that descriptor, as it keeps any
    /// it describes, so the identifier is registered there from then on, as a generated one always is.
    /// </summary>
    [Fact]
    public void An_identifier_registered_by_hand_as_an_identifier_alone_is_mapped_by_convention()
    {
        EntityIdRegistry.Register<HandWrittenId<ArchiveProfile>>();
        ValueObjectRegistry.TryGet(typeof(HandWrittenId<ArchiveProfile>), out _).Should().BeFalse();

        var archive = DesignTimeModel(new ArchiveContext()).FindEntityType(typeof(Archive))!;

        var reference = archive.FindProperty(nameof(Archive.Reference))!;
        reference.GetValueConverter().Should().BeOfType<ValueObjectConverter<HandWrittenId<ArchiveProfile>, string>>();
        reference.GetValueComparer().Should().BeOfType<ValueObjectComparer<HandWrittenId<ArchiveProfile>>>();
        reference.GetMaxLength().Should().Be(HandWrittenId<ArchiveProfile>.Length);
        reference.IsFixedLength().Should().BeTrue();
        reference.IsUnicode().Should().BeFalse();
        reference.GetCollation().Should().Be(IdCollations.PostgreSql);

        var previous = archive.FindProperty(nameof(Archive.Previous))!;
        previous.GetValueConverter().Should().BeOfType<NullableValueObjectConverter<HandWrittenId<ArchiveProfile>>>();
        previous.GetValueComparer().Should().BeOfType<NullableValueObjectComparer<HandWrittenId<ArchiveProfile>>>();
        previous.GetMaxLength().Should().Be(HandWrittenId<ArchiveProfile>.Length);
        previous.IsFixedLength().Should().BeTrue();
        previous.IsUnicode().Should().BeFalse();
        previous.GetCollation().Should().Be(IdCollations.PostgreSql);
        previous.IsNullable.Should().BeTrue();

        ValueObjectRegistry.TryGet(typeof(HandWrittenId<ArchiveProfile>), out _).Should().BeTrue();
    }

    private static IModel DesignTimeModel(DbContext context)
    {
        using (context)
        {
            return context.GetService<IDesignTimeModel>().Model;
        }
    }

    /// <summary>An entity referring to others by their public identifiers.</summary>
    private sealed class Ledger
    {
        public int Id { get; set; }

        public AccountId Account { get; set; }

        public EventId Event { get; set; }

        public AccountId? Replaced { get; set; }
    }

    /// <summary>An entity referring to another by an identifier written by hand.</summary>
    private sealed class Archive
    {
        public int Id { get; set; }

        public HandWrittenId<ArchiveProfile> Reference { get; set; }

        public HandWrittenId<ArchiveProfile>? Previous { get; set; }
    }

    /// <summary>A model holding an identifier written by hand, in a binary collation.</summary>
    private sealed class ArchiveContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureEntityIds(IdCollations.PostgreSql);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Archive>();
    }

    /// <summary>A model whose identifier columns compare byte by byte.</summary>
    private sealed class CollatedContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureEntityIds(IdCollations.PostgreSql, typeof(AccountId).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Ledger>();
    }

    /// <summary>A model validating what it reads, identifiers included.</summary>
    private sealed class StrictContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder
                .ConfigureValueObjects(strict: true, typeof(AccountId).Assembly)
                .ConfigureEntityIds(strict: true, typeof(AccountId).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Ledger>();
    }

    /// <summary>A model leaving the identifier columns in the database's collation.</summary>
    private sealed class DefaultContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureEntityIds();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Ledger>();
    }
}
