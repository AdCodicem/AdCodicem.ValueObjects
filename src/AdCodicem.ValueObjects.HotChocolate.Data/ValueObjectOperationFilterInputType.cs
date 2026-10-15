using HotChocolate.Data.Filters;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// The comparable operations of a value object, whose operands are its scalar, named after the scalar.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <remarks>
/// <para>
/// <c>eq</c>, <c>neq</c>, <c>in</c>, <c>nin</c>, <c>gt</c>, <c>gte</c>, <c>lt</c>, <c>lte</c> and their negations, as
/// HotChocolate's <c>ComparableOperationFilterInputType</c> gives them, or <c>eq</c> and <c>neq</c> alone for a value
/// object over <see cref="bool"/>, as HotChocolate gives a <see cref="bool"/>. An operand is nullable; an element of
/// <c>in</c> and <c>nin</c> is not. HotChocolate.Data's handlers read an operand through the scalar, normalized and
/// validated, and compare it with the member through the value object's operators.
/// </para>
/// <para>
/// The input is named after the scalar, <c>IbanOperationFilterInput</c>, with the prefix HotChocolate gives a type of a
/// named convention; a subclass that names itself keeps its name. Subclass it, or bind it with <c>BindRuntimeType</c>
/// for both the value object and its nullable type, to narrow the operations of one value object.
/// </para>
/// </remarks>
public class ValueObjectOperationFilterInputType<TSelf> : ComparableOperationFilterInputType<TSelf>, IValueObjectOperationFilterInputType
    where TSelf : struct, IValueObject
{
    /// <inheritdoc />
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (typeof(IValueObject<bool>).IsAssignableFrom(typeof(TSelf)))
        {
            // What HotChocolate's BooleanOperationFilterInputType offers a bool, its operand the value object's scalar.
            descriptor.Operation(DefaultFilterOperations.Equals).Type(typeof(TSelf)).MakeNullable();
            descriptor.Operation(DefaultFilterOperations.NotEquals).Type(typeof(TSelf)).MakeNullable();
            descriptor.AllowAnd(false).AllowOr(false);
        }
        else
        {
            base.Configure(descriptor);
        }

        var scalar = descriptor.Extend().Context.TypeInspector.GetTypeRef(typeof(TSelf), TypeContext.Input);
        ValueObjectFilterNames.NameAfter(descriptor, scalar, static scalarName => scalarName + "OperationFilterInput");
    }
}
