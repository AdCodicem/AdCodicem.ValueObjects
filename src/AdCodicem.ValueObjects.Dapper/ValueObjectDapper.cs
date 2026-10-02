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
    /// Tells whether an underlying type is one no provider can carry.
    /// </summary>
    /// <param name="valueType">Underlying type of a value object.</param>
    /// <returns><see langword="true"/> for <see cref="Int128"/> and <see cref="UInt128"/>.</returns>
    private static bool Is128Bit(Type valueType) => valueType == typeof(Int128) || valueType == typeof(UInt128);
}
