namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Receives the type of an entity identifier that a caller knows only by its descriptor, so that the caller closes its
/// own generic code over it at compile time.
/// </summary>
/// <typeparam name="TResult">What the visitor hands back: the adapter it built, a flag.</typeparam>
/// <remarks>
/// <see cref="EntityIdDescriptor.Accept{TResult}(IEntityIdVisitor{TResult})"/> calls <see cref="Visit{TId}"/> with the
/// type the descriptor was built for, as <see cref="Metadata.ValueObjectDescriptor.Accept{TResult}(Metadata.IValueObjectVisitor{TResult})"/>
/// hands a value object's type arguments to an <see cref="Metadata.IValueObjectVisitor{TResult}"/>. An integration that
/// finds identifiers in <see cref="EntityIdRegistry"/> creates its adapter there, <c>new MyGenerator&lt;TId&gt;()</c>,
/// where <see cref="Type.MakeGenericType(Type[])"/> would close it at run time, which native AOT cannot do for a struct;
/// and it reaches the members only an identifier has, <see cref="IEntityId{TSelf}.New()"/> among them, through the
/// constraint.
/// </remarks>
public interface IEntityIdVisitor<out TResult>
{
    /// <summary>
    /// Receives the type of the identifier.
    /// </summary>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <returns>What the visitor builds for the identifier.</returns>
    TResult Visit<TId>()
        where TId : struct, IEntityId<TId>;
}
