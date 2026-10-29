# Class KnownValueAttribute {#AdCodicem_ValueObjects_Annotations_KnownValueAttribute}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Marks a static member of a value object as one of its known values.

```csharp
[AttributeUsage(AttributeTargets.Struct|AttributeTargets.Property|AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class KnownValueAttribute : Attribute
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ← 
[KnownValueAttribute](AdCodicem.ValueObjects.Annotations.KnownValueAttribute.md)

#### Inherited Members

[Attribute.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.attribute.equals), 
[Attribute.GetCustomAttribute\(Assembly, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-assembly\-system\-type\)), 
[Attribute.GetCustomAttribute\(Assembly, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-assembly\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttribute\(MemberInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-memberinfo\-system\-type\)), 
[Attribute.GetCustomAttribute\(MemberInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-memberinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttribute\(Module, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-module\-system\-type\)), 
[Attribute.GetCustomAttribute\(Module, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-module\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttribute\(ParameterInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-parameterinfo\-system\-type\)), 
[Attribute.GetCustomAttribute\(ParameterInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattribute\#system\-attribute\-getcustomattribute\(system\-reflection\-parameterinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Assembly\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\)), 
[Attribute.GetCustomAttributes\(Assembly, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Assembly, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\-system\-type\)), 
[Attribute.GetCustomAttributes\(Assembly, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-assembly\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(MemberInfo\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\)), 
[Attribute.GetCustomAttributes\(MemberInfo, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(MemberInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\-system\-type\)), 
[Attribute.GetCustomAttributes\(MemberInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-memberinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Module\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\)), 
[Attribute.GetCustomAttributes\(Module, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(Module, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\-system\-type\)), 
[Attribute.GetCustomAttributes\(Module, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-module\-system\-type\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(ParameterInfo\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\)), 
[Attribute.GetCustomAttributes\(ParameterInfo, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\-system\-boolean\)), 
[Attribute.GetCustomAttributes\(ParameterInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\-system\-type\)), 
[Attribute.GetCustomAttributes\(ParameterInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.getcustomattributes\#system\-attribute\-getcustomattributes\(system\-reflection\-parameterinfo\-system\-type\-system\-boolean\)), 
[Attribute.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.attribute.gethashcode), 
[Attribute.IsDefaultAttribute\(\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefaultattribute), 
[Attribute.IsDefined\(Assembly, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-assembly\-system\-type\)), 
[Attribute.IsDefined\(Assembly, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-assembly\-system\-type\-system\-boolean\)), 
[Attribute.IsDefined\(MemberInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-memberinfo\-system\-type\)), 
[Attribute.IsDefined\(MemberInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-memberinfo\-system\-type\-system\-boolean\)), 
[Attribute.IsDefined\(Module, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-module\-system\-type\)), 
[Attribute.IsDefined\(Module, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-module\-system\-type\-system\-boolean\)), 
[Attribute.IsDefined\(ParameterInfo, Type\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-parameterinfo\-system\-type\)), 
[Attribute.IsDefined\(ParameterInfo, Type, bool\)](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined\#system\-attribute\-isdefined\(system\-reflection\-parameterinfo\-system\-type\-system\-boolean\)), 
[Attribute.Match\(object?\)](https://learn.microsoft.com/dotnet/api/system.attribute.match), 
[Attribute.TypeId](https://learn.microsoft.com/dotnet/api/system.attribute.typeid), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The member is the author's own: a <code>static readonly</code> field, or a static get-only auto-property, of the value
object's type, initialized through <code>Known</code>, a factory the generator writes on every value object.
<code>[KnownValue] public static readonly CountryCode France = Known("FR");</code> adds the value to
<code>CountryCode.KnownValues</code> and surfaces it in the OpenAPI schema. Combined with <xref href="AdCodicem.ValueObjects.Annotations.ValueSetKind.Closed" data-throw-if-not-resolved="false"></xref>
the declared values also become the validation rule of the type. The compiler checks the name of the member and the
type of its value, which any expression of the underlying type can give:
<code>Known(new DateOnly(1970, 1, 1))</code>.
</p>
<p>
<code>Known</code> normalizes the value and applies every rule of the type but membership, which the value satisfies by
declaration. A value the rules refuse throws as the type initializes: it is reported at compile time when the argument
of <code>Known</code> is a constant the generator can evaluate, and would otherwise stop the application before it starts.
<code>Known</code> may be called nowhere else (<code>VO0037</code>), and a member that is not static, can be written, is of
another type or is not initialized through <code>Known</code> is <code>VO0036</code>.
</p>
<p>
The schema keeps the name of the member and the description beside the value
(<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValueDetails" data-throw-if-not-resolved="false"></xref>), and the OpenAPI integration publishes them beside the
<code>enum</code> of a closed set, so that a client generated from the document names the member of its enumeration
<code>France</code> rather than <code>FR</code>. The name is therefore part of that client's contract: renaming a known value
renames its member in every client generated afterwards.
</p>

## Constructors

### KnownValueAttribute\(\) {#AdCodicem_ValueObjects_Annotations_KnownValueAttribute__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.Annotations.KnownValueAttribute" data-throw-if-not-resolved="false"></xref> class, for the member it is applied to.

```csharp
public KnownValueAttribute()
```

### KnownValueAttribute\(string, object\) {#AdCodicem_ValueObjects_Annotations_KnownValueAttribute__ctor_System_String_System_Object_}

Declared a known value on the type, by name and value. Removed: declare the member instead.

```csharp
[Obsolete("Declare the known value as a member of the value object, [KnownValue] public static readonly TSelf Name = Known(value), which a code fix writes. Any minor version may remove this constructor before 1.0.0.", true, DiagnosticId = "VO0034")]
public KnownValueAttribute(string name, object value)
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

The name the generated property took.

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)

The underlying value, written as text for a type an attribute argument cannot carry.

#### Remarks

The name was an identifier the generator created and the value a constant or text it read under a grammar of its
own. A member the author declares is checked by the compiler instead. <code>[KnownValue("France", "FR")]</code> on the
type becomes <code>[KnownValue] public static readonly CountryCode France = Known("FR");</code> in it, which a code fix
writes. Any minor version may remove this constructor before 1.0.0.

## Properties

### Description {#AdCodicem_ValueObjects_Annotations_KnownValueAttribute_Description}

Gets or sets the description of the known value.

```csharp
public string? Description { get; set; }
```

#### Property Value

 [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Kept in <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectSchema.KnownValueDetails" data-throw-if-not-resolved="false"></xref>, unless it is blank, and published with the
value in the OpenAPI document, where client generators turn it into the documentation of the member. Without it,
the <code>&lt;summary&gt;</code> of the member describes the value, as the summary of a value object describes it.

