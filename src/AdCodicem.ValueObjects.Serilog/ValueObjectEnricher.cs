using Serilog.Core;
using Serilog.Events;

namespace AdCodicem.ValueObjects.Serilog;

/// <summary>
/// Replaces every value object Serilog captured as a scalar with its underlying value, in the properties of an event and
/// in the structures, sequences and dictionaries they hold.
/// </summary>
/// <remarks>
/// Nothing is rebuilt where no value object was found: a property, a structure, a sequence or a dictionary holding none
/// is kept as it is. A dictionary key holding a value object is kept too when another key of the same dictionary already
/// is its underlying value, since a dictionary cannot hold two equal keys.
/// </remarks>
internal sealed class ValueObjectEnricher : ILogEventEnricher
{
    /// <summary>Gets the one instance, which keeps nothing.</summary>
    public static ValueObjectEnricher Instance { get; } = new();

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        List<LogEventProperty>? unwrapped = null;
        foreach (var property in logEvent.Properties)
        {
            var value = Unwrap(property.Value);
            if (!ReferenceEquals(value, property.Value))
            {
                (unwrapped ??= []).Add(new LogEventProperty(property.Key, value));
            }
        }

        if (unwrapped is null)
        {
            return;
        }

        foreach (var property in unwrapped)
        {
            logEvent.AddOrUpdateProperty(property);
        }
    }

    /// <summary>Replaces the value objects a property value holds with their underlying values.</summary>
    /// <param name="value">The property value.</param>
    /// <returns>The same instance when it holds no value object.</returns>
    internal static LogEventPropertyValue Unwrap(LogEventPropertyValue value) => value switch
    {
        ScalarValue scalar => UnwrapScalar(scalar),
        StructureValue structure => UnwrapStructure(structure),
        SequenceValue sequence => UnwrapSequence(sequence),
        DictionaryValue dictionary => UnwrapDictionary(dictionary),
        _ => value,
    };

    private static ScalarValue UnwrapScalar(ScalarValue scalar)
        => scalar.Value is IValueObject valueObject ? new ScalarValue(valueObject.GetBoxedValue()) : scalar;

    private static StructureValue UnwrapStructure(StructureValue structure)
    {
        LogEventProperty[]? properties = null;
        for (var i = 0; i < structure.Properties.Count; i++)
        {
            var property = structure.Properties[i];
            var value = Unwrap(property.Value);
            if (!ReferenceEquals(value, property.Value))
            {
                properties ??= [.. structure.Properties];
                properties[i] = new LogEventProperty(property.Name, value);
            }
        }

        return properties is null ? structure : new StructureValue(properties, structure.TypeTag);
    }

    private static SequenceValue UnwrapSequence(SequenceValue sequence)
    {
        LogEventPropertyValue[]? elements = null;
        for (var i = 0; i < sequence.Elements.Count; i++)
        {
            var element = sequence.Elements[i];
            var value = Unwrap(element);
            if (!ReferenceEquals(value, element))
            {
                elements ??= [.. sequence.Elements];
                elements[i] = value;
            }
        }

        return elements is null ? sequence : new SequenceValue(elements);
    }

    private static DictionaryValue UnwrapDictionary(DictionaryValue dictionary)
    {
        List<KeyValuePair<ScalarValue, LogEventPropertyValue>>? elements = null;
        HashSet<ScalarValue>? unwrappedKeys = null;
        var index = 0;
        foreach (var element in dictionary.Elements)
        {
            var key = UnwrapKey(element.Key, dictionary, ref unwrappedKeys);
            var value = Unwrap(element.Value);
            if (elements is null && (!ReferenceEquals(key, element.Key) || !ReferenceEquals(value, element.Value)))
            {
                elements = [.. dictionary.Elements.Take(index)];
            }

            elements?.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(key, value));
            index++;
        }

        return elements is null ? dictionary : new DictionaryValue(elements);
    }

    /// <summary>
    /// Replaces a key holding a value object with its underlying value, unless another key of the dictionary already is
    /// that value: two equal keys would make <see cref="DictionaryValue"/> throw, and Serilog, catching it, would leave
    /// every value object of the event as it was captured.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="dictionary">The dictionary holding it.</param>
    /// <param name="unwrappedKeys">The keys replaced so far, created on the first one.</param>
    /// <returns>The key to write.</returns>
    private static ScalarValue UnwrapKey(ScalarValue key, DictionaryValue dictionary, ref HashSet<ScalarValue>? unwrappedKeys)
    {
        var unwrapped = UnwrapScalar(key);
        if (ReferenceEquals(unwrapped, key)
            || dictionary.Elements.ContainsKey(unwrapped)
            || !(unwrappedKeys ??= []).Add(unwrapped))
        {
            return key;
        }

        return unwrapped;
    }
}
