using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using AdCodicem.ValueObjects.Testing.Data;
using AutoFixture.Kernel;

namespace AdCodicem.ValueObjects.AutoFixture;

/// <summary>
/// Answers the request for a value object type, or for its nullable type, with a value drawn by a sampler, closed over the
/// value object through its descriptor's visitor; any other request is left to the next builder.
/// </summary>
internal sealed class ValueObjectSpecimenBuilder : ISpecimenBuilder
{
    private readonly Draw _draw;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectSpecimenBuilder"/> class.
    /// </summary>
    /// <param name="sampler">The sampler every value object is drawn by.</param>
    public ValueObjectSpecimenBuilder(ValueObjectSampler sampler) => _draw = new Draw(sampler);

    /// <inheritdoc/>
    /// <exception cref="ValueObjectSamplingException">No value the type accepts could be drawn.</exception>
    public object Create(object request, ISpecimenContext context)
        // TryResolve turns down a type that is not a value object on its marker interface, registering nothing.
        => request is Type type && ValueObjectRegistry.TryResolve(type, out var descriptor)
            ? descriptor.Accept(_draw)
            : new NoSpecimen();

    private sealed class Draw(ValueObjectSampler sampler) : IValueObjectVisitor<object>
    {
        public object Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            try
            {
                return sampler.Next<TSelf, TValue>();
            }
            catch (ValueObjectSamplingException exception) when (!exception.FromGenerator)
            {
                // A generator registered with the options keeps the sampler's message, which names that registration.
                throw new ValueObjectSamplingException(
                    exception.ValueObjectType,
                    exception.Attempts,
                    exception.ErrorCode,
                    $"fixture.Register(() => {TypeNames.Of(typeof(TSelf))}.Create(...))");
            }
        }
    }
}
