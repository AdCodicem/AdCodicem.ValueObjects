using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

// Value objects declaring their text non-Unicode, owned by the Entity Framework Core, Dapper and descriptor tests of the
// option, and schemas stating it Unicode, which leaves the column and the parameter to the provider. Each value object
// declaring the option holds its text to ASCII, as VO0047 asks: Bic, in Domain/UnderlyingTypes.cs, is the one every
// generated member family and every integration runs on.

/// <summary>A tag, held to ASCII by its pattern and declared non-Unicode, with no maximum length.</summary>
[ValueObject<string>(Unicode = false)]
public readonly partial struct AsciiTag : IValueObjectPatternValidator
{
    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>
/// A reference to a document of some owner, held to ASCII and declared non-Unicode: each test closes it over an owner of
/// its own, which nothing else resolves.
/// </summary>
/// <typeparam name="TOwner">The owner of the document.</typeparam>
[ValueObject<string>(MaxLength = 12, Unicode = false)]
public readonly partial struct AsciiReference<TOwner> : IValueObjectPatternValidator
    where TOwner : class
{
    [GeneratedRegex("^[A-Z0-9-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>
/// A value object the Entity Framework Core tests register by hand with a schema declaring its text non-Unicode, where
/// its type declares Unicode text.
/// </summary>
[ValueObject<string>(MaxLength = 12)]
public readonly partial struct RegisteredMark;

/// <summary>
/// The schema of a value object written by hand declaring its text non-Unicode, read through
/// <see cref="Declared{TDeclaration, TValue}"/> on the typed paths alone: nothing resolves it through the registry.
/// </summary>
public sealed class AsciiDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema => new() { MaxLength = 20, IsUnicode = false };
}

/// <summary>
/// The same schema, which the descriptor and Entity Framework Core tests resolve through the registry, where it stays for
/// the process: no other test names it.
/// </summary>
public sealed class RegisteredAsciiDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema => new() { MaxLength = 20, IsUnicode = false };
}

/// <summary>
/// The schema of a value object written by hand stating its text Unicode, which leaves the column and the parameter to
/// the provider as stating nothing does, read through <see cref="Declared{TDeclaration, TValue}"/> on the typed paths
/// alone: nothing resolves it through the registry.
/// </summary>
public sealed class UnicodeDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema => new() { MaxLength = 20, IsUnicode = true };
}

/// <summary>
/// The same schema, which the Entity Framework Core tests resolve through the registry, where it stays for the process:
/// no other test names it.
/// </summary>
public sealed class RegisteredUnicodeDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema => new() { MaxLength = 20, IsUnicode = true };
}
