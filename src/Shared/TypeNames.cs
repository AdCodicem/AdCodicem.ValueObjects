namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Writes a type the way C# names it in code, for a message that shows the registration to add: the test-data sampler
/// and each library adapter over it link this file, so that each writes its own registration with the same names.
/// </summary>
internal static class TypeNames
{
    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
    };

    /// <summary>
    /// Writes a type: a keyword for a type C# has one for, the types it is nested in before it, and its type arguments
    /// in angle brackets, <c>Ordering.OrderReference</c> or <c>Code&lt;PurchaseOrder&gt;</c>; never its namespace.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>Its name.</returns>
    public static string Of(Type type)
    {
        if (Keywords.TryGetValue(type, out var keyword))
        {
            return keyword;
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Of(underlying) + "?";
        }

        if (type.IsArray)
        {
            return Of(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        return type.IsGenericParameter ? type.Name : Named(type, type.GetGenericArguments());
    }

    /// <summary>
    /// Writes a type that is neither a keyword, an array nor a nullable value type, given the type arguments of the
    /// outermost construction, of which the types it is nested in take the first ones.
    /// </summary>
    private static string Named(Type type, Type[] arguments)
    {
        var declaring = type.DeclaringType;
        var inherited = declaring?.GetGenericArguments().Length ?? 0;
        var prefix = declaring is null ? string.Empty : Named(declaring, arguments) + ".";
        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        if (tick < 0)
        {
            return prefix + type.Name;
        }

        var own = arguments[inherited..type.GetGenericArguments().Length];
        return prefix + type.Name[..tick] + "<" + string.Join(", ", own.Select(Of)) + ">";
    }
}
