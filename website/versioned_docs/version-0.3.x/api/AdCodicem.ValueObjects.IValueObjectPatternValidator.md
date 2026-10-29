# Interface IValueObjectPatternValidator {#AdCodicem_ValueObjects_IValueObjectPatternValidator}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares that a string value object must match a regular expression.

```csharp
public interface IValueObjectPatternValidator
```

## Remarks

<p>
Implement it with a source-generated regular expression, which the regex generator can only write for code a
consumer wrote by hand:
<code>[GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }</code>.
It runs compiled under native AOT, where a <xref href="System.Text.RegularExpressions.Regex" data-throw-if-not-resolved="false"></xref> built at run time
is interpreted, and costs nothing until it first runs.
</p>
<p>
The pattern runs after <code>MinLength</code> and <code>MaxLength</code>, before the known values and
<xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>, and a value it does not match is rejected as
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.InvalidFormat" data-throw-if-not-resolved="false"></xref>. It is declared once: its text is also the OpenAPI
<code>pattern</code>, read off the <code>[GeneratedRegex]</code> attribute when the type compiles. That text carries no
<xref href="System.Text.RegularExpressions.RegexOptions" data-throw-if-not-resolved="false"></xref>, so a rule like case insensitivity belongs in the
pattern itself.
</p>
<p>
It replaces the <code>Pattern</code> option of <code>[ValueObject&lt;T&gt;]</code>, which builds its regular expression at
run time. Implementing it on a value object that is not a string, or on an <code>[EntityId]</code>, which owns its
format, is a build error.
</p>

## Properties

### Pattern {#AdCodicem_ValueObjects_IValueObjectPatternValidator_Pattern}

Gets the regular expression a value must match.

```csharp
public static abstract Regex Pattern { get; }
```

#### Property Value

 [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex)

