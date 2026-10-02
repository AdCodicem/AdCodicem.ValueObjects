using System.Data;
using AdCodicem.ValueObjects.Dapper;
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

        account.Iban.IsDefault.Should().BeTrue();
        account.Balance.IsDefault.Should().BeTrue();
        account.Closed.Should().BeNull();
        positional.Iban.IsDefault.Should().BeTrue();
        positional.Balance.IsDefault.Should().BeTrue();
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

    /// <summary>A row of a table of accounts, mapped through its constructor.</summary>
    /// <param name="Iban">Account number.</param>
    /// <param name="Balance">Current balance.</param>
    /// <param name="Closed">When the account was closed, if it was.</param>
    private sealed record PositionalAccount(Iban Iban, Amount Balance, RecordedAt? Closed);
}
