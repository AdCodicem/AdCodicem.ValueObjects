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
                if (SqlMapper.HasTypeHandler(descriptor.ValueObjectType))
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
}
