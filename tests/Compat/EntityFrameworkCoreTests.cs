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

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }
}
