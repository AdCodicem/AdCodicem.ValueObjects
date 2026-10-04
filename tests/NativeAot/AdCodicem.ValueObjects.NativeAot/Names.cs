namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>Names a type as C# writes it, so that a line reads the same in both runs and to a reviewer.</summary>
internal static class Names
{
    public static string Of(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return $"{Of(underlying)}?";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];

        return type.IsGenericTypeDefinition
            ? $"{name}<{new string(',', type.GetGenericArguments().Length - 1)}>"
            : $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Of))}>";
    }
}
