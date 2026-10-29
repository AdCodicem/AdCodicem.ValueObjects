# Class ParsesAsAttribute {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Requires text to be accepted by a value object, and reports the message and the code of the rule that refuses it.

```csharp
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field|AttributeTargets.Parameter, AllowMultiple = false)]
public class ParsesAsAttribute : ValidationAttribute
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ← 
[ValidationAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute) ← 
[ParsesAsAttribute](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.md)

#### Derived

[ParsesAsAttribute<TSelf, TValue\>](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute\-2.md)

#### Inherited Members

[ValidationAttribute.FormatErrorMessage\(string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.formaterrormessage), 
[ValidationAttribute.GetValidationResult\(object?, ValidationContext\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.getvalidationresult), 
[ValidationAttribute.IsValid\(object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.isvalid\#system\-componentmodel\-dataannotations\-validationattribute\-isvalid\(system\-object\)), 
[ValidationAttribute.IsValid\(object?, ValidationContext\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.isvalid\#system\-componentmodel\-dataannotations\-validationattribute\-isvalid\(system\-object\-system\-componentmodel\-dataannotations\-validationcontext\)), 
[ValidationAttribute.Validate\(object?, ValidationContext\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.validate\#system\-componentmodel\-dataannotations\-validationattribute\-validate\(system\-object\-system\-componentmodel\-dataannotations\-validationcontext\)), 
[ValidationAttribute.Validate\(object?, string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.validate\#system\-componentmodel\-dataannotations\-validationattribute\-validate\(system\-object\-system\-string\)), 
[ValidationAttribute.ErrorMessage](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessage), 
[ValidationAttribute.ErrorMessageResourceName](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessageresourcename), 
[ValidationAttribute.ErrorMessageResourceType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessageresourcetype), 
[ValidationAttribute.ErrorMessageString](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessagestring), 
[ValidationAttribute.RequiresValidationContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.requiresvalidationcontext), 
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
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A DTO, an options class or a form model holding the text a value object is made from later states no rule of its own:
<code>[ParsesAs(typeof(Iban))] public string? Account { get; set; }</code> holds the text to the rules the type declares,
its normalization included, wherever DataAnnotations runs: <code>Validator</code>, MVC, <code>AddValidation()</code>, options
validation and <code>ObservableValidator</code>. It validates and changes nothing: the member keeps the text as it was sent.
</p>
<p>
A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> passes, so that <xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref> says whether the member must be there, and so
does an <xref href="System.Collections.Immutable.ImmutableArray%601" data-throw-if-not-resolved="false"></xref> left at its default, which is how an absent member of that type is left. Empty
text is the value object's to judge, as any text is. A <xref href="System.String" data-throw-if-not-resolved="false"></xref> is parsed in the invariant culture, as the
binders read text, unless <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.UseCurrentCulture" data-throw-if-not-resolved="false"></xref> says otherwise; each element of an
<xref href="System.Collections.Generic.IEnumerable%601" data-throw-if-not-resolved="false"></xref> of <xref href="System.String" data-throw-if-not-resolved="false"></xref> is parsed in turn, a <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> element skipped, and
the first refused is reported under the member's own name. A member of any other type throws
<xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref>, a misuse <code>VO0039</code> reports at compile time.
</p>
<p>
A refusal is a <xref href="AdCodicem.ValueObjects.Annotations.ValueObjectValidationResult" data-throw-if-not-resolved="false"></xref>, which carries the code. Its message is the English message of
the rule unless the attribute declares one: <xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessage" data-throw-if-not-resolved="false"></xref>, or a resource with its
name, give DataAnnotations' single message, <code>{0}</code> the member; <xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessageResourceType" data-throw-if-not-resolved="false"></xref>
alone looks the message up by the code of the rule, under the keys of
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectMessages.KeysFor(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.String)" data-throw-if-not-resolved="false"></xref>, with the arguments of the rule from <code>{1}</code>,
in the current UI culture, and keeps the English message for a key the resource lacks. The message never quotes the
text.
</p>
<p>
This form names the value object by its <xref href="System.Type" data-throw-if-not-resolved="false"></xref>, and finds its descriptor the first time a value is
validated: registered, or described by reflection where the runtime supports dynamic code. Under native AOT, a value
object written by hand and a construction of a generic one are found only once registered by hand, or through the type
arguments of <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute%602" data-throw-if-not-resolved="false"></xref>, which needs no registry.
</p>

## Constructors

### ParsesAsAttribute\(Type\) {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute__ctor_System_Type_}

Names the value object the text must be accepted by, which the registry describes the first time a value is
validated.

```csharp
public ParsesAsAttribute(Type valueObjectType)
```

#### Parameters

`valueObjectType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The value object type, or its nullable type, which names the same value object.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">valueObjectType</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

## Properties

### UseCurrentCulture {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute_UseCurrentCulture}

Gets or sets whether the text is read in the current culture rather than the invariant one.

```csharp
public bool UseCurrentCulture { get; set; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

The binders, the configuration binder and <code>MustParseAs</code> read the invariant culture, which refuses
<code>12,5</code>. A form a person types in, in French, may set it, and the application then converts the text in the
same culture when it reads it.

### ValueObjectType {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute_ValueObjectType}

Gets the value object the text must be accepted by: the type named, or the value object a nullable type names,
<code>Iban</code> for <code>typeof(Iban?)</code>, as a refusal reports it.

```csharp
public Type ValueObjectType { get; }
```

#### Property Value

 [Type](https://learn.microsoft.com/dotnet/api/system.type)

## Methods

### FormatErrorMessage\(string\) {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute_FormatErrorMessage_System_String_}

Applies formatting to an error message, based on the data field where the error occurred.

```csharp
public override string FormatErrorMessage(string name)
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

The name to include in the formatted message.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

An instance of the formatted error message.

#### Remarks

The message DataAnnotations formats on its own, outside a validation, as <xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.Validate(System.Object%2cSystem.String)" data-throw-if-not-resolved="false"></xref>
does: with <xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessageResourceType" data-throw-if-not-resolved="false"></xref> alone, which DataAnnotations would refuse,
its default message.

#### Exceptions

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The current attribute is malformed.

### IsValid\(object?, ValidationContext\) {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute_IsValid_System_Object_System_ComponentModel_DataAnnotations_ValidationContext_}

Validates the specified value with respect to the current validation attribute.

```csharp
protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
```

#### Parameters

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The value to validate.

`validationContext` [ValidationContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationcontext)

The context information about the validation operation.

#### Returns

 [ValidationResult](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationresult)?

An instance of the <xref href="System.ComponentModel.DataAnnotations.ValidationResult" data-throw-if-not-resolved="false"></xref> class.

#### Exceptions

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The current attribute is malformed.

 [NotImplementedException](https://learn.microsoft.com/dotnet/api/system.notimplementedexception)

<xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.IsValid(System.Object%2cSystem.ComponentModel.DataAnnotations.ValidationContext)" data-throw-if-not-resolved="false"></xref> has not been implemented by a derived class.

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The member is neither text nor a collection of text, or the type is not a value object the registry describes.

