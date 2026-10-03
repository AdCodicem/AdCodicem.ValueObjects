using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
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
