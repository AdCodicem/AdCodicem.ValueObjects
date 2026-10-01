using System.Collections.Concurrent;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AdCodicem.ValueObjects.AspNetCore.ModelBinding;

/// <summary>
/// Supplies the model binder of any value object.
/// </summary>
/// <remarks>
/// Providers are consulted once per action parameter while the application starts, so closing the generic
/// binder over the concrete types here costs nothing per request. The instances are cached anyway, because MVC
/// creates metadata for the same type in many places.
/// </remarks>
public sealed class ValueObjectModelBinderProvider : IModelBinderProvider
{
    private static readonly ConcurrentDictionary<Type, IModelBinder?> Binders = new();

    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Binders.GetOrAdd(context.Metadata.ModelType, static modelType => Create(modelType));
    }

    private static IModelBinder? Create(Type modelType)
    {
        // A descriptor exists for a struct implementing IValueObject<TSelf, TValue> over itself, the one shape the
        // binder can be closed over. Anything else carrying the marker - an interface, a class, a struct with the
        // marker or IValueObject<TValue> alone - is left to MVC's own binders.
        if (!ValueObjectRegistry.TryResolve(modelType, out var descriptor))
        {
            return null;
        }

        var binderType = typeof(ValueObjectModelBinder<,>).MakeGenericType(descriptor.ValueObjectType, descriptor.ValueType);

        return (IModelBinder?)Activator.CreateInstance(binderType);
    }
}
