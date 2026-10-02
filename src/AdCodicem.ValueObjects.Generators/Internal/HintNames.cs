using System.Globalization;
using System.Text;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// Names the file generated for a value object.
/// </summary>
/// <remarks>
/// <para>
/// A hint name has to be unique within the generator, and Roslyn compares hint names ignoring case: a second file
/// under a name already added throws, and the generator then contributes nothing to the compilation at all, every
/// other value object included (CS8785). The name of the type, readable in the file name, cannot be unique on its
/// own: <c>Code</c> and <c>CODE</c> are two types that differ by case only, and <c>@event</c> and <c>_event</c> two
/// that differ by a character no file name holds.
/// </para>
/// <para>
/// So the readable part is followed by a 64-bit FNV-1a hash of the exact qualified name, which tells apart what the
/// readable part loses. It is computed from the declaration alone, as the incremental pipeline needs: two types collide
/// only if their hashes do, which for ten thousand value objects in one compilation has a chance below one in ten
/// billion.
/// </para>
/// </remarks>
internal static class HintNames
{
    /// <summary>
    /// Builds the hint name of the file generated for a type.
    /// </summary>
    /// <param name="qualifiedName">Qualified name of the type, without the <c>global::</c> alias.</param>
    /// <returns>The hint name.</returns>
    public static string For(string qualifiedName)
    {
        var builder = new StringBuilder(qualifiedName.Length + 24);
        foreach (var character in qualifiedName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '.' ? character : '_');
        }

        return builder
            .Append('.')
            .Append(Hash(qualifiedName))
            .Append(".g.cs")
            .ToString();
    }

    /// <summary>
    /// Gets the hash a hint name ends with, which also tells apart the members the registration of each type writes on
    /// the types around it.
    /// </summary>
    /// <param name="qualifiedName">Qualified name of the type, without the <c>global::</c> alias.</param>
    /// <returns>Sixteen lowercase hexadecimal digits.</returns>
    public static string Hash(string qualifiedName) => Fnv1a(qualifiedName).ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>
    /// Hashes text with 64-bit FNV-1a over its UTF-16 code units, the same on every run and every machine.
    /// </summary>
    /// <param name="text">Text to hash.</param>
    /// <returns>The hash.</returns>
    private static ulong Fnv1a(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
