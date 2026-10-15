using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Filters.Expressions;
using HotChocolate.Language;
using HotChocolate.Types;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// A string operation over the underlying text of a value object, as HotChocolate.Data's queryable provider handles it
/// for a <c>String</c> member.
/// </summary>
/// <remarks>
/// The member's text is <see cref="IValueObject{TValue}.Value"/>, read through the interface, which an explicit
/// implementation answers too, and which Entity Framework Core translates to the column under
/// <c>UseValueObjectQueries()</c>. A member of a nullable type is guarded by <c>!= null</c>, which Entity Framework Core
/// translates to <c>IS NOT NULL</c>: HotChocolate's own guard wraps <c>HasValue</c> in a test it does not translate.
/// </remarks>
internal sealed class ValueObjectTextOperationHandler : QueryableOperationHandlerBase
{
    /// <summary>The text of a value object over string, read through its interface.</summary>
    private static readonly PropertyInfo Text = typeof(IValueObject<string>).GetProperty(nameof(IValueObject<string>.Value))!;

    private readonly int _operation;

    /// <summary>
    /// Initializes the handler of one operation.
    /// </summary>
    /// <param name="inputParser">The parser of the operation's literal.</param>
    /// <param name="operation">One of <see cref="Operations"/>.</param>
    public ValueObjectTextOperationHandler(InputParser inputParser, int operation)
        : base(inputParser)
    {
        _operation = operation;

        // A null fragment is refused by the base class, as HotChocolate refuses one for a String member.
        CanBeNull = false;
    }

    /// <summary>
    /// Gets the operations a handler is created for, each with its negation.
    /// </summary>
    public static IReadOnlyList<int> Operations { get; } =
    [
        DefaultFilterOperations.Contains, DefaultFilterOperations.NotContains,
        DefaultFilterOperations.StartsWith, DefaultFilterOperations.NotStartsWith,
        DefaultFilterOperations.EndsWith, DefaultFilterOperations.NotEndsWith,
    ];

    /// <inheritdoc />
    public override bool CanHandle(
        ITypeCompletionContext context,
        IFilterInputTypeConfiguration typeConfiguration,
        IFilterFieldConfiguration fieldConfiguration)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Type is IValueObjectTextOperationFilterInputType
            && fieldConfiguration is FilterOperationFieldConfiguration { Id: var id }
            && id == _operation;
    }

    /// <inheritdoc />
    public override bool TryHandleOperation(
        QueryableFilterContext context,
        IFilterOperationField field,
        ObjectFieldNode node,
        [NotNullWhen(true)] out Expression? result)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Value.Kind == SyntaxKind.NullValue)
        {
            return base.TryHandleOperation(context, field, node, out result);
        }

        // The fragment is a String, never the value object: "FR" is no IBAN.
        var fragment = InputParser.ParseLiteral(node.Value, field, typeof(string));
        result = HandleOperation(context, field, node.Value, fragment);
        return true;
    }

    /// <inheritdoc />
    public override Expression HandleOperation(
        QueryableFilterContext context,
        IFilterOperationField field,
        IValueNode value,
        object? parsedValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        var instance = context.GetInstance();
        var underlying = Nullable.GetUnderlyingType(instance.Type);
        var text = Expression.Property(underlying is null ? instance : Expression.Convert(instance, underlying), Text);
        var test = _operation switch
        {
            DefaultFilterOperations.Contains or DefaultFilterOperations.NotContains => FilterExpressionBuilder.Contains(text, parsedValue!),
            DefaultFilterOperations.StartsWith or DefaultFilterOperations.NotStartsWith => FilterExpressionBuilder.StartsWith(text, parsedValue!),
            _ => FilterExpressionBuilder.EndsWith(text, parsedValue!),
        };

        if (underlying is not null)
        {
            test = Expression.AndAlso(Expression.NotEqual(instance, Expression.Constant(null, instance.Type)), test);
        }

        return _operation is DefaultFilterOperations.NotContains or DefaultFilterOperations.NotStartsWith or DefaultFilterOperations.NotEndsWith
            ? FilterExpressionBuilder.Not(test)
            : test;
    }
}
