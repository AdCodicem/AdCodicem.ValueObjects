using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using Dapper;

namespace AdCodicem.ValueObjects.Dapper;

/// <summary>
/// Registers Dapper type handlers for value objects.
/// </summary>
/// <remarks>
/// Dapper keeps its handlers in a process-wide table, so this is called once at start-up. Without it, every
/// query touching a value object would need an explicit projection.
/// </remarks>
public static class ValueObjectDapper
{
    private static readonly Lock Gate = new();

    /// <summary>
    /// Registers a handler for every value object declared in the given assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// Assemblies declaring the value objects. When none is given, everything already registered is handled.
    /// </param>
    /// <remarks>
    /// A value object Dapper already has a handler for keeps it, whoever registered it, so calling this again
    /// changes nothing and a handler of the application's own is not replaced. Which types are handled is read
    /// from Dapper's table rather than remembered here, so a call after <see cref="SqlMapper.ResetTypeHandlers"/>
    /// registers everything again.
    /// <para>
    /// A value object over <see cref="Int128"/> or <see cref="UInt128"/> gets no handler. No ADO.NET provider takes
    /// either as a parameter or returns one, so the column type, and the conversion to it, are the application's
    /// to choose, in a handler of its own.
    /// </para>
    /// <para>
    /// A generic value object has no handler until its constructions are known, and Dapper looks a handler up by the
    /// exact type, ahead of any query: register each construction with
    /// <see cref="AddValueObjectHandler{TSelf, TValue}"/>.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Closes the generic type handler over each value object type.")]
    [RequiresDynamicCode("Closes the generic type handler over each value object type.")]
    public static void AddValueObjectHandlers(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
        }

        lock (Gate)
        {
            foreach (var descriptor in ValueObjectRegistry.GetRegistered())
            {
                if (SqlMapper.HasTypeHandler(descriptor.ValueObjectType) || Is128Bit(descriptor.ValueType))
                {
                    continue;
                }

                var handlerType = typeof(ValueObjectTypeHandler<,>)
                    .MakeGenericType(descriptor.ValueObjectType, descriptor.ValueType);

                var handler = (SqlMapper.ITypeHandler)Activator.CreateInstance(handlerType)!;

                SqlMapper.AddTypeHandler(descriptor.ValueObjectType, handler);
                SqlMapper.AddTypeHandler(typeof(Nullable<>).MakeGenericType(descriptor.ValueObjectType), handler);
            }
        }
    }

    /// <summary>
    /// Registers a handler for one value object, such as a construction of a generic value object.
    /// </summary>
    /// <typeparam name="TSelf">Value object type, <c>Code&lt;Order&gt;</c> for instance.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TValue"/> is <see cref="Int128"/> or <see cref="UInt128"/>, which no ADO.NET provider carries.
    /// </exception>
    /// <remarks>
    /// The handler is the one <see cref="AddValueObjectHandlers"/> registers, for the value object and its nullable
    /// form, and a value object Dapper already has a handler for keeps it. Closed at compile time, it needs no dynamic
    /// code to be built; Dapper reflects over the handlers it is given, and the handler of a construction reads the
    /// column the construction declares from the registry, which describes the construction by reflection.
    /// </remarks>
    public static void AddValueObjectHandler<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (Is128Bit(typeof(TValue)))
        {
            throw new NotSupportedException(
                $"'{typeof(TSelf)}' is a value object over {typeof(TValue).Name}, which no ADO.NET provider carries. "
                + "Choose the column type, and the conversion to it, in a handler of the application's own.");
        }

        lock (Gate)
        {
            if (SqlMapper.HasTypeHandler(typeof(TSelf)))
            {
                return;
            }

            // Dapper registers a handler for a value type under its nullable form as well.
            SqlMapper.AddTypeHandler(new ValueObjectTypeHandler<TSelf, TValue>());
        }
    }

    /// <summary>
    /// Tells whether an underlying type is one no provider can carry.
    /// </summary>
    /// <param name="valueType">Underlying type of a value object.</param>
    /// <returns><see langword="true"/> for <see cref="Int128"/> and <see cref="UInt128"/>.</returns>
    private static bool Is128Bit(Type valueType) => valueType == typeof(Int128) || valueType == typeof(UInt128);
}
