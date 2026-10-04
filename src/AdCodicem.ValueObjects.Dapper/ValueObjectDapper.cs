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
    /// <para>
    /// Each handler is closed over its value object at compile time, through the type arguments the descriptor hands
    /// back to a visitor (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>), never at
    /// run time with <see cref="Type.MakeGenericType(Type[])"/>, so building them takes no dynamic code. Locating the
    /// generated registration of an assembly given by name reads its metadata, which trimming may remove. Dapper itself
    /// is not compatible with native AOT: it files each handler in a cache it closes over the type at run time, and reads
    /// rows through code it emits.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
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
                descriptor.Accept(HandlerRegistration.Instance);
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
    /// code to be built, and it reads the column the value object declares off
    /// <see cref="IValueObject{TSelf, TValue}.Schema"/>, without asking the registry, which would describe a construction
    /// by reflection; Dapper reflects over the handlers it is given.
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
            TryAdd<TSelf, TValue>();
        }
    }

    /// <summary>
    /// Registers the handler of a value object, unless Dapper already has one for it. The caller holds the lock.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <returns><see langword="true"/> when the handler was registered.</returns>
    private static bool TryAdd<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (SqlMapper.HasTypeHandler(typeof(TSelf)))
        {
            return false;
        }

        // Dapper registers a handler for a value type under its nullable form as well.
        SqlMapper.AddTypeHandler(new ValueObjectTypeHandler<TSelf, TValue>());
        return true;
    }

    /// <summary>
    /// Tells whether an underlying type is one no provider can carry.
    /// </summary>
    /// <param name="valueType">Underlying type of a value object.</param>
    /// <returns><see langword="true"/> for <see cref="Int128"/> and <see cref="UInt128"/>.</returns>
    private static bool Is128Bit(Type valueType) => valueType == typeof(Int128) || valueType == typeof(UInt128);

    /// <summary>
    /// Registers the handler of the value object a descriptor stands for, closed over the type arguments it hands back.
    /// </summary>
    private sealed class HandlerRegistration : IValueObjectVisitor<bool>
    {
        public static readonly HandlerRegistration Instance = new();

        /// <summary>
        /// Registers the handler of the value object, unless it is over a 128-bit integer, which no provider carries, or
        /// Dapper already has a handler for it.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns><see langword="true"/> when the handler was registered.</returns>
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => !Is128Bit(typeof(TValue)) && TryAdd<TSelf, TValue>();
    }
}
