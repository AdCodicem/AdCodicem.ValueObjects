using System.Data;
using AdCodicem.ValueObjects.Dapper;
using AdCodicem.ValueObjects.Metadata;
using Dapper;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The Dapper type handlers, driven through Dapper's own reader API over an in-memory table, which takes the path a
/// single-column query takes: the cell the provider returned goes to the handler as it is.
/// </summary>
/// <remarks>
/// What a real provider returns for each column type, and how a value object travels as a parameter, is checked
/// against PostgreSQL and SQL Server in the integration suite.
/// </remarks>
public class DapperTests
{
    /// <summary>
    /// What a provider returns for a column of the date and time family, when it is not the value object's own
    /// underlying type, keyed by a description of the case: SQL Server returns a <see cref="DateTime"/> for a
    /// <c>date</c> and a <see cref="TimeSpan"/> for a <c>time</c>, Npgsql a <see cref="DateOnly"/> and a
    /// <see cref="TimeOnly"/> for them, and a UTC <see cref="DateTime"/> for a <c>timestamptz</c>.
    /// </summary>
    private static readonly Dictionary<string, (Type Type, object Cell, object Expected)> ProviderTypes = new()
    {
        ["a SQL Server date into a DateOnly"] =
            (typeof(BirthDate), new DateTime(1990, 5, 17), BirthDate.Create(new DateOnly(1990, 5, 17))),
        ["a SQL Server time into a TimeOnly"] =
            (typeof(OpeningTime), new TimeSpan(9, 30, 0), OpeningTime.Create(new TimeOnly(9, 30))),
        ["an Npgsql date into a DateTime"] =
            (typeof(RecordedAt), new DateOnly(2024, 5, 17), RecordedAt.Create(new DateTime(2024, 5, 17))),
        ["an Npgsql time into a TimeSpan"] =
            (typeof(Duration), new TimeOnly(1, 30), Duration.Create(TimeSpan.FromMinutes(90))),
        ["an Npgsql timestamptz into a DateTimeOffset"] = (
            typeof(OccurredAt),
            new DateTime(2024, 5, 17, 10, 0, 0, DateTimeKind.Utc),
            OccurredAt.Create(new DateTimeOffset(2024, 5, 17, 10, 0, 0, TimeSpan.Zero))),
        ["a float into a decimal"] = (typeof(Amount), 12.5d, Amount.Create(12.5m)),
    };

    /// <summary>
    /// Values the handler cannot convert to the underlying type, keyed by a description of the case.
    /// </summary>
    private static readonly Dictionary<string, (Type Type, object Cell)> Mismatches = new()
    {
        ["a DateOnly into a DateTimeOffset"] = (typeof(OccurredAt), new DateOnly(2024, 5, 17)),
        ["a TimeSpan into a DateTimeOffset"] = (typeof(OccurredAt), new TimeSpan(9, 30, 0)),
        ["a TimeOnly into a DateTimeOffset"] = (typeof(OccurredAt), new TimeOnly(9, 30)),
        ["a Guid into an int"] = (typeof(PageNumber), Guid.Parse("0192f4a0-0000-7000-8000-000000000001")),
        ["a long out of the range of an int"] = (typeof(PageNumber), 5_000_000_000L),
        ["a float out of the range of a decimal"] = (typeof(Amount), 1e30d),
        ["a TimeSpan longer than a day into a TimeOnly"] = (typeof(OpeningTime), new TimeSpan(25, 0, 0)),
        ["a negative TimeSpan into a TimeOnly"] = (typeof(OpeningTime), TimeSpan.FromHours(-1)),
    };

