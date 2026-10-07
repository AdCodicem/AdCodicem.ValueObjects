using MessagePack;
using MessagePack.Resolvers;

namespace AdCodicem.ValueObjects.MessagePack;

/// <summary>
/// Wires value objects into MessagePack serializer options.
/// </summary>
public static class ValueObjectMessagePackExtensions
{
    /// <summary>
    /// Returns options whose resolver answers value objects first, then the options' own resolver.
    /// </summary>
    /// <param name="options">The options, <c>MessagePackSerializerOptions.Standard</c> or the application's own.</param>
    /// <param name="trusted">
    /// Whether value objects are read without validation, for bytes the application alone wrote, a cache among them;
    /// <see langword="false"/>, the default, reads them through their rules.
    /// </param>
    /// <returns>A copy of <paramref name="options"/> with the resolver in front of theirs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The resolver of the copy is a composite of <see cref="ValueObjectResolver.Instance"/>, or
    /// <see cref="ValueObjectResolver.Trusted"/>, and the options' own resolver, which answers every other type: nothing
    /// else changes, <see cref="MessagePackSerializerOptions.Security"/> and
    /// <see cref="MessagePackSerializerOptions.Compression"/> included. A value object is then written as the bare value
    /// the options' resolver writes for its underlying type, so that a <c>Native*</c> resolver of the chain is honoured.
    /// </para>
    /// <para>
    /// Called twice, the last call answers value objects first: its trust is the one that applies.
    /// </para>
    /// </remarks>
    public static MessagePackSerializerOptions WithValueObjects(this MessagePackSerializerOptions options, bool trusted = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.WithResolver(
            CompositeResolver.Create(trusted ? ValueObjectResolver.Trusted : ValueObjectResolver.Instance, options.Resolver));
    }
}
