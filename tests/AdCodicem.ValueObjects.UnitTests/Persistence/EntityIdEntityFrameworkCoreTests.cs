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
