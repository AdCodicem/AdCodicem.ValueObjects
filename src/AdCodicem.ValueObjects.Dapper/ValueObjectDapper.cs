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
    private static readonly HashSet<Type> Registered = [];

    /// <summary>
    /// Registers a handler for every value object declared in the given assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// Assemblies declaring the value objects. When none is given, everything already registered is handled.
    /// </param>
    [RequiresUnreferencedCode("Closes the generic type handler over each value object type.")]
    [RequiresDynamicCode("Closes the generic type handler over each value object type.")]
    public static void AddValueObjectHandlers(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
        }

        lock (Registered)
        {
            foreach (var descriptor in ValueObjectRegistry.GetRegistered())
            {
                if (!Registered.Add(descriptor.ValueObjectType))
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
