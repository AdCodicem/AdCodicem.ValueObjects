using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;

namespace AdCodicem.ValueObjects.Serilog;

/// <summary>
/// Destructures a value object as the scalar its underlying value is.
/// </summary>
internal sealed class ValueObjectDestructuringPolicy : IDestructuringPolicy
{
    /// <summary>Gets the one instance, which keeps nothing.</summary>
    public static ValueObjectDestructuringPolicy Instance { get; } = new();

    /// <inheritdoc />
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value is IValueObject valueObject)
        {
            result = new ScalarValue(valueObject.GetBoxedValue());
            return true;
        }

        result = null;
        return false;
    }
}
