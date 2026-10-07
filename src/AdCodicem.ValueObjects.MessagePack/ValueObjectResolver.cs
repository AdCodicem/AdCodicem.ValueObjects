using AdCodicem.ValueObjects.Metadata;
using MessagePack;
using MessagePack.Formatters;

namespace AdCodicem.ValueObjects.MessagePack;

/// <summary>
/// Answers the formatter of any value object, closed over it at compile time through the descriptor's visitor, and
/// nothing else.
/// </summary>
/// <remarks>
/// <para>
/// It answers <see langword="null"/> for any other type, and for a <see cref="Nullable{T}"/> of a value object, so that
/// the next resolver of a composite answers: MessagePack's own <c>NullableFormatter</c> then writes and reads a
/// <c>nil</c> for a <c>TSelf?</c> holding nothing and asks this resolver for <c>TSelf</c> otherwise. Placed first, before
/// <c>StandardResolver</c>, <c>ContractlessStandardResolver</c> or SignalR's own, it is the one MessagePack asks for a
/// value object, which no other resolver writes as its value: <c>StandardResolver</c> knows no formatter for one, and a
/// contractless resolver writes it as an empty map, <c>{}</c>, read back as the default instance.
/// </para>
/// <para>
/// A value object the registry holds is closed through <see cref="ValueObjectDescriptor.Accept{TResult}"/>. One it does
/// not hold yet, a construction of a generic value object nothing registered, a value object written by hand, a value
/// object of a module whose registration has not run, is described by <see cref="ValueObjectRegistry.TryResolve"/>, by
/// reflection, which only the JIT can run. The formatter of each type is built once per resolver and kept.
/// </para>
/// </remarks>
public sealed class ValueObjectResolver : IFormatterResolver
{
    private readonly bool _trusted;

    private ValueObjectResolver(bool trusted) => _trusted = trusted;

    /// <summary>
    /// Gets the resolver whose formatters read through the value objects' rules.
    /// </summary>
    public static ValueObjectResolver Instance { get; } = new(trusted: false);

    /// <summary>
    /// Gets the resolver whose formatters read without validating, for a cache or a store the application alone writes.
    /// </summary>
    public static ValueObjectResolver Trusted { get; } = new(trusted: true);

    /// <inheritdoc />
    public IMessagePackFormatter<T>? GetFormatter<T>()
        => _trusted ? TrustedCache<T>.Formatter : StrictCache<T>.Formatter;

    /// <summary>
    /// Builds the formatter of a value object, or answers <see langword="null"/> for any other type.
    /// </summary>
    /// <typeparam name="T">The type MessagePack asks a formatter for.</typeparam>
    /// <param name="trusted">Whether the formatter reads without validating.</param>
    /// <returns>The formatter, or <see langword="null"/>.</returns>
    private static IMessagePackFormatter<T>? Create<T>(bool trusted)
        => typeof(T).IsValueType
           && Nullable.GetUnderlyingType(typeof(T)) is null
           && ValueObjectRegistry.TryResolve(typeof(T), out var descriptor)
            ? (IMessagePackFormatter<T>)descriptor.Accept(new FormatterFactory(trusted))
            : null;

    /// <summary>
    /// Keeps the strict formatter of each type.
    /// </summary>
    /// <typeparam name="T">The type MessagePack asks a formatter for.</typeparam>
    private static class StrictCache<T>
    {
        /// <summary>
        /// The formatter, or <see langword="null"/> when <typeparamref name="T"/> is no value object.
        /// </summary>
        public static readonly IMessagePackFormatter<T>? Formatter = Create<T>(trusted: false);
    }

    /// <summary>
    /// Keeps the trusted formatter of each type.
    /// </summary>
    /// <typeparam name="T">The type MessagePack asks a formatter for.</typeparam>
    private static class TrustedCache<T>
    {
        /// <summary>
        /// The formatter, or <see langword="null"/> when <typeparamref name="T"/> is no value object.
        /// </summary>
        public static readonly IMessagePackFormatter<T>? Formatter = Create<T>(trusted: true);
    }

    /// <summary>
    /// Builds the formatter of a value object, closed over its type arguments.
    /// </summary>
    /// <param name="trusted">Whether the formatter reads without validating.</param>
    private sealed class FormatterFactory(bool trusted) : IValueObjectVisitor<IMessagePackFormatter>
    {
        /// <inheritdoc />
        public IMessagePackFormatter Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectFormatter<TSelf, TValue>(trusted);
    }
}
