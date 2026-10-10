using System.ComponentModel.DataAnnotations.Schema;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// A collection of value objects, which the Entity Framework Core convention maps as a primitive collection whose
/// elements get the converter, the comparer and the length of their value object.
/// </summary>
/// <remarks>
/// SQLite in memory stores each collection as a JSON array and queries its elements through <c>json_each</c>, with no
/// server: each test opens a database of its own. Entity Framework Core builds a model once per context type, so each
/// model shape has a context type of its own. What PostgreSQL and SQL Server make of a collection is checked in the
/// integration suite.
/// </remarks>
public sealed partial class PrimitiveCollectionTests : IDisposable
{
    private static readonly Iban French = Iban.Create("FR7630006000011234567890189");
    private static readonly Iban Belgian = Iban.Create("BE68539007547034");
    private static readonly CustomerId Ada = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));
    private static readonly CustomerId Grace = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000002"));

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public PrimitiveCollectionTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Each property holding a collection of a value object the convention maps, whatever collection type Entity
    /// Framework Core maps, is a primitive collection, whose elements are converted, compared and sized as the value
    /// object is. A collection of an optional value object over text stores a value it rejects as a null; over a value
    /// type, its elements are converted as those of the value object are. Only a property Entity Framework Core would map
    /// itself is made one: a read-only property, an indexer and a property marked [NotMapped] stay out of the model.
    /// </summary>
    [Fact]
    public void A_collection_of_value_objects_is_a_primitive_collection_whose_elements_are_converted_compared_and_sized()
    {
        using var context = Open<AccountContext>();
        var account = DesignTimeModel(context).FindEntityType(typeof(Account))!;

        Element<Iban>(account, nameof(Account.Ibans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        Element<Quantity>(account, nameof(Account.Quantities), typeof(ValueObjectConverter<Quantity, short>), maxLength: null);
        Element<Iban>(account, nameof(Account.ReadOnlyIbans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        Element<Iban>(account, nameof(Account.CollectedIbans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        Element<Iban>(account, nameof(Account.EnumeratedIbans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        Element<Iban>(account, nameof(Account.OptionalIbans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        Element<CustomerId>(account, nameof(Account.Holders), typeof(ValueObjectConverter<CustomerId, Guid>), maxLength: null);
        Element<Amount>(account, nameof(Account.Limits), typeof(ValueObjectConverter<Amount, decimal>), maxLength: null);
        Element<Ordering.OrderReference>(
            account, nameof(Account.References), typeof(ValueObjectConverter<Ordering.OrderReference, string>), maxLength: 20);
        Element<Reference<Account>>(
            account, nameof(Account.Orders), typeof(ValueObjectConverter<Reference<Account>, string>), maxLength: 12);
        account.FindProperty(nameof(Account.OptionalIbans))!.IsNullable.Should().BeTrue();

        OptionalElement<Iban>(account, nameof(Account.PreviousIbans), typeof(NullableValueObjectConverter<Iban>), maxLength: 34);
        OptionalElement<Reference<Account>>(
            account, nameof(Account.Returns), typeof(NullableValueObjectConverter<Reference<Account>>), maxLength: 12);
        OptionalElement<CustomerId>(
            account, nameof(Account.FormerHolders), typeof(ValueObjectConverter<CustomerId, Guid>), maxLength: null);

        var tags = account.FindProperty(nameof(Account.Tags))!;
        tags.IsPrimitiveCollection.Should().BeTrue("Entity Framework Core maps a list of text itself");
        tags.GetElementType()!.GetValueConverter().Should().BeNull("text is no value object");

        account.FindProperty(nameof(Account.Unmapped)).Should().BeNull("[NotMapped] keeps a property out of the model");
        account.FindProperty(nameof(Account.Computed)).Should().BeNull("a read-only property is out of the model, as before");
        account.FindProperty("Item").Should().BeNull("an indexer is no property");
        account.FindProperty(nameof(Account.Counts)).Should().BeNull();
    }

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, maps in a collection as it maps in a column,
    /// and round-trips there; one over a 128-bit integer is left to the application, in a collection as in a column.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_round_trips_through_a_primitive_collection(string type)
        => Samples.All[type].RoundTripsThroughAPrimitiveCollection();

    /// <summary>
    /// The length of an element is the one the descriptor declares, as a column's is: a value object registered by hand
    /// with a schema of its own sizes its elements by that schema. The value object is this test's own, since the registry
    /// is shared by the whole process.
    /// </summary>
    [Fact]
    public void An_element_is_sized_by_the_schema_its_value_object_was_registered_with()
    {
        ValueObjectRegistry.Register<CollectionMark, string>(new ValueObjectSchema { MaxLength = 20 });

        using var context = Open<MarkContext>();
        var shelf = DesignTimeModel(context).FindEntityType(typeof(MarkedShelf))!;

        CollectionMark.Schema.MaxLength.Should().Be(12, "the type declares a length of its own");
        Element<CollectionMark>(shelf, nameof(MarkedShelf.Marks), typeof(ValueObjectConverter<CollectionMark, string>), maxLength: 20);
    }

    /// <summary>
    /// A collection is stored as the JSON array of the values its elements carry, null elements included, and read back
    /// as it was written; a query over its elements runs on the database, through <c>json_each</c>.
    /// </summary>
    [Fact]
    public async Task A_collection_round_trips_as_the_JSON_array_of_its_values_and_a_query_over_its_elements_is_translated()
    {
        var order = Reference<Account>.Create("po-1");
        var written = new Account
        {
            Id = 1,
            Ibans = [French, Belgian],
            Quantities = [Quantity.Create(1), Quantity.Create(5)],
            ReadOnlyIbans = [Belgian],
            CollectedIbans = new List<Iban> { French },
            EnumeratedIbans = new List<Iban> { Belgian, French },
            PreviousIbans = [null, French],
            Holders = [Ada, Grace],
            FormerHolders = [Grace, null],
            Limits = [Amount.Create(0m), Amount.Create(12.5m)],
            Orders = [order],
            Returns = [null, order],
            References = [Ordering.OrderReference.Create("ord-1")],
            OptionalIbans = null,
        };

        await using (var write = Open<AccountContext>())
        {
            write.Add(written);
            write.Add(new Account { Id = 2 });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await StoredAsync("Ibans", 1)).Should().Be("""["FR7630006000011234567890189","BE68539007547034"]""");
        (await StoredAsync("PreviousIbans", 1)).Should().Be("""[null,"FR7630006000011234567890189"]""");
        (await StoredAsync("Quantities", 1)).Should().Be("[1,5]");
        (await StoredAsync("OptionalIbans", 1)).Should().BeNull();

        await using var read = Open<AccountContext>();
        var reloaded = await read.Set<Account>().AsNoTracking().SingleAsync(account => account.Id == 1, TestContext.Current.CancellationToken);
        reloaded.Ibans.Should().Equal(French, Belgian);
        reloaded.Quantities.Should().Equal(Quantity.Create(1), Quantity.Create(5));
        reloaded.ReadOnlyIbans.Should().Equal(Belgian);
        reloaded.CollectedIbans.Should().Equal(French);
        reloaded.EnumeratedIbans.Should().Equal(Belgian, French);
        reloaded.PreviousIbans.Should().Equal(null, French);
        reloaded.Holders.Should().Equal(Ada, Grace);
        reloaded.FormerHolders.Should().Equal(Grace, null);
        reloaded.Limits.Should().Equal(Amount.Create(0m), Amount.Create(12.5m));
        reloaded.Orders.Should().Equal(Reference<Account>.Create("PO-1"));
        reloaded.Returns.Should().Equal(null, order);
        reloaded.OptionalIbans.Should().BeNull();

        // Values hoisted into locals, as a query over a value object takes them.
        var belgian = Belgian;
        var one = Quantity.Create(1);
        Iban? previous = French;
        var byIban = read.Set<Account>().Where(account => account.Ibans.Contains(belgian));
        byIban.ToQueryString().Should().Contain("json_each", "the elements are read on the database");
        (await byIban.Select(account => account.Id).ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(1);
        (await read.Set<Account>().CountAsync(account => account.Quantities.Any(quantity => quantity > one), TestContext.Current.CancellationToken))
            .Should().Be(1);
        (await read.Set<Account>().CountAsync(account => account.Orders.Contains(order), TestContext.Current.CancellationToken)).Should().Be(1);
        (await read.Set<Account>().CountAsync(account => account.PreviousIbans.Contains(previous), TestContext.Current.CancellationToken))
            .Should().Be(1);
        (await read.Set<Account>().CountAsync(account => account.Holders.Count == 2, TestContext.Current.CancellationToken)).Should().Be(1);
        (await read.Set<Account>().CountAsync(account => account.Ibans[1] == belgian, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>
    /// Change tracking compares the elements as their value object does: a list differing only by case, for a value
    /// object compared without regard to it, is no change, and an element added in place, a null among them, is one.
    /// </summary>
    [Fact]
    public async Task Change_tracking_compares_each_element_as_its_value_object_does()
    {
        await using (var write = Open<AccountContext>())
        {
            write.Add(new Account { Id = 1, Ibans = [French], PreviousIbans = [French], References = [Ordering.OrderReference.Create("ORD-1")] });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = Open<AccountContext>())
        {
            var tracked = await update.Set<Account>().SingleAsync(TestContext.Current.CancellationToken);
            tracked.References = [Ordering.OrderReference.Create("ord-1")];

            update.ChangeTracker.DetectChanges();
            update.Entry(tracked).State.Should().Be(EntityState.Unchanged, "the references are equal without regard to case");

            tracked.Ibans.Add(Belgian);
            tracked.PreviousIbans.Add(null);
            update.ChangeTracker.DetectChanges();
            update.Entry(tracked).Property(account => account.Ibans).IsModified.Should().BeTrue();
            update.Entry(tracked).Property(account => account.PreviousIbans).IsModified.Should().BeTrue();
            (await update.SaveChangesAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        }

        await using var read = Open<AccountContext>();
        var reloaded = await read.Set<Account>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Ibans.Should().Equal(French, Belgian);
        reloaded.PreviousIbans.Should().Equal(French, null);
    }

    /// <summary>
    /// Under <c>strict: true</c>, the elements are validated as the properties are: what another writer stored is read as
    /// stored by the lenient context, normalized by the strict one, and refused by it with the code of the rule broken.
    /// A null element is read as a null by both.
    /// </summary>
    [Fact]
    public async Task A_strict_context_normalizes_and_validates_each_element_it_reads()
    {
        await using (var write = Open<AccountContext>())
        {
            write.Add(new Account { Id = 1 });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var strict = new StrictAccountContext(_connection))
        {
            var account = DesignTimeModel(strict).FindEntityType(typeof(Account))!;
            Element<Iban>(account, nameof(Account.Ibans), typeof(StrictValueObjectConverter<Iban, string>), maxLength: 34);
            OptionalElement<Iban>(account, nameof(Account.PreviousIbans), typeof(StrictNullableValueObjectConverter<Iban>), maxLength: 34);
            OptionalElement<CustomerId>(
                account, nameof(Account.FormerHolders), typeof(StrictValueObjectConverter<CustomerId, Guid>), maxLength: null);
            OptionalElement<Reference<Account>>(
                account, nameof(Account.Returns), typeof(StrictNullableValueObjectConverter<Reference<Account>>), maxLength: 12);
        }

        // Another writer stores an IBAN in its print form, which the domain normalizes, and a null.
        await ExecuteAsync("""UPDATE accounts SET "Ibans" = '["fr76 3000 6000 0112 3456 7890 189"]', "PreviousIbans" = '[null]'""");

        await using (var lenient = Open<AccountContext>())
        {
            var trusted = await lenient.Set<Account>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            trusted.Ibans.Should().Equal(Iban.CreateUnchecked("fr76 3000 6000 0112 3456 7890 189"));
            trusted.PreviousIbans.Should().ContainSingle().Which.Should().BeNull();
        }

        await using (var strict = new StrictAccountContext(_connection))
        {
            var validated = await strict.Set<Account>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            validated.Ibans.Should().Equal(French);
            validated.PreviousIbans.Should().ContainSingle().Which.Should().BeNull();
        }

        // Then a quantity out of its range, which the strict context refuses.
        await ExecuteAsync("""UPDATE accounts SET "Quantities" = '[1001]'""");

        await using (var strict = new StrictAccountContext(_connection))
        {
            var read = () => strict.Set<Account>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);

            var thrown = await read.Should().ThrowAsync<Exception>();
            ValueObjectErrors.TryGetCode(thrown.Which, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        }
    }

    /// <summary>
    /// An element the value object rejects, which only an instance that never went through <c>Create</c> can hold, is
    /// refused in a collection of the value object, and nothing is written; a zero the value object accepts is written.
    /// In a collection of the optional value object over text, it is stored as a null, as an optional column stores a
    /// NULL: the maintainer's choice. Over a value type, Entity Framework Core cannot write that null, and the element is
    /// refused as in a collection of the value object.
    /// </summary>
    [Fact]
    public async Task An_element_the_value_object_rejects_is_refused_or_stored_as_a_null_in_a_collection_of_the_optional_value_object_over_text()
    {
#pragma warning disable VO0010 // The uninitialized instances are what the converters refuse, or store as a null.
        var unsetCustomer = default(CustomerId);
        var unsetIban = default(Iban);
        var unsetAmount = default(Amount);
        var unsetOrder = default(Reference<Account>);
#pragma warning restore VO0010

        await RefusedAsync(new Account { Id = 1, Holders = [Ada, unsetCustomer] }, typeof(CustomerId), ValueObjectErrorCodes.Required);
        await RefusedAsync(new Account { Id = 2, Ibans = [unsetIban] }, typeof(Iban), ValueObjectErrorCodes.Required);
        await RefusedAsync(new Account { Id = 3, FormerHolders = [unsetCustomer] }, typeof(CustomerId), ValueObjectErrorCodes.Required);

        await using (var write = Open<AccountContext>())
        {
            write.Add(new Account { Id = 4, PreviousIbans = [unsetIban, French], Returns = [unsetOrder], Limits = [unsetAmount] });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await StoredAsync("PreviousIbans", 4)).Should().Be("""[null,"FR7630006000011234567890189"]""");
        (await StoredAsync("Returns", 4)).Should().Be("[null]");
        (await StoredAsync("Limits", 4)).Should().Be("""["0.0"]""", "zero is an amount");

        await using var read = Open<AccountContext>();
        var reloaded = await read.Set<Account>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.PreviousIbans.Should().Equal(null, French);
        reloaded.Returns.Should().ContainSingle().Which.Should().BeNull();
        reloaded.Limits.Should().Equal(Amount.Create(0m));
    }

    /// <summary>
    /// A collection inside an owned type or a complex type, mapped to its own columns or to a JSON column, a complex
    /// collection included, is a primitive collection too: its elements are configured, it round-trips, and a query over
    /// them is translated.
    /// </summary>
    [Fact]
    public async Task A_collection_inside_an_owned_or_a_complex_type_is_a_primitive_collection_too()
    {
        await using (var write = Open<BranchContext>())
        {
            var branch = DesignTimeModel(write).FindEntityType(typeof(Branch))!;
            var converter = typeof(ValueObjectConverter<Iban, string>);
            Element<Iban>(branch.FindNavigation(nameof(Branch.Contact))!.TargetEntityType, nameof(Contact.Ibans), converter, maxLength: 34);
            Element<Iban>(branch.FindNavigation(nameof(Branch.Desks))!.TargetEntityType, nameof(Desk.Ibans), converter, maxLength: 34);
            Element<Iban>(branch.FindComplexProperty(nameof(Branch.Address))!.ComplexType, nameof(Postal.Ibans), converter, maxLength: 34);
            Element<Iban>(branch.FindComplexProperty(nameof(Branch.Mailing))!.ComplexType, nameof(Postal.Ibans), converter, maxLength: 34);
            OptionalElement<Iban>(
                branch.FindComplexProperty(nameof(Branch.Archives))!.ComplexType,
                nameof(Archive.Ibans),
                typeof(NullableValueObjectConverter<Iban>),
                maxLength: 34);

            write.Add(new Branch
            {
                Id = 1,
                Contact = new Contact { Ibans = [French] },
                Desks = [new Desk { Ibans = [Belgian] }],
                Address = new Postal { Ibans = [French, Belgian] },
                Mailing = new Postal { Ibans = [Belgian] },
                Archives = [new Archive { Ibans = [null, French] }],
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = Open<BranchContext>();
        var reloaded = await read.Set<Branch>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Contact.Ibans.Should().Equal(French);
        reloaded.Desks.Should().ContainSingle().Which.Ibans.Should().Equal(Belgian);
        reloaded.Address.Ibans.Should().Equal(French, Belgian);
        reloaded.Mailing.Ibans.Should().Equal(Belgian);
        reloaded.Archives.Should().ContainSingle().Which.Ibans.Should().Equal(null, French);

        var belgian = Belgian;
        (await read.Set<Branch>().CountAsync(branch => branch.Address.Ibans.Contains(belgian), TestContext.Current.CancellationToken)).Should().Be(1);
        (await read.Set<Branch>().CountAsync(branch => branch.Mailing.Ibans.Contains(belgian), TestContext.Current.CancellationToken)).Should().Be(1);
        (await read.Set<Branch>().CountAsync(branch => branch.Contact.Ibans.Contains(belgian), TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// A model that builds today does not change: a property ignored, a whole collection the application converts
    /// itself, in an entity type and in a complex type, and an element whose converter the application set, which keeps
    /// its length and the comparer Entity Framework Core gives it, are left as they went in.
    /// </summary>
    [Fact]
    public void What_the_application_configured_itself_is_left_as_it_went_in()
    {
        using var context = Open<ConfiguredContext>();
        var ledger = DesignTimeModel(context).FindEntityType(typeof(Ledger))!;

        ledger.FindProperty(nameof(Ledger.Ignored)).Should().BeNull();

        var whole = ledger.FindProperty(nameof(Ledger.Whole))!;
        whole.IsPrimitiveCollection.Should().BeFalse("the application converts the whole collection");
        whole.GetValueConverter().Should().BeOfType<IbansToText>();
        var nested = ledger.FindComplexProperty(nameof(Ledger.Nested))!.ComplexType.FindProperty(nameof(Postal.Ibans))!;
        nested.IsPrimitiveCollection.Should().BeFalse();
        nested.GetValueConverter().Should().BeOfType<IbansToText>();

        var sized = ledger.FindProperty(nameof(Ledger.Sized))!.GetElementType()!;
        sized.GetValueConverter().Should().BeOfType<ValueObjectConverter<Iban, string>>();
        sized.GetMaxLength().Should().Be(40, "the application sized it");
        sized.GetValueComparer().Should().NotBeOfType<ValueObjectComparer<Iban>>("the convention left the element alone");

        var balances = ledger.FindProperty(nameof(Ledger.Balances))!.GetElementType()!;
        balances.GetValueConverter().Should().BeOfType<LedgerBalanceToDecimal>("Entity Framework Core maps no 128-bit integer");
    }

    /// <summary>
    /// A read-only property over a backing field, which Entity Framework Core leaves out of the model unless told, gets
    /// its elements configured once the application declares it a primitive collection, and round-trips through the field.
    /// A length the application gave the elements stays.
    /// </summary>
    [Fact]
    public async Task A_primitive_collection_the_application_declares_over_a_read_only_property_gets_its_elements_configured()
    {
        await using (var write = Open<WalletContext>())
        {
            var wallet = DesignTimeModel(write).FindEntityType(typeof(Wallet))!;
            Element<Iban>(wallet, nameof(Wallet.Ibans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
            Element<Iban>(wallet, nameof(Wallet.Sized), typeof(ValueObjectConverter<Iban, string>), maxLength: 40);

            var written = new Wallet { Id = 1 };
            written.Open(French);
            written.Open(Belgian);
            write.Add(written);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = Open<WalletContext>();
        var reloaded = await read.Set<Wallet>().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Ibans.Should().Equal(French, Belgian);
    }

    /// <summary>
    /// In a hierarchy, a collection declared by the base entity type is one property, on the base, and a collection
    /// inherited from a class that is no entity type, with a setter only that class can call, is discovered as Entity
    /// Framework Core discovers such a property.
    /// </summary>
    [Fact]
    public void A_collection_in_a_hierarchy_is_one_property_of_the_type_declaring_it()
    {
        using var context = Open<PartyContext>();
        var party = DesignTimeModel(context).FindEntityType(typeof(Party))!;
        var person = DesignTimeModel(context).FindEntityType(typeof(Person))!;

        Element<Iban>(party, nameof(Party.Ibans), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        Element<Iban>(party, nameof(Party.Aliases), typeof(ValueObjectConverter<Iban, string>), maxLength: 34);
        person.GetDeclaredProperties().Should().NotContain(property => property.Name == nameof(Party.Ibans));
        person.FindProperty(nameof(Party.Ibans)).Should().BeSameAs(party.FindProperty(nameof(Party.Ibans)));
    }

    /// <summary>
    /// A collection of a value object written by hand over a reference type other than text maps as one of a generated
    /// value object does; its optional elements keep the converter of the value object, which has no nullable one.
    /// </summary>
    [Fact]
    public async Task A_collection_of_a_value_object_written_by_hand_round_trips()
    {
        var link = HandWrittenLink.Create(new Uri("https://example.com/a"));

        await using (var write = Open<BookmarkContext>())
        {
            var bookmarks = DesignTimeModel(write).FindEntityType(typeof(Bookmarks))!;
            Element<HandWrittenLink>(bookmarks, nameof(Bookmarks.Links), typeof(ValueObjectConverter<HandWrittenLink, Uri>), maxLength: null);
            OptionalElement<HandWrittenLink>(
                bookmarks, nameof(Bookmarks.OptionalLinks), typeof(ValueObjectConverter<HandWrittenLink, Uri>), maxLength: null);

            write.Add(new Bookmarks { Id = 1, Links = [link], OptionalLinks = [null, link] });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = Open<BookmarkContext>();
        var reloaded = await read.Set<Bookmarks>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Links.Should().Equal(link);
        reloaded.OptionalLinks.Should().Equal(null, link);
    }

    /// <summary>
    /// An entity identifier in a collection is mapped as the value object it is: its elements take its converter and its
    /// length, not the fixed width and the non-Unicode type <c>ConfigureEntityIds</c> gives a column of it.
    /// </summary>
    [Fact]
    public void An_entity_identifier_in_a_collection_is_mapped_as_a_value_object()
    {
        using var context = Open<SubscriberContext>();
        var subscriber = DesignTimeModel(context).FindEntityType(typeof(Subscriber))!;

        var primary = subscriber.FindProperty(nameof(Subscriber.Primary))!;
        primary.IsFixedLength().Should().BeTrue("ConfigureEntityIds maps a column of an identifier");
        primary.IsUnicode().Should().BeFalse();

        Element<AccountId>(subscriber, nameof(Subscriber.Accounts), typeof(ValueObjectConverter<AccountId, string>), maxLength: AccountId.Length);
        var element = subscriber.FindProperty(nameof(Subscriber.Accounts))!.GetElementType()!;
        element.IsFixedLength().Should().BeNull("the convention of identifiers leaves the elements to the one of value objects");
        element.IsUnicode().Should().BeNull();
    }

    /// <summary>
    /// A collection type Entity Framework Core takes for no primitive collection is Entity Framework Core's to refuse, as
    /// it refuses one of text: a set builds the model, and is refused once tracked.
    /// </summary>
    [Fact]
    public void A_set_of_value_objects_is_refused_by_Entity_Framework_Core_as_a_set_of_text_is()
    {
        using var write = Open<SetContext>();
        DesignTimeModel(write).FindEntityType(typeof(Keyring))!.FindProperty(nameof(Keyring.Ibans))!.IsPrimitiveCollection.Should().BeTrue();

        write.Invoking(context => context.Add(new Keyring { Id = 1, Ibans = [French] }))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The type 'HashSet<Iban>' cannot be used as a primitive collection because it is not an array and does not implement 'IList<Iban?>'.*");
    }

    /// <summary>
    /// On PostgreSQL, whose model building opens no connection, a collection is an array of the element's column type,
    /// sized by the value object.
    /// </summary>
    [Fact]
    public void On_PostgreSQL_a_collection_is_an_array_of_the_column_type_of_its_element()
    {
        using var context = new PostgreSqlAccountContext();
        var account = DesignTimeModel(context).FindEntityType(typeof(Account))!;

        account.FindProperty(nameof(Account.Ibans))!.GetColumnType().Should().Be("character varying(34)[]");
        account.FindProperty(nameof(Account.Quantities))!.GetColumnType().Should().Be("smallint[]");
    }

    private static void Element<TSelf>(IReadOnlyTypeBase type, string name, Type converter, int? maxLength)
        where TSelf : struct, IEquatable<TSelf>
    {
        var element = PrimitiveCollection(type, name);
        element.ClrType.Should().Be<TSelf>();
        element.GetValueConverter().Should().BeOfType(converter);
        element.GetValueComparer().Should().BeOfType<ValueObjectComparer<TSelf>>();
        element.GetMaxLength().Should().Be(maxLength);
    }

    private static void OptionalElement<TSelf>(IReadOnlyTypeBase type, string name, Type converter, int? maxLength)
        where TSelf : struct, IEquatable<TSelf>
    {
        var element = PrimitiveCollection(type, name);
        element.ClrType.Should().Be<TSelf?>();
        element.GetValueConverter().Should().BeOfType(converter);

        // The comparer type is what a compiled model instantiates.
        element.FindAnnotation("ValueComparerType")!.Value.Should().Be(typeof(NullableValueObjectComparer<TSelf>));
        element.GetValueComparer().Should().BeOfType<NullableValueObjectComparer<TSelf>>();
        element.GetMaxLength().Should().Be(maxLength);
    }

    /// <summary>Reads the model a context builds as it stands at design time, with every annotation the convention sets.</summary>
    private static IModel DesignTimeModel(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static IReadOnlyElementType PrimitiveCollection(IReadOnlyTypeBase type, string name)
    {
        var property = type.FindProperty(name);
        property.Should().NotBeNull("{0} is mapped", name);
        property!.IsPrimitiveCollection.Should().BeTrue("{0} is a primitive collection", name);

        return property.GetElementType()!;
    }

    private TContext Open<TContext>()
        where TContext : CollectionContext
    {
        var context = (TContext)Activator.CreateInstance(typeof(TContext), _connection)!;
        context.Database.EnsureCreated();

        return context;
    }

    private async Task<string?> StoredAsync(string column, int id)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""SELECT "{column}" FROM accounts WHERE "Id" = {id}""";

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task RefusedAsync(Account account, Type valueObject, string code)
    {
        await using (var write = Open<AccountContext>())
        {
            write.Add(account);
            var save = () => write.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Entity Framework Core wraps what a converter throws while it saves; the code is read through it.
            var thrown = await save.Should().ThrowAsync<DbUpdateException>();
            ValueObjectErrors.TryGetCode(thrown.Which, out var read).Should().BeTrue();
            read.Should().Be(code);
            var refusal = thrown.WithInnerException<ValueObjectException>().Which;
            refusal.ValueObjectType.Should().Be(valueObject);
            refusal.Message.Should().StartWith($"The value to write is not a valid {valueObject.Name}: ");
        }

        await using var check = Open<AccountContext>();
        (await check.Set<Account>().AnyAsync(entity => entity.Id == account.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse("nothing reached the table");
    }

    /// <summary>An account, holding collections of value objects in every shape a property takes.</summary>
    private sealed class Account
    {
        private readonly List<Iban> _computed = [];

        public int Id { get; set; }

        public List<Iban> Ibans { get; set; } = [];

        public Quantity[] Quantities { get; set; } = [];

        public IReadOnlyList<Iban> ReadOnlyIbans { get; set; } = [];

        public IReadOnlyCollection<Iban> CollectedIbans { get; set; } = [];

        public IEnumerable<Iban> EnumeratedIbans { get; set; } = [];

        public List<Iban?> PreviousIbans { get; set; } = [];

        public List<CustomerId> Holders { get; set; } = [];

        public List<CustomerId?> FormerHolders { get; set; } = [];

        public List<Amount> Limits { get; set; } = [];

        public List<Ordering.OrderReference> References { get; set; } = [];

        public List<Reference<Account>> Orders { get; set; } = [];

        public List<Reference<Account>?> Returns { get; set; } = [];

        public List<Iban>? OptionalIbans { get; set; }

        public List<string> Tags { get; set; } = [];

        [NotMapped]
        public List<Iban> Unmapped { get; set; } = [];

        [NotMapped]
        public Dictionary<string, int> Counts { get; set; } = [];

        public IReadOnlyList<Iban> Computed => _computed;

        public Iban this[int index]
        {
            get => Ibans[index];
            set => Ibans[index] = value;
        }
    }

    /// <summary>An owned type holding a collection of value objects, mapped to a JSON column.</summary>
    private sealed class Contact
    {
        public List<Iban> Ibans { get; set; } = [];
    }

    /// <summary>An owned type of a collection, holding a collection of value objects, mapped to a JSON column.</summary>
    private sealed class Desk
    {
        public List<Iban> Ibans { get; set; } = [];
    }

    /// <summary>A complex type holding a collection of value objects.</summary>
    private sealed class Postal
    {
        public List<Iban> Ibans { get; set; } = [];
    }

    /// <summary>An element of a complex collection, holding optional value objects.</summary>
    private sealed class Archive
    {
        public List<Iban?> Ibans { get; set; } = [];
    }

    /// <summary>A branch, holding collections inside owned and complex types.</summary>
    private sealed class Branch
    {
        public int Id { get; set; }

        public Contact Contact { get; set; } = new();

        public List<Desk> Desks { get; set; } = [];

        public Postal Address { get; set; } = new();

        public Postal Mailing { get; set; } = new();

        public List<Archive> Archives { get; set; } = [];
    }

    /// <summary>A ledger, whose collections the application configures itself.</summary>
    private sealed class Ledger
    {
        public int Id { get; set; }

        public List<Iban> Ignored { get; set; } = [];

        public List<Iban> Whole { get; set; } = [];

        public Postal Nested { get; set; } = new();

        public List<Iban> Sized { get; set; } = [];

        public List<LedgerBalance> Balances { get; set; } = [];
    }

    /// <summary>A wallet, whose collections are read-only properties over backing fields, as a DDD aggregate holds them.</summary>
    private sealed class Wallet
    {
        private readonly List<Iban> _ibans = [];
        private readonly List<Iban> _sized = [];

        public int Id { get; set; }

        public IReadOnlyList<Iban> Ibans => _ibans;

        public IReadOnlyList<Iban> Sized => _sized;

        public void Open(Iban iban) => _ibans.Add(iban);
    }

    /// <summary>A class that is no entity type, declaring a collection only it can set.</summary>
    private abstract class Registrant
    {
        public List<Iban> Aliases { get; private set; } = [];
    }

    /// <summary>The base entity type of a hierarchy.</summary>
    private abstract class Party : Registrant
    {
        public int Id { get; set; }

        public List<Iban> Ibans { get; set; } = [];
    }

    /// <summary>A derived entity type.</summary>
    private sealed class Person : Party;

    /// <summary>Another derived entity type.</summary>
    private sealed class Company : Party;

    /// <summary>Bookmarks, holding a value object written by hand over <see cref="Uri"/>.</summary>
    private sealed class Bookmarks
    {
        public int Id { get; set; }

        public List<HandWrittenLink> Links { get; set; } = [];

        public List<HandWrittenLink?> OptionalLinks { get; set; } = [];
    }

    /// <summary>A subscriber, holding an entity identifier and a collection of them.</summary>
    private sealed class Subscriber
    {
        public int Id { get; set; }

        public AccountId Primary { get; set; }

        public List<AccountId> Accounts { get; set; } = [];
    }

    /// <summary>A keyring, holding a set, which Entity Framework Core maps as no primitive collection.</summary>
    private sealed class Keyring
    {
        public int Id { get; set; }

        public HashSet<Iban> Ibans { get; set; } = [];
    }

    /// <summary>A shelf holding the value object one test registers by hand.</summary>
    private sealed class MarkedShelf
    {
        public int Id { get; set; }

        public List<CollectionMark> Marks { get; set; } = [];
    }

    /// <summary>Stores a whole list of IBANs as one text, as an application converting it itself does.</summary>
    private sealed class IbansToText() : ValueConverter<List<Iban>, string>(
        ibans => string.Join(',', ibans.Select(static iban => iban.Value)),
        text => text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(static iban => Iban.Create(iban)).ToList());

    /// <summary>Compares two lists of IBANs element by element.</summary>
    private sealed class IbansComparer() : ValueComparer<List<Iban>>(
        (left, right) => left!.SequenceEqual(right!),
        ibans => ibans.Aggregate(0, static (hash, iban) => HashCode.Combine(hash, iban)),
        ibans => ibans.ToList());

    /// <summary>Stores a balance as a decimal.</summary>
    private sealed class LedgerBalanceToDecimal() : ValueConverter<LedgerBalance, decimal>(
        balance => (decimal)balance.Value,
        value => LedgerBalance.Create((Int128)value));

    /// <summary>The mark of a shelf, which one test registers by hand with a schema of its own.</summary>
    [ValueObject<string>(MaxLength = 12)]
    public readonly partial struct CollectionMark;

    /// <summary>A context over the connection of a test, mapping the value objects of the unit domain.</summary>
    /// <param name="connection">The database of the test.</param>
    private abstract class CollectionContext(SqliteConnection connection) : DbContext
    {
        protected virtual bool Strict => false;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite(connection);

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects(Strict, typeof(Iban).Assembly);
    }

    /// <summary>Maps the accounts, trusting what it reads.</summary>
    private class AccountContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Account>().ToTable("accounts");
    }

    /// <summary>Maps the accounts, validating what it reads.</summary>
    private sealed class StrictAccountContext(SqliteConnection connection) : AccountContext(connection)
    {
        protected override bool Strict => true;
    }

    /// <summary>Maps the accounts on PostgreSQL, whose model building opens no connection.</summary>
    private sealed class PostgreSqlAccountContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureValueObjects();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Account>();
    }

    /// <summary>Maps the branches, with their owned and complex types.</summary>
    private sealed class BranchContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Branch>(branch =>
            {
                branch.OwnsOne(entity => entity.Contact, contact => contact.ToJson());
                branch.OwnsMany(entity => entity.Desks, desk => desk.ToJson());
                branch.ComplexProperty(entity => entity.Address);
                branch.ComplexProperty(entity => entity.Mailing, mailing => mailing.ToJson());
                branch.ComplexCollection(entity => entity.Archives, archive => archive.ToJson());
            });
    }

    /// <summary>Maps the ledgers, configuring their collections as an application departing from the convention does.</summary>
    private sealed class ConfiguredContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>(ledger =>
            {
                ledger.Ignore(entity => entity.Ignored);
                ledger.Property(entity => entity.Whole).HasConversion(new IbansToText(), new IbansComparer());
                ledger.ComplexProperty(entity => entity.Nested)
                    .Property(holding => holding.Ibans).HasConversion(new IbansToText(), new IbansComparer());
                ledger.PrimitiveCollection(entity => entity.Sized)
                    .ElementType(element => element.HasConversion<ValueObjectConverter<Iban, string>>().HasMaxLength(40));
                ledger.PrimitiveCollection(entity => entity.Balances)
                    .ElementType(element => element.HasConversion<LedgerBalanceToDecimal>());
            });
    }

    /// <summary>Maps the wallets, declaring their read-only collections.</summary>
    private sealed class WalletContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Wallet>(wallet =>
            {
                wallet.PrimitiveCollection(entity => entity.Ibans);
                wallet.PrimitiveCollection(entity => entity.Sized).ElementType(element => element.HasMaxLength(40));
            });
    }

    /// <summary>Maps a hierarchy in one table.</summary>
    private sealed class PartyContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>();
            modelBuilder.Entity<Company>();
            modelBuilder.Entity<Party>();
        }
    }

    /// <summary>Maps the bookmarks, after resolving the value object written by hand, which nothing registers.</summary>
    private sealed class BookmarkContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            ValueObjectRegistry.TryResolve(typeof(HandWrittenLink), out _).Should().BeTrue();
            base.ConfigureConventions(configurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Bookmarks>();
    }

    /// <summary>Maps the subscribers through both conventions, as an application holding identifiers does.</summary>
    private sealed class SubscriberContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.ConfigureEntityIds(typeof(AccountId).Assembly);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Subscriber>();
    }

    /// <summary>Maps the keyrings.</summary>
    private sealed class SetContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Keyring>();
    }

    /// <summary>Maps the shelves holding a value object registered by hand.</summary>
    private sealed class MarkContext(SqliteConnection connection) : CollectionContext(connection)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<MarkedShelf>();
    }
}
