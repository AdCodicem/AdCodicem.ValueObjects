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
/// <para>
/// Each binder is closed over its value object at compile time, through the type arguments the descriptor hands back
/// to a visitor (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>), never at run time
/// with <see cref="Type.MakeGenericType(Type[])"/>. The registry still describes by reflection a value object nothing
/// registered, one written by hand or a construction of a generic one, the first time it is asked for it.
/// </para>
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
        // marker or IValueObject<TValue> alone - is left to MVC's own binders. A nullable value object resolves to
        // the value object itself: the binder tells an optional one by its metadata.
        if (!ValueObjectRegistry.TryResolve(modelType, out var descriptor))
        {
            return null;
        }

        return descriptor.Accept(BinderFactory.Instance);
    }

    /// <summary>
    /// Creates the binder of the value object a descriptor stands for, closed over the type arguments it hands back.
    /// </summary>
    private sealed class BinderFactory : IValueObjectVisitor<IModelBinder>
    {
        public static readonly BinderFactory Instance = new();

        /// <summary>
        /// Creates the binder of the value object, whatever its underlying type: MVC binds it from text, through
        /// <c>TryParse</c>.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns>The binder, closed over the value object.</returns>
        public IModelBinder Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectModelBinder<TSelf, TValue>();
    }
}
