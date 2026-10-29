# Interface IValueObjectNormalizer<TValue\> {#AdCodicem_ValueObjects_IValueObjectNormalizer_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares that a value object normalizes its underlying value before validating it.

```csharp
public interface IValueObjectNormalizer<TValue>
```

#### Type Parameters

`TValue` 

Underlying value type.

## Remarks

<p>
Implement this to give a value object a normalization rule: an IBAN without its separators and upper-cased,
a phone number carrying its country code. The generator calls <xref href="AdCodicem.ValueObjects.IValueObjectNormalizer%601.NormalizeValue(%600)" data-throw-if-not-resolved="false"></xref> from the
<code>Normalize</code> member of <xref href="AdCodicem.ValueObjects.IValueObject%602" data-throw-if-not-resolved="false"></xref>, which additionally guards against a
null underlying value, so the rule itself never has to.
</p>
<p>
Normalization must be idempotent and must never reject: normalizing an already normalized value returns it
unchanged, and a value that cannot be normalized is rejected by <xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>
instead.
</p>

## Methods

### NormalizeValue\(TValue\) {#AdCodicem_ValueObjects_IValueObjectNormalizer_1_NormalizeValue__0_}

Puts a value into its canonical form.

```csharp
public static abstract TValue NormalizeValue(TValue value)
```

#### Parameters

`value` TValue

Value to normalize. Never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

#### Returns

 TValue

The canonical form of the value.

