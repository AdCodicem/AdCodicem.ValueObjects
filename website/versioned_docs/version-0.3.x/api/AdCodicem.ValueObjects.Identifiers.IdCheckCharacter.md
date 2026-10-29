# Class IdCheckCharacter {#AdCodicem_ValueObjects_Identifiers_IdCheckCharacter}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

The trailing check character of an entity identifier.

```csharp
public static class IdCheckCharacter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[IdCheckCharacter](AdCodicem.ValueObjects.Identifiers.IdCheckCharacter.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The character is <code>(seed(prefix) + Σᵢ wᵢ · vᵢ) mod 32</code>, where <code>vᵢ</code> is the Crockford value of the
i-th data character of the body and <code>wᵢ = 2·(i mod 16) + 1</code>. Its purpose is to turn a mistyped or
truncated identifier into a rejection at the boundary — offline, without a database round trip — rather than
into a lookup that misses, or worse, one that hits something else.
</p>
<p>
What it guarantees, stated exactly, because a checksum that promises more than it delivers is worse than
none at all:
</p>
<ul><li>
every single-character substitution in the body is detected: the weights are odd, hence invertible modulo
32, and a non-zero difference of Crockford values can never be congruent to zero modulo 32;
</li><li>
every adjacent transposition is detected unless the two characters' values differ by exactly 16 — the
weight difference between adjacent positions is congruent to 2 modulo 32 everywhere, the wrap included;
</li><li>
the prefix enters through the seed, so the same body under two different prefixes yields a different check
character for 31 prefixes out of 32. A body copied between two identifier types is therefore caught even by
a validator that does not yet know which prefix to expect, which is what <code>AnyEntityId</code> needs
before it has resolved anything;
</li><li>
random corruption slips through with probability 1/32. That is the information-theoretic limit of one check
character over a 32-symbol alphabet, and no scheme does better.
</li></ul>

## Methods

### Compute\(ReadOnlySpan<char\>, ReadOnlySpan<char\>\) {#AdCodicem_ValueObjects_Identifiers_IdCheckCharacter_Compute_System_ReadOnlySpan_System_Char__System_ReadOnlySpan_System_Char__}

Computes the check character of a body.

```csharp
public static char Compute(ReadOnlySpan<char> prefix, ReadOnlySpan<char> bodyData)
```

#### Parameters

`prefix` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Declared prefix of the identifier type, without its trailing separator.

`bodyData` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

The data characters of the body — the time bucket and the random part — with the check character
excluded. Every character must decode; a character that does not contributes nothing, which is
harmless because validation refuses it before the check character is ever consulted.

#### Returns

 [char](https://learn.microsoft.com/dotnet/api/system.char)

The check character.