    static DapperTests() => ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);

    public static TheoryData<string> EveryProviderType => [.. ProviderTypes.Keys];

    public static TheoryData<string> EveryMismatch => [.. Mismatches.Keys];

    [Fact]
    public void Registering_the_handlers_makes_Dapper_handle_every_value_object_and_its_nullable()
    {
        SqlMapper.HasTypeHandler(typeof(Iban)).Should().BeTrue();
        SqlMapper.HasTypeHandler(typeof(Iban?)).Should().BeTrue();
        SqlMapper.HasTypeHandler(typeof(CustomerId)).Should().BeTrue();
        SqlMapper.HasTypeHandler(typeof(AccountId)).Should().BeTrue("an entity identifier is a value object too");
    }

    /// <summary>
    /// No provider takes an Int128 or a UInt128 as a parameter, or returns one, so which column a 128-bit value object
    /// lands in, and how it gets there, is the application's to say, through a handler of its own.
    /// </summary>
    [Fact]
    public void A_128_bit_value_object_gets_no_handler()
    {
        SqlMapper.HasTypeHandler(typeof(LedgerBalance)).Should().BeFalse();
        SqlMapper.HasTypeHandler(typeof(Fingerprint)).Should().BeFalse();
    }

    /// <summary>
    /// Dapper looks a handler up by the exact type, before any query, and the constructions of a generic value object
    /// are only known to the application: it registers each one it stores, and its nullable form comes with it. The
    /// constructions here are closed over types of this class, which nothing else in the process resolves, so the
    /// registration is the only thing that can have handled them.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_is_handled_once_registered()
    {
        try
        {
            SqlMapper.AddTypeHandler(new FixedIbanHandler());
            ValueObjectDapper.AddValueObjectHandlers();
            SqlMapper.HasTypeHandler(typeof(Reference<Account>)).Should().BeFalse("nothing resolved the construction");

            ValueObjectDapper.AddValueObjectHandler<Reference<Account>, string>();
            ValueObjectDapper.AddValueObjectHandler<Reference<Account>, string>();
            ValueObjectDapper.AddValueObjectHandler<Catalog<Account>.Stock, int>();
            ValueObjectDapper.AddValueObjectHandler<Iban, string>();

            SqlMapper.HasTypeHandler(typeof(Reference<Account>?)).Should().BeTrue();
            Read(typeof(Reference<Account>), "PO-7").Should().Be(Reference<Account>.Create("PO-7"));
            Read(typeof(Catalog<Account>.Stock?), 12).Should().Be(Catalog<Account>.Stock.Create(12));
            SqlMapper.HasTypeHandler(typeof(Reference<DapperTests>)).Should().BeFalse("each construction is a type of its own");
            Read(typeof(Iban), "anything").Should().Be(FixedIbanHandler.Iban, "a handler of the application's own stays");
        }
        finally
        {
            SqlMapper.ResetTypeHandlers();
            ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);
        }
    }

    /// <summary>
    /// The registration reads what is handled from Dapper's own table, so a value object it meets for the first time,
    /// here a construction resolved just now, keeps a handler the application registered for it, where remembering
    /// what it had registered would have replaced it.
    /// </summary>
    [Fact]
    public void Registering_the_handlers_keeps_a_handler_the_application_registered_for_a_type_met_for_the_first_time()
    {
        try
        {
            ValueObjectRegistry.TryResolve(typeof(Reference<PositionalAccount>), out _).Should().BeTrue();
            SqlMapper.AddTypeHandler(new FixedReferenceHandler());

            ValueObjectDapper.AddValueObjectHandlers();

            Read(typeof(Reference<PositionalAccount>), "anything").Should().Be(FixedReferenceHandler.Reference);
        }
        finally
        {
            SqlMapper.ResetTypeHandlers();
            ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);
        }
    }

    /// <summary>
    /// A construction declares the column of its parameter as the value object it is built from does, whatever resolved
    /// it first: nothing else in the process resolves this one.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_declares_its_column()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<FixedIbanHandler>), out _).Should().BeFalse();
        var parameter = Substitute.For<IDbDataParameter>();

        new ValueObjectTypeHandler<Reference<FixedIbanHandler>, string>().SetValue(parameter, Reference<FixedIbanHandler>.Create("po-7"));

        parameter.DbType.Should().Be(DbType.String);
        parameter.Size.Should().Be(12);
    }

    /// <summary>
    /// Registering one value object is asking for it, so one no provider can carry is refused rather than skipped.
    /// </summary>
    [Fact]
    public void Registering_a_128_bit_value_object_by_its_type_is_refused()
    {
        var register = ValueObjectDapper.AddValueObjectHandler<LedgerBalance, Int128>;

        register.Should().Throw<NotSupportedException>().WithMessage("*LedgerBalance*Int128*handler of the application's own*");
        SqlMapper.HasTypeHandler(typeof(LedgerBalance)).Should().BeFalse();
    }

    /// <summary>
    /// Start-up code may run twice in one process - two hosts under test, say - and Dapper's table is process-wide.
    /// </summary>
    [Fact]
    public void Registering_the_handlers_again_changes_nothing()
    {
        ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);
        ValueObjectDapper.AddValueObjectHandlers();

        SqlMapper.HasTypeHandler(typeof(Iban)).Should().BeTrue();
        Read(typeof(Iban), "FR7630006000011234567890189").Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    /// <summary>
    /// A test suite may reset Dapper between tests. The reset empties Dapper's table and tells nobody, so what is
    /// handled has to be read from that table rather than remembered beside it.
    /// </summary>
    [Fact]
    public void Registering_the_handlers_after_Dapper_reset_its_table_registers_them_again()
    {
        try
        {
            SqlMapper.ResetTypeHandlers();
            SqlMapper.HasTypeHandler(typeof(Iban)).Should().BeFalse();

            ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);

            SqlMapper.HasTypeHandler(typeof(Iban)).Should().BeTrue();
            SqlMapper.HasTypeHandler(typeof(Iban?)).Should().BeTrue();
            Read(typeof(Iban), "FR7630006000011234567890189").Should().Be(Iban.Create("FR7630006000011234567890189"));
        }
        finally
        {
            ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);
        }
    }

    /// <summary>
    /// An application may handle one value object its own way. Registering the handlers again, at the start-up of a
    /// second host say, leaves its handler in place.
    /// </summary>
    [Fact]
    public void Registering_the_handlers_keeps_a_handler_the_application_registered()
    {
        try
        {
            SqlMapper.AddTypeHandler(new FixedIbanHandler());

            ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);

            Read(typeof(Iban), "anything").Should().Be(FixedIbanHandler.Iban);
        }
        finally
        {
            SqlMapper.ResetTypeHandlers();
            ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);
        }
    }

    /// <summary>
    /// A row is trusted as it is read: the value comes back as stored, neither normalized nor validated, as the
    /// Entity Framework Core converter reads it.
    /// </summary>
    [Fact]
    public void A_row_maps_its_columns_onto_value_object_members_as_stored()
    {
        using var table = new DataTable();
        table.Columns.Add(nameof(Account.Iban), typeof(string));
        table.Columns.Add(nameof(Account.Balance), typeof(decimal));
        table.Columns.Add(nameof(Account.Closed), typeof(DateTime));
        table.Rows.Add("FR7630006000011234567890189", 12.345m, DBNull.Value);

        using var reader = table.CreateDataReader();
        var account = reader.Parse<Account>().Single();

        account.Iban.Should().Be(Iban.Create("FR7630006000011234567890189"));
        account.Balance.Value.Should().Be(12.345m, "a value read as the underlying type is not normalized again");
        account.Closed.Should().BeNull();
    }

    [Fact]
    public void A_value_object_parameter_goes_out_as_its_underlying_value()
    {
        var handler = new ValueObjectTypeHandler<Amount, decimal>();
        var parameter = Substitute.For<IDbDataParameter>();

        handler.SetValue(parameter, Amount.Create(12.5m));

        parameter.Value.Should().Be(12.50m);
    }

    /// <summary>
    /// An instance that never went through <c>Create</c> holds the default value, which a read would trust. The handler
    /// refuses to write one its type rejects, with the rule, however Dapper reaches it: Dapper hands an optional value
    /// object holding one to the handler as it hands a required one, so the column it is written to cannot decide. A
    /// type that accepts its zero writes it.
    /// </summary>
    [Fact]
    public void A_value_object_parameter_its_type_rejects_is_refused()
    {
        SqlMapper.ITypeHandler boxed = new ValueObjectTypeHandler<CustomerId, Guid>();
        var parameter = Substitute.For<IDbDataParameter>();
        var zero = Substitute.For<IDbDataParameter>();

#pragma warning disable VO0010 // The uninitialized instance is what the handler refuses.
        var country = () => new ValueObjectTypeHandler<CountryCode, string>().SetValue(parameter, default);
        var customer = () => boxed.SetValue(parameter, default(CustomerId));
        new ValueObjectTypeHandler<Amount, decimal>().SetValue(zero, default);
#pragma warning restore VO0010

        country.Should().Throw<DataException>().WithMessage("The value to write is not a valid CountryCode: ?*");
        customer.Should().Throw<DataException>()
            .WithMessage("The value to write is not a valid CustomerId: A customer identifier must not be empty.");
        parameter.DidNotReceive().Value = Arg.Any<object>();
        zero.Value.Should().Be(0m, "zero is an amount");
    }

    /// <summary>
    /// The EF Core conventions map an identifier to fixed-length, non-Unicode text of its exact length, and a value
    /// object declaring a maximum length to Unicode text of that length. A parameter declaring the same type is
    /// compared with the column as it is, and SQL Server keeps its index seek.
    /// </summary>
    [Fact]
    public void A_text_parameter_declares_the_column_the_conventions_map_the_value_object_to()
    {
        var identifier = Substitute.For<IDbDataParameter>();
        var iban = Substitute.For<IDbDataParameter>();

        new ValueObjectTypeHandler<AccountId, string>().SetValue(identifier, AccountId.New());
        new ValueObjectTypeHandler<Iban, string>().SetValue(iban, Iban.Create("FR7630006000011234567890189"));

        identifier.DbType.Should().Be(DbType.AnsiStringFixedLength);
        identifier.Size.Should().Be(AccountId.Length);
        iban.DbType.Should().Be(DbType.String);
        iban.Size.Should().Be(34);
    }

    /// <summary>
    /// A value object saying no more than its underlying type leaves the parameter's type and size to the provider.
    /// </summary>
    [Fact]
    public void A_parameter_the_value_object_says_nothing_more_about_is_left_to_the_provider()
    {
        var amount = Substitute.For<IDbDataParameter>();
        var phone = Substitute.For<IDbDataParameter>();

        new ValueObjectTypeHandler<Amount, decimal>().SetValue(amount, Amount.Create(12.5m));
        new ValueObjectTypeHandler<PhoneNumber, string>().SetValue(phone, PhoneNumber.Create("+33123456789"));

        amount.DidNotReceive().DbType = Arg.Any<DbType>();
        amount.DidNotReceive().Size = Arg.Any<int>();
        phone.DidNotReceive().DbType = Arg.Any<DbType>();
        phone.DidNotReceive().Size = Arg.Any<int>();
    }

    /// <summary>
    /// A row read without validation may hold a value longer than the value object allows, and both SqlClient and
    /// Npgsql truncate a value longer than the size of its parameter, silently: the size grows to carry it whole.
    /// </summary>
    [Fact]
    public void A_parameter_is_never_sized_below_the_value_it_carries()
    {
        var parameter = Substitute.For<IDbDataParameter>();

        new ValueObjectTypeHandler<Iban, string>().SetValue(parameter, Iban.CreateUnchecked(new string('X', 40)));

        parameter.Size.Should().Be(40);
    }

    [Fact]
    public void An_optional_identifier_holding_nothing_declares_its_column_too()
    {
        SqlMapper.ITypeHandler handler = new ValueObjectTypeHandler<AccountId, string>();
        var parameter = Substitute.For<IDbDataParameter>();

        handler.SetValue(parameter, DBNull.Value);

        parameter.Value.Should().Be(DBNull.Value);
        parameter.DbType.Should().Be(DbType.AnsiStringFixedLength);
        parameter.Size.Should().Be(AccountId.Length);
    }

    [Fact]
    public void A_NULL_read_into_an_optional_value_object_is_null()
    {
        Read(typeof(Iban?), DBNull.Value).Should().BeNull();
        Read(typeof(Iban?), "FR7630006000011234567890189").Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    [Fact]
    public void A_NULL_read_into_a_required_value_object_is_refused_as_Dapper_refuses_it_for_an_int()
    {
        var act = () => Read(typeof(Iban), DBNull.Value);

        act.Should().Throw<DataException>().WithMessage("*NULL*Iban*");
    }

    /// <summary>
    /// Dapper checks for a NULL before it hands a column to the handler of a mapped member or of a constructor
    /// parameter, and never calls it: a required value object is left uninitialized, as a required <c>int</c> is
    /// left at zero, and only a single-column query reaches the refusal above. A nullable column belongs in an
    /// optional member.
    /// </summary>
    [Fact]
    public void A_NULL_column_leaves_a_required_member_or_constructor_parameter_uninitialized()
    {
        using var table = new DataTable();
        table.Columns.Add(nameof(Account.Iban), typeof(string));
        table.Columns.Add(nameof(Account.Balance), typeof(decimal));
        table.Columns.Add(nameof(Account.Closed), typeof(DateTime));
        table.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value);

        using var members = table.CreateDataReader();
        var account = members.Parse<Account>().Single();
        using var parameters = table.CreateDataReader();
        var positional = parameters.Parse<PositionalAccount>().Single();

        ((IValueObject<Iban, string>)account.Iban).IsDefault.Should().BeTrue();
        ((IValueObject<Amount, decimal>)account.Balance).IsDefault.Should().BeTrue();
        account.Closed.Should().BeNull();
        ((IValueObject<Iban, string>)positional.Iban).IsDefault.Should().BeTrue();
        ((IValueObject<Amount, decimal>)positional.Balance).IsDefault.Should().BeTrue();
        positional.Closed.Should().BeNull();
    }

    /// <summary>
    /// What the provider returns as another type than the underlying one is converted, and trusted as a value of
    /// the underlying type would be.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryProviderType))]
    public void A_column_the_provider_returns_as_another_type_is_converted_to_the_underlying_one(string name)
    {
        var (type, cell, expected) = ProviderTypes[name];

        Read(type, cell).Should().Be(expected);
    }

    /// <summary>
    /// Read in a single-column query, what the handler throws reaches the caller as it is: a conversion it cannot
    /// make is a <see cref="DataException"/>, as a NULL or text the value object refuses is, naming the type the
    /// provider returned and the value object it was read into.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(EveryMismatch))]
    public void A_column_the_value_object_cannot_hold_is_a_DataException_naming_both_types(string name)
    {
        var (type, cell) = Mismatches[name];

        var act = () => Read(type, cell);

        act.Should().Throw<DataException>()
            .WithMessage($"The {cell.GetType().Name} read cannot be converted to {type.Name}, a value object over *.");
    }

    /// <summary>
    /// SQL Server returns a <c>datetime2</c>, and Npgsql a <c>timestamp</c>, as a DateTime that names no zone, and
    /// no offset can be taken from it without guessing one.
    /// </summary>
    [Fact]
    public void A_DateTime_of_no_zone_read_into_a_DateTimeOffset_is_a_DataException()
    {
        var act = () => Read(typeof(OccurredAt), new DateTime(2024, 5, 17, 10, 0, 0, DateTimeKind.Unspecified));

        act.Should().Throw<DataException>()
            .WithMessage("The DateTime read names no zone*cannot be converted to OccurredAt, a value object over DateTimeOffset.");
    }

    /// <summary>
    /// A legacy schema may keep a Guid or a number in a text column. The text is parsed the way the value object
    /// parses text, which normalizes it, and validates it: unlike a value the provider returns as the underlying
    /// type, text was not necessarily written through the value object.
    /// </summary>
    [Fact]
    public void Text_read_into_a_value_object_of_another_type_is_parsed_and_validated()
    {
        Read(typeof(CustomerId), "0192f4a0-0000-7000-8000-000000000001")
            .Should().Be(CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")));
        Read(typeof(Amount), "12.345").Should().Be(Amount.Create(12.34m), "text normalizes as Parse normalizes it");
    }

    [Theory]
    [InlineData(typeof(CustomerId), "00000000-0000-0000-0000-000000000000", "*not a valid CustomerId*must not be empty*")]
    [InlineData(typeof(Amount), "-5", "*not a valid Amount*greater than or equal to 0*")]
    [InlineData(typeof(Amount), "five", "*not a valid Amount*")]
    public void Text_the_value_object_refuses_is_a_DataException_carrying_the_rule(Type type, string text, string message)
    {
        var act = () => Read(type, text);

        act.Should().Throw<DataException>().WithMessage(message);
    }

    /// <summary>
    /// The reverse of text kept for a number: a legacy schema may keep the digits of a reference in a numeric column.
    /// The number becomes text, which no value object wrote either, so it is normalized and validated through
    /// <c>TryCreate</c>.
    /// </summary>
    [Fact]
    public void A_value_converted_into_a_string_value_object_is_validated()
    {
        Read(typeof(Ordering.OrderReference), 12345L).Should().Be(Ordering.OrderReference.Create("12345"));
        Read(typeof(Ordering.OrderReference), 12.5m).Should().Be(Ordering.OrderReference.Create("12.5"));
    }

    /// <summary>
    /// A legacy schema may keep a string reference in a <c>uuid</c> or <c>uniqueidentifier</c> column, which the
    /// provider returns as a Guid. The Guid becomes text in the form <see cref="Guid.ToString()"/> writes, which no
    /// value object wrote, so it is normalized and validated as a number would be.
    /// </summary>
    [Fact]
    public void A_Guid_read_into_a_string_value_object_is_formatted_and_validated()
    {
        var guid = Guid.Parse("0192F4A0-0000-7000-8000-000000000001");

        Read(typeof(Label), guid).Should().Be(Label.Create("0192f4a0-0000-7000-8000-000000000001"));
    }

    [Theory]
    [InlineData(typeof(Iban), 7630006000L, "The value read is not a valid Iban: *")]
    [InlineData(typeof(Ordering.OrderReference), 12, "The value read is not a valid OrderReference: *at least 3*")]
    public void A_value_converted_into_a_string_value_object_it_refuses_is_a_DataException_carrying_the_rule(
        Type type,
        object cell,
        string message)
    {
        var act = () => Read(type, cell);

        act.Should().Throw<DataException>().WithMessage(message);
    }

    [Fact]
    public void A_Guid_read_into_a_string_value_object_it_refuses_is_a_DataException_carrying_the_rule()
    {
        var act = () => Read(typeof(Ordering.OrderReference), Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));

        act.Should().Throw<DataException>().WithMessage("The value read is not a valid OrderReference: *at most 20*");
    }

    /// <summary>
    /// Dapper checks for DBNull before it hands a column to the handler of a typed member, and the readers it
    /// drives return DBNull rather than null, so only a direct call shows that null is refused like DBNull.
    /// </summary>
    [Fact]
    public void A_null_handed_to_the_handler_directly_is_refused_like_a_NULL()
    {
        var handler = new ValueObjectTypeHandler<Iban, string>();

        var act = () => handler.Parse(null!);

        act.Should().Throw<DataException>().WithMessage("*NULL*Iban*");
    }

    [Fact]
    public void An_optional_value_object_holding_nothing_goes_out_as_DBNull()
    {
        SqlMapper.ITypeHandler handler = new ValueObjectTypeHandler<Iban, string>();
        var empty = Substitute.For<IDbDataParameter>();
        var filled = Substitute.For<IDbDataParameter>();

        handler.SetValue(empty, DBNull.Value);
        handler.SetValue(filled, Iban.Create("FR7630006000011234567890189"));

        empty.Value.Should().Be(DBNull.Value);
        filled.Value.Should().Be("FR7630006000011234567890189");
    }

    /// <summary>
    /// Reads one cell through Dapper, as a single-column query would.
    /// </summary>
    /// <param name="type">Type the column is read into.</param>
    /// <param name="cell">What the provider returned for the column.</param>
    /// <returns>What Dapper materialized.</returns>
    private static object? Read(Type type, object cell)
    {
        using var table = new DataTable();
        table.Columns.Add("value", typeof(object));
        table.Rows.Add(cell);

        using var reader = table.CreateDataReader();

        return reader.Parse(type).Single();
    }

    /// <summary>A row of a table of accounts.</summary>
    private sealed class Account
    {
        public Iban Iban { get; set; }

        public Amount Balance { get; set; }

        public RecordedAt? Closed { get; set; }
    }

    /// <summary>An application's own handler, which reads every cell as the same IBAN.</summary>
    private sealed class FixedIbanHandler : SqlMapper.TypeHandler<Iban>
    {
        public static readonly Iban Iban = Iban.Create("FR7630006000011234567890189");

        public override void SetValue(IDbDataParameter parameter, Iban value) => parameter.Value = value.Value;

        public override Iban Parse(object value) => Iban;
    }

    /// <summary>An application's own handler, which reads every cell as the same reference.</summary>
    private sealed class FixedReferenceHandler : SqlMapper.TypeHandler<Reference<PositionalAccount>>
    {
        public static readonly Reference<PositionalAccount> Reference = Reference<PositionalAccount>.Create("FIXED");

        public override void SetValue(IDbDataParameter parameter, Reference<PositionalAccount> value) => parameter.Value = value.Value;

        public override Reference<PositionalAccount> Parse(object value) => Reference;
    }

    /// <summary>A row of a table of accounts, mapped through its constructor.</summary>
    /// <param name="Iban">Account number.</param>
    /// <param name="Balance">Current balance.</param>
    /// <param name="Closed">When the account was closed, if it was.</param>
    private sealed record PositionalAccount(Iban Iban, Amount Balance, RecordedAt? Closed);
}
