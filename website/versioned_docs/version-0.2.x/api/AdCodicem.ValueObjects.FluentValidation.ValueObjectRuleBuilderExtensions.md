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

The point is to state a rule once. A command carrying a raw <code>string</code> for an IBAN should not restate the
length, the pattern and the check-digit rule in its validator: it should defer to the value object that owns
them, and report the same stable error code the rest of the system uses.

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

The value object type is passed as a <xref href="System.Type" data-throw-if-not-resolved="false"></xref> rather than a type argument so that
<code>RuleFor(x =&gt; x.Iban).MustParseAs(typeof(Iban))</code> stays readable: C# cannot infer one type argument
while another is given explicitly, and spelling out the validated type at every rule is noise.

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

### NotDefault<T, TSelf, TValue\>\(IRuleBuilder<T, TSelf\>\) {#AdCodicem_ValueObjects_FluentValidation_ValueObjectRuleBuilderExtensions_NotDefault__3_FluentValidation_IRuleBuilder___0___1__}

Requires a value object member to hold a value that actually went through validation.

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

Catches the one hole a struct value object cannot close by itself: an uninitialized instance, which the
CLR always allows and which the analyzer only catches where it can see the code.

