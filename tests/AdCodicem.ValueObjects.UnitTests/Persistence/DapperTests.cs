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
    static DapperTests() => ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);

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
}
