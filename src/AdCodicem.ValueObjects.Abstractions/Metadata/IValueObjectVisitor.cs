namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Receives the type arguments of a value object that a caller knows only by its descriptor, so that the caller closes
/// its own generic code over them at compile time.
/// </summary>
/// <typeparam name="TResult">What the visitor hands back: the adapter it built, a flag, a schema read off the type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/> calls
/// <see cref="Visit{TSelf, TValue}"/> with the type arguments the descriptor was built with. An integration that finds
/// value objects by <see cref="Type"/> creates its adapter there, <c>new MyFormatter&lt;TSelf, TValue&gt;()</c>, rather
/// than closing it with <see cref="Type.MakeGenericType(Type[])"/> and <see cref="Activator"/>, which has no native code
/// to run for a struct under native AOT.
/// </para>
/// <para>
/// Under native AOT, the compiler generates <see cref="Visit{TSelf, TValue}"/> of each visitor the application creates for
/// each value object a descriptor is built for in code: every value object the generator registers, and every value
/// object registered by hand, a value object written by hand or a construction of a generic one. One the registry
/// describes by reflection, the first time <see cref="ValueObjectRegistry.TryResolve"/> is asked for it because nothing
/// registered it, has a visitor call too, which only the JIT can run.
/// </para>
/// <para>
/// A visitor needing context, a builder or a flag, holds it in fields. The calls happen at start-up, where creating a
/// visitor per call costs nothing that matters.
/// </para>
/// </remarks>
public interface IValueObjectVisitor<out TResult>
{
    /// <summary>
    /// Receives the type arguments of the value object.
    /// </summary>
    /// <typeparam name="TSelf">The value object type.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <returns>What the visitor builds for the value object.</returns>
    TResult Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>;
}
