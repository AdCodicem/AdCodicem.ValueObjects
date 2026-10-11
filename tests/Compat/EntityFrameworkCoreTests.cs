using AdCodicem.ValueObjects.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The Entity Framework Core packages, built against the current major, inside a model of the next one, on SQLite: no
/// container, so these run wherever the island builds.
/// </summary>
public sealed class EntityFrameworkCoreTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions _options;

    public EntityFrameworkCoreTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder().UseSqlite(_connection).Options;

        using var context = new ShopContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void The_model_runs_on_the_next_major_of_Entity_Framework_Core()
        => typeof(DbContext).Assembly.GetName().Version!.Major.Should().Be(11);

    [Fact]
    public async Task A_value_object_round_trips_and_a_comparison_on_it_is_translated()
    {
        var customer = new Customer { Id = CustomerId.New(), Email = EmailAddress.Create("  Ada@Example.COM "), Country = CountryCode.France };
        customer.Accounts.Add(new BankAccount
        {
            Iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189"),
            CustomerId = customer.Id,
            Balance = Amount.Create(1250.505m),
        });

        await using (var write = new ShopContext(_options))
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = new ShopContext(_options);
        var query = read.Customers.Include(entity => entity.Accounts).Where(entity => entity.Email == EmailAddress.Create("ada@example.com"));

        query.ToQueryString().Should().NotContain("Value", "the comparison runs on the column, not on the client");
        var reloaded = await query.SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Id.Should().Be(customer.Id);
        reloaded.Country.Should().Be(CountryCode.France);
        reloaded.Accounts.Should().ContainSingle().Which.Iban.Value.Should().Be("FR7630006000011234567890189");
        reloaded.Accounts[0].Balance.Value.Should().Be(1250.50m);
    }

    [Fact]
    public void The_convention_installs_its_converter_and_comparer_and_sizes_the_column()
    {
        using var context = new ShopContext(_options);
        var iban = context.Model.FindEntityType(typeof(BankAccount))!.FindProperty(nameof(BankAccount.Iban))!;

        iban.GetMaxLength().Should().Be(34);
        iban.GetValueConverter().Should().BeOfType<ValueObjectConverter<Iban, string>>();
        iban.GetValueComparer().Should().BeOfType<ValueObjectComparer<Iban>>();
    }

    [Fact]
    public void An_entity_identifier_gets_a_fixed_width_non_unicode_column()
    {
        using var context = new ShopContext(_options);
        var id = context.Model.FindEntityType(typeof(Payment))!.FindProperty(nameof(Payment.Id))!;

        id.GetMaxLength().Should().Be(PaymentId.Length);
        id.IsFixedLength().Should().BeTrue();
        id.IsUnicode().Should().BeFalse();
    }

    [Fact]
    public async Task An_entity_identifier_round_trips()
    {
        var payment = new Payment { Id = PaymentId.New(), Account = Iban.Create("FR7630006000011234567890189"), Amount = Amount.Create(42m) };

        await using (var write = new ShopContext(_options))
        {
            write.Payments.Add(payment);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = new ShopContext(_options);
        var reloaded = await read.Payments.SingleAsync(entity => entity.Id == payment.Id, TestContext.Current.CancellationToken);

        reloaded.Id.Should().Be(payment.Id);
        reloaded.Id.Value.Should().StartWith("pay_");
    }

    [Fact]
    public async Task Each_construction_of_a_generic_value_object_gets_its_own_converter_and_round_trips()
    {
        await using (var write = new ShopContext(_options))
        {
            write.Orders.Add(new Order
            {
                Id = 1,
                Purchase = Reference<PurchaseOrder>.Create(" po-1 "),
                Invoice = Reference<SalesInvoice>.Create("inv-1"),
                Quantity = Quantity.Create(3),
            });
            write.Orders.Add(new Order { Id = 2, Purchase = Reference<PurchaseOrder>.Create("po-2"), Quantity = Quantity.Create(1) });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = new ShopContext(_options);
        var entity = read.Model.FindEntityType(typeof(Order))!;
        entity.FindProperty(nameof(Order.Purchase))!.GetValueConverter().Should().BeOfType<ValueObjectConverter<Reference<PurchaseOrder>, string>>();
        entity.FindProperty(nameof(Order.Invoice))!.GetValueConverter().Should().BeOfType<NullableValueObjectConverter<Reference<SalesInvoice>>>();
        entity.FindProperty(nameof(Order.Purchase))!.GetMaxLength().Should().Be(12);

        var orders = await read.Orders
            .Where(order => order.Purchase == Reference<PurchaseOrder>.Create("PO-1") || order.Invoice == null)
            .OrderBy(order => order.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        orders.Should().HaveCount(2);
        orders[0].Purchase.Value.Should().Be("PO-1");
        orders[0].Invoice!.Value.Value.Should().Be("INV-1");
        orders[1].Invoice.Should().BeNull();
    }

    [Fact]
    public async Task A_collection_of_value_objects_is_a_primitive_collection_that_round_trips_and_is_queried()
    {
        var belgian = Iban.Create("BE68539007547034");
        await using (var write = new ShopContext(_options))
        {
            var portfolio = write.Model.FindEntityType(typeof(Portfolio))!;
            var ibans = portfolio.FindProperty(nameof(Portfolio.Ibans))!;
            ibans.IsPrimitiveCollection.Should().BeTrue();
            ibans.GetElementType()!.GetValueConverter().Should().BeOfType<ValueObjectConverter<Iban, string>>();
            ibans.GetElementType()!.GetValueComparer().Should().BeOfType<ValueObjectComparer<Iban>>();
            ibans.GetElementType()!.GetMaxLength().Should().Be(34);
            portfolio.FindProperty(nameof(Portfolio.Previous))!.GetElementType()!.GetValueConverter()
                .Should().BeOfType<NullableValueObjectConverter<Iban>>();
            portfolio.FindProperty(nameof(Portfolio.Orders))!.GetElementType()!.GetValueConverter()
                .Should().BeOfType<ValueObjectConverter<Reference<PurchaseOrder>, string>>();

            write.Portfolios.Add(new Portfolio
            {
                Id = 1,
                Ibans = [Iban.Create("FR7630006000011234567890189"), belgian],
                Previous = [null, belgian],
                Quantities = [Quantity.Create(2)],
                Orders = [Reference<PurchaseOrder>.Create("po-1")],
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = new ShopContext(_options);
        var query = read.Portfolios.Where(entity => entity.Ibans.Contains(belgian));
        query.ToQueryString().Should().Contain("json_each");
        var reloaded = await query.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Ibans.Should().Equal(Iban.Create("FR7630006000011234567890189"), belgian);
        reloaded.Previous.Should().Equal(null, belgian);
        reloaded.Quantities.Should().Equal(Quantity.Create(2));
        reloaded.Orders.Should().Equal(Reference<PurchaseOrder>.Create("PO-1"));
    }

    [Fact]
    public async Task An_element_a_value_object_rejects_is_refused_or_stored_as_a_null()
    {
#pragma warning disable VO0010 // The uninitialized instances are what the converters refuse, or store as a null.
        var refused = new Portfolio { Id = 2, Ibans = [default] };
        var stored = new Portfolio { Id = 3, Previous = [default(Iban)] };
#pragma warning restore VO0010

        await using (var write = new ShopContext(_options))
        {
            write.Portfolios.Add(refused);
            var save = () => write.SaveChangesAsync(TestContext.Current.CancellationToken);

            var thrown = await save.Should().ThrowAsync<DbUpdateException>();
            ValueObjectErrors.TryGetCode(thrown.Which, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.Required);
        }

        await using (var write = new ShopContext(_options))
        {
            write.Portfolios.Add(stored);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = new ShopContext(_options);
        (await read.Portfolios.AnyAsync(entity => entity.Id == 2, TestContext.Current.CancellationToken)).Should().BeFalse();
        var reloaded = await read.Portfolios.AsNoTracking().SingleAsync(entity => entity.Id == 3, TestContext.Current.CancellationToken);
        reloaded.Previous.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task The_value_of_a_value_object_is_read_from_its_column_under_UseValueObjectQueries()
    {
        var options = new DbContextOptionsBuilder().UseSqlite(_connection).UseValueObjectQueries().Options;
        var french = Iban.Create("FR7630006000011234567890189");
        var customer = new Customer { Id = CustomerId.New(), Email = EmailAddress.Create("value@example.com"), Country = CountryCode.France };
        customer.Accounts.Add(new BankAccount { Iban = french, CustomerId = customer.Id, Balance = Amount.Create(10m) });
        customer.Accounts.Add(new BankAccount { Iban = Iban.Create("BE68539007547034"), CustomerId = customer.Id, Balance = Amount.Create(20m) });

        await using (var write = new ShopContext(options))
        {
            write.Customers.Add(customer);
            write.Orders.Add(new Order { Id = 1, Purchase = Reference<PurchaseOrder>.Create("po-1"), Quantity = Quantity.Create(5) });
            write.Portfolios.Add(new Portfolio { Id = 1, Ibans = [french], Previous = [null, french] });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = new ShopContext(options);
        var prefix = "FR";
        short least = 2;

        var byPrefix = read.Accounts.Where(account => account.Iban.Value.StartsWith(prefix)).Select(account => account.Iban);
        byPrefix.ToQueryString().Should().Contain("\"a\".\"Iban\" LIKE @prefix_startswith").And.NotContain("CAST");
        (await byPrefix.ToListAsync(TestContext.Current.CancellationToken)).Should().Equal(french);
        (await read.Orders.CountAsync(order => order.Quantity.Value > least && order.Purchase.Value.StartsWith("PO"), TestContext.Current.CancellationToken))
            .Should().Be(1);
        var byElement = read.Portfolios.Where(entity => entity.Ibans.Any(iban => iban.Value.StartsWith(prefix)));
        byElement.ToQueryString().Should().NotContain("CAST", "Entity Framework Core 11 leaves out the conversion of an element to its own store type");
        (await byElement.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await read.Portfolios.CountAsync(entity => entity.Previous.Any(iban => iban!.Value.Value.StartsWith(prefix)), TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task Change_tracking_compares_the_way_the_value_object_does()
    {
        var customer = new Customer { Id = CustomerId.New(), Email = EmailAddress.Create("tracking@example.com"), Country = CountryCode.France };

        await using (var write = new ShopContext(_options))
        {
            write.Customers.Add(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var update = new ShopContext(_options);
        var tracked = await update.Customers.SingleAsync(entity => entity.Id == customer.Id, TestContext.Current.CancellationToken);
        tracked.Country = CountryCode.Create("fr");

        update.ChangeTracker.DetectChanges();
        update.Entry(tracked).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task A_lenient_read_trusts_the_column_and_a_strict_read_refuses_what_the_domain_would_reject()
    {
        var owner = CustomerId.New();
        await using (var write = new ShopContext(_options))
        {
            write.Customers.Add(new Customer { Id = owner, Email = EmailAddress.Create("strict@example.com"), Country = CountryCode.Belgium });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Another writer stores an IBAN whose check digits are wrong.
            await write.Database.ExecuteSqlRawAsync(
                "INSERT INTO accounts (Iban, CustomerId, Balance) VALUES ({0}, {1}, {2})",
                // SQLite holds a Guid as upper-case text, the way the provider writes it.
                ["FR0030006000011234567890190", owner.Value.ToString().ToUpperInvariant(), "1.00"],
                TestContext.Current.CancellationToken);
        }

        await using var lenient = new ShopContext(_options);
        var trusted = await lenient.Accounts.SingleAsync(account => account.CustomerId == owner, TestContext.Current.CancellationToken);
        trusted.Iban.Value.Should().Be("FR0030006000011234567890190");

        await using var strict = new StrictShopContext(_options);
        var read = () => strict.Accounts.SingleAsync(account => account.CustomerId == owner, TestContext.Current.CancellationToken);

        var thrown = await read.Should().ThrowAsync<Exception>();
        Chain(thrown.Which).Should().ContainItemsAssignableTo<ValueObjectException>();
    }

    [Fact]
    public async Task A_Guid_value_object_key_is_generated_as_a_version_7_GUID_and_round_trips()
    {
        var parcels = new[] { new Parcel { Label = "first" }, new Parcel { Label = "second" } };
        await using (var write = new ShopContext(_options))
        {
            write.Parcels.AddRange(parcels);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        parcels.Select(parcel => parcel.Id.Value).Should().OnlyHaveUniqueItems().And.OnlyContain(key => key.Version == 7);
        await using var read = new ShopContext(_options);
        var key = parcels[1].Id;
        (await read.Parcels.SingleAsync(parcel => parcel.Id == key, TestContext.Current.CancellationToken)).Label.Should().Be("second");
    }

    [Fact]
    public void An_entity_added_without_its_Guid_key_is_refused()
    {
        using var context = new ShopContext(_options);

        var add = () => context.Customers.Add(new Customer { Email = EmailAddress.Create("no.key@example.com"), Country = CountryCode.France });

        add.Should().Throw<InvalidOperationException>().WithMessage("'Customer' was added without its key: *");
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task An_integer_value_object_key_is_generated_by_the_database_and_read_back_strictly()
    {
        var tickets = new[] { new Ticket { Label = "first" }, new Ticket { Label = "second" } };
        await using (var write = new StrictShopContext(_options))
        {
            write.Tickets.AddRange(tickets);
            write.Entry(tickets[0]).Property(ticket => ticket.Id).IsTemporary.Should().BeTrue();
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        tickets.Select(ticket => ticket.Id.Value).Should().Equal(1, 2);
        await using var read = new StrictShopContext(_options);
        var key = tickets[1].Id;
        (await read.Tickets.SingleAsync(ticket => ticket.Id == key, TestContext.Current.CancellationToken)).Label.Should().Be("second");
    }

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }
}
