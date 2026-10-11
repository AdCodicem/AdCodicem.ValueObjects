using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Translates <c>.Value</c> of a value object this package maps to its column, typed as the underlying value.
/// </summary>
/// <remarks>
/// <para>
/// The column keeps its store type: the translation takes the type mapping of the underlying type for it, so that
/// <c>nvarchar(34)</c> stays <c>nvarchar(34)</c>, and a parameter compared with the column is declared alike.
/// </para>
/// <para>
/// A column of a table, which Entity Framework Core links to the column of its relational model, is typed anew, so that
/// an index on it serves. Any other expression is converted by a <c>CAST</c> to that same store type:
/// </para>
/// <list type="bullet">
/// <item>
/// an element of a collection, read from a table-valued function: Npgsql infers the type of the array it unnests from
/// the columns referencing its element, and an element typed anew would have it rebuild the array over the underlying
/// type, which its value comparer refuses;
/// </item>
/// <item>
/// a column an optional navigation joins in, which Entity Framework Core no longer links to its table;
/// </item>
/// <item>
/// an expression, a <c>COALESCE</c> or a <c>CASE</c>.
/// </item>
/// </list>
/// </remarks>
/// <param name="typeMappingSource">Source of the type mapping a translated column takes.</param>
/// <param name="sqlExpressionFactory">Factory of the conversion an expression other than a column takes.</param>
internal sealed class ValueObjectMemberTranslator(
    IRelationalTypeMappingSource typeMappingSource,
    ISqlExpressionFactory sqlExpressionFactory) : IMemberTranslator
{
    /// <inheritdoc />
    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance?.TypeMapping is not { Converter: { } converter } typeMapping
            || member.Name != nameof(IValueObject<,>.Value))
        {
            return null;
        }

        // The type mapping of the underlying type for the column's own store type, which each provider finds from the
        // name alone, as it found the column's. Nothing a model can map lacks one.
        var mapping = typeMappingSource.FindMapping(returnType, typeMapping.StoreType);
        if (mapping is null || !StoresTheUnderlyingValue(converter, mapping))
        {
            return null;
        }

        return instance switch
        {
            ColumnExpression { Column: not null } column
                => new ColumnExpression(column.Name, column.TableAlias, column.Column, returnType, mapping, column.IsNullable),
            JsonScalarExpression json => new JsonScalarExpression(json.Json, json.Path, returnType, mapping, json.IsNullable),
            _ => sqlExpressionFactory.Convert(instance, returnType, mapping),
        };
    }

    /// <summary>
    /// Tells whether a converter stores the bare underlying value: one of this package's converters, whose conversion
    /// is the call to its <c>ToProvider</c> alone, or, where the provider stores the underlying type through a
    /// conversion of its own, the composition of the two Entity Framework Core writes, its conversion around that call.
    /// </summary>
    /// <param name="converter">The converter of the column.</param>
    /// <param name="mapping">The type mapping of the underlying type for the column's store type.</param>
    /// <remarks>
    /// Where the provider stores the underlying type as it stands, its mapping carries no converter, and a conversion
    /// around the call is the application's, which may store something other than the value: a text it encrypts or
    /// prefixes.
    /// </remarks>
    private static bool StoresTheUnderlyingValue(ValueConverter converter, RelationalTypeMapping mapping)
    {
        if (mapping.Converter is null)
        {
            return converter.ConvertToProviderExpression.Body is MethodCallExpression alone && IsOfThisPackage(alone.Method.DeclaringType!);
        }

        var call = new ConverterCall();
        call.Visit(converter.ConvertToProviderExpression);

        return call.Found;
    }

    /// <summary>Tells whether a type is a construction of one of this package's converters.</summary>
    private static bool IsOfThisPackage(Type type) => type.IsGenericType && ConverterTypes.Definitions.Contains(type.GetGenericTypeDefinition());

    /// <summary>
    /// Finds, in the conversion a converter writes, a call to one of this package's converters.
    /// </summary>
    private sealed class ConverterCall : ExpressionVisitor
    {
        /// <summary>Gets a value indicating whether the conversion calls one of this package's converters.</summary>
        public bool Found { get; private set; }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Found |= IsOfThisPackage(node.Method.DeclaringType!);

            return base.VisitMethodCall(node);
        }
    }
}
