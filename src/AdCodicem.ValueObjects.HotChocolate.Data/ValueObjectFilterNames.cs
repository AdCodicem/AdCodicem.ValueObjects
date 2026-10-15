using HotChocolate.Data.Filters;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// Names the package's filter types after the scalar of their value object.
/// </summary>
internal static class ValueObjectFilterNames
{
    /// <summary>
    /// Names a filter type after another type of the schema once that one is named, keeping the prefix HotChocolate gives
    /// a type of a named convention and a name a subclass sets itself.
    /// </summary>
    /// <param name="descriptor">The filter type's descriptor.</param>
    /// <param name="dependency">The type it is named after: the scalar, or the element of a list.</param>
    /// <param name="name">Writes the name from the dependency's, its convention's prefix left out.</param>
    /// <remarks>
    /// HotChocolate's own filter interceptor prefixes the name of a type of a named convention, <c>s_</c>, before the
    /// type's naming tasks run, so the task puts the prefix back on the name it writes; a name that is neither the one
    /// the convention gave nor that one prefixed was set by a subclass, which keeps it.
    /// </remarks>
    public static void NameAfter(IFilterInputTypeDescriptor descriptor, TypeReference dependency, Func<string, string> name)
    {
        var extension = descriptor.Extend();
        var scope = extension.Configuration.Scope;
        var prefix = scope is null ? string.Empty : scope + "_";
        var conventional = extension.Configuration.Name;
        extension.Configuration.NeedsNameCompletion = true;
        extension
            .OnBeforeNaming((context, configuration) =>
            {
                var current = configuration.Name;
                if (current != conventional && current != prefix + conventional)
                {
                    return;
                }

                var named = context.GetType<IType>(dependency).NamedType().Name;
                configuration.Name = prefix + name(named.StartsWith(prefix, StringComparison.Ordinal) ? named[prefix.Length..] : named);
            })
            .DependsOn(dependency, mustBeNamed: true);
    }
}
