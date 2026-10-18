using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.CsvHelper;

/// <summary>
/// Creates the converter of every value object a CsvHelper context meets, from the registry, the first time CsvHelper
/// asks for it.
/// </summary>
/// <remarks>
/// <para>
/// CsvHelper asks the application's factories before its own, the first time it meets a type, and keeps the converter a
/// factory creates. This one claims every value object, never its nullable type, around which CsvHelper's own
/// <c>NullableConverter</c> puts the converter it creates. Its descriptor is the one registered, or, where the runtime
/// supports dynamic code, one the registry describes by reflection, as for <c>[ParsesAs(typeof(T))]</c>: a value object
/// is found when CsvHelper meets it, however late its declaring module's initializer runs, and a construction of a generic
/// one and one written by hand included.
/// </para>
/// <para>
/// Under native AOT, a value object written by hand and a construction of a generic one are registered first, with
/// <see cref="ValueObjectRegistry.Register{TSelf, TValue}(ValueObjectSchema)"/>, or their converter added with
/// <see cref="ValueObjectTypeConverterCacheExtensions.AddValueObject{TSelf, TValue}"/>. A value object the registry cannot
/// describe is not declined, which would let CsvHelper map it as a record of its own and read default instances with
/// nothing thrown: <see cref="Create"/> throws <see cref="InvalidOperationException"/>, with no code.
/// </para>
/// </remarks>
public sealed class ValueObjectTypeConverterFactory : ITypeConverterFactory
{
    private ValueObjectTypeConverterFactory()
    {
    }

    /// <summary>Gets the factory.</summary>
    public static ValueObjectTypeConverterFactory Instance { get; } = new();

    /// <summary>
    /// Answers a value object, never its nullable type, around which CsvHelper's own <c>NullableConverter</c> puts this
    /// factory's converter.
    /// </summary>
    /// <param name="type">The type CsvHelper meets.</param>
    /// <returns><see langword="true"/> for a value object, as <see cref="ValueObjectRegistry.IsValueObject"/> answers it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public bool CanCreate(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return Nullable.GetUnderlyingType(type) is null && ValueObjectRegistry.IsValueObject(type);
    }

    /// <summary>
    /// Closes the converter through the descriptor's visitor, or throws <see cref="InvalidOperationException"/> for a value
    /// object the registry cannot describe without dynamic code.
    /// </summary>
    /// <param name="type">The value object.</param>
    /// <param name="cache">The cache asking, which keeps the converter.</param>
    /// <param name="typeConverter">The converter.</param>
    /// <returns><see langword="true"/>, so that the cache keeps the converter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The type is not a value object the registry describes, as under native AOT for one written by hand or a construction
    /// of a generic one nothing registered.
    /// </exception>
    public bool Create(Type type, TypeConverterCache cache, out ITypeConverter typeConverter)
    {
        ArgumentNullException.ThrowIfNull(type);

        typeConverter = Find(type).Accept(Closer.Instance);
        return true;
    }

    /// <summary>
    /// Finds the descriptor of a value object: the one registered, or, where the runtime supports dynamic code, one the
    /// registry describes by reflection, as <c>[ParsesAs(typeof(T))]</c> does.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only reached for a value object nothing registered: one written by hand, or a construction of a "
                        + "generic one, which the member CsvHelper maps names, and so keeps. Under native AOT, where dynamic "
                        + "code is not supported, the registered descriptor is the only one.")]
    private static ValueObjectDescriptor Find(Type type)
    {
        if (ValueObjectRegistry.TryGet(type, out var descriptor)
            || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(type, out descriptor)))
        {
            return descriptor;
        }

        throw new InvalidOperationException(
            $"CsvHelper meets '{TypeNames.Of(type)}', which is not a value object the registry describes. Under native AOT, a "
            + "value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a construction "
            + "of a generic one, ValueObjectRegistry.Register<TSelf, TValue>(static () => new TSelf.ValueJsonConverter()), "
            + "have to be registered before CsvHelper meets them, or their converter added with "
            + "AddValueObject<TSelf, TValue>().");
    }

    /// <summary>Closes the converter over the type arguments a descriptor hands back.</summary>
    private sealed class Closer : IValueObjectVisitor<ITypeConverter>
    {
        public static readonly Closer Instance = new();

        /// <summary>Closes the converter.</summary>
        /// <typeparam name="TSelf">The value object type.</typeparam>
        /// <typeparam name="TValue">Its underlying type.</typeparam>
        /// <returns>The converter.</returns>
        public ITypeConverter Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectTypeConverter<TSelf, TValue>();
    }
}
