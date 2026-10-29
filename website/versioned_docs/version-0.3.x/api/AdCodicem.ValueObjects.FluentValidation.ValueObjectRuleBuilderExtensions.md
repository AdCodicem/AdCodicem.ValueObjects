# Class ValueObjectRuleBuilderExtensions {#AdCodicem_ValueObjects_FluentValidation_ValueObjectRuleBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.FluentValidation](AdCodicem.ValueObjects.FluentValidation.md)  
Assembly: AdCodicem.ValueObjects.FluentValidation.dll  

FluentValidation rules built on the rules a value object already enforces.

```csharp
public static class ValueObjectRuleBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectRuleBuilderExtensions](AdCodicem.ValueObjects.FluentValidation.ValueObjectRuleBuilderExtensions.md)

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
The point is to state a rule once. A command carrying a raw <code>string</code> for an IBAN should not restate the
length, the pattern and the check-digit rule in its validator: it should defer to the value object that owns
them, and report the same stable error code the rest of the system uses.
</p>
<p>
A failure of <xref href="AdCodicem.ValueObjects.FluentValidation.ValueObjectRuleBuilderExtensions.MustParseAs%60%601(FluentValidation.IRuleBuilder%7b%60%600%2cSystem.String%7d%2cSystem.Type)" data-throw-if-not-resolved="false"></xref> or <xref href="AdCodicem.ValueObjects.FluentValidation.ValueObjectRuleBuilderExtensions.MustSatisfy%60%603(FluentValidation.IRuleBuilder%7b%60%600%2c%60%602%7d)" data-throw-if-not-resolved="false"></xref> carries, by default, the
code and the message of the value object's rule that refused the value. The options chained on either replace
them as they would on any rule: <code>WithErrorCode</code>, <code>WithMessage</code>, <code>WithSeverity</code>, <code>WithState</code>
and <code>WithName</code>. A message template can quote the value object's own message as <code>{Reason}</code>, besides the
usual <code>{PropertyName}</code>, <code>{PropertyValue}</code>, <code>{PropertyPath}</code> and, in a child validator run for each
element of a collection, <code>{CollectionIndex}</code>. The global options apply as well: the default severity, and the
<code>OnFailureCreated</code> callback.
</p>
<p>
A template can also quote <code>{ValueObject}</code>, the name of the value object type, and the arguments its declared
rules supply for the code that refused the value, formatted in the current culture: <code>{MinLength}</code> for
<code>value_object.too_short</code>, <code>{MaxLength}</code> for <code>value_object.too_long</code>, <code>{Minimum}</code> and
<code>{Maximum}</code> for <code>value_object.out_of_range</code>, either empty when the type declares no such bound, and
<code>{KnownValues}</code> for <code>value_object.not_a_known_value</code>, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.ArgumentsFor(AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%2cSystem.String%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref>
reads them. FluentValidation leaves a placeholder it has no value for, a positional one included, as it is.
</p>
<p>
Unless a message is chained, a failure looks its message up in <xref href="FluentValidation.ValidatorOptions" data-throw-if-not-resolved="false"></xref>'
<code>LanguageManager</code>, which reads the current UI culture, under the keys of
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeysFor(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.String)" data-throw-if-not-resolved="false"></xref>: <code>ValueObject.value_object.too_short</code>, after
<code>ValueObject.value_object.out_of_range.minimum</code> or <code>.maximum</code> for a type declaring one bound alone. An
application adds its translations to a <code>LanguageManager</code> of its own, as FluentValidation documents it, and the
value object's English message, <code>{Reason}</code>, stands for any key it does not translate. The key is that of the code
of the value object's rule, whose arguments the message quotes, whatever code <code>WithErrorCode</code> chains.
<xref href="AdCodicem.ValueObjects.FluentValidation.ValueObjectRuleBuilderExtensions.NotDefault%60%603(FluentValidation.IRuleBuilder%7b%60%600%2c%60%601%7d)" data-throw-if-not-resolved="false"></xref> does the same under <code>ValueObject.value_object.required</code>.
</p>

## Methods

### MustParseAs<T\>\(IRuleBuilder<T, string?\>, Type\) {#AdCodicem_ValueObjects_FluentValidation_ValueObjectRuleBuilderExtensions_MustParseAs__1_FluentValidation_IRuleBuilder___0_System_String__System_Type_}

Requires the text to be acceptable to a value object type.

```csharp
public static IRuleBuilderOptions<T, string?> MustParseAs<T>(this IRuleBuilder<T, string?> ruleBuilder, Type valueObjectType)
```

#### Parameters

`ruleBuilder` IRuleBuilder<T, [string](https://learn.microsoft.com/dotnet/api/system.string)?\>

Rule builder for a text member.

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

Value object the text must parse into.

#### Returns

 IRuleBuilderOptions<T, [string](https://learn.microsoft.com/dotnet/api/system.string)?\>

The rule, so it can be configured further.

#### Type Parameters

`T` 

Validated object.

#### Remarks

<p>
A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> passes: whether the member is required is <code>NotNull</code>'s or <code>NotEmpty</code>'s to
say, as everywhere in FluentValidation. Empty text is the value object's to judge, as any text is: a string
value object refuses it as required unless it allows empty text, and one over another type cannot parse it.
</p>
<p>
The value object type is passed as a <xref href="System.Type" data-throw-if-not-resolved="false"></xref> rather than a type argument so that
<code>RuleFor(x =&gt; x.Iban).MustParseAs(typeof(Iban))</code> stays readable: C# cannot infer one type argument
while another is given explicitly, and spelling out the validated type at every rule is noise.
</p>

### MustSatisfy<T, TSelf, TValue\>\(IRuleBuilder<T, TValue\>\) {#AdCodicem_ValueObjects_FluentValidation_ValueObjectRuleBuilderExtensions_MustSatisfy__3_FluentValidation_IRuleBuilder___0___2__}

Requires an underlying value to satisfy the rules of a value object, without constructing it.

```csharp
public static IRuleBuilderOptions<T, TValue> MustSatisfy<T, TSelf, TValue>(this IRuleBuilder<T, TValue> ruleBuilder) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`ruleBuilder` IRuleBuilder<T, TValue\>

Rule builder for a member holding the underlying value.

#### Returns

 IRuleBuilderOptions<T, TValue\>

The rule, so it can be configured further.

#### Type Parameters

`T` 

Validated object.

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Remarks

A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> passes: whether the member is required is <code>NotNull</code>'s or <code>NotEmpty</code>'s to
say, as everywhere in FluentValidation.

### NotDefault<T, TSelf, TValue\>\(IRuleBuilder<T, TSelf\>\) {#AdCodicem_ValueObjects_FluentValidation_ValueObjectRuleBuilderExtensions_NotDefault__3_FluentValidation_IRuleBuilder___0___1__}

Requires a value object member not to equal <code>default(TSelf)</code>, through
<xref href="AdCodicem.ValueObjects.IValueObject%602.IsDefault" data-throw-if-not-resolved="false"></xref>.

```csharp
public static IRuleBuilderOptions<T, TSelf> NotDefault<T, TSelf, TValue>(this IRuleBuilder<T, TSelf> ruleBuilder) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`ruleBuilder` IRuleBuilder<T, TSelf\>

Rule builder for a value object member.

#### Returns

 IRuleBuilderOptions<T, TSelf\>

The rule, so it can be configured further.

#### Type Parameters

`T` 

Validated object.

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Remarks

<p>
Catches the one hole a struct value object cannot close by itself: an uninitialized instance, which the
CLR always allows and which the analyzer only catches where it can see the code.
</p>
<p>
Over a value type, a constructed instance holding the type's zero (<code>0</code>, <xref href="System.Guid.Empty" data-throw-if-not-resolved="false"></xref>, …) equals
the default too, so the rule refuses a valid zero as <code>value_object.required</code>. Keep it for a value object
over <xref href="System.String" data-throw-if-not-resolved="false"></xref>, or over a value type whose rules refuse its zero anyway.
</p>
<p>
Unless a message is chained, a failure carries the message <xref href="FluentValidation.ValidatorOptions" data-throw-if-not-resolved="false"></xref>' <code>LanguageManager</code>
holds under <code>ValueObject.value_object.required</code> for the current UI culture, or
<code>'{PropertyName}' is required.</code> when it holds none. A template can quote <code>{ValueObject}</code>, the name of the
value object type, besides FluentValidation's own placeholders.
</p>

