# Class NotDefaultAttribute {#AdCodicem_ValueObjects_Annotations_NotDefaultAttribute}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Requires a value-object member not to hold the instance equal to <code>default(TSelf)</code>, which
<xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref> lets through, since a struct is never <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

```csharp
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field|AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class NotDefaultAttribute : ValidationAttribute
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ← 
[ValidationAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute) ← 
[NotDefaultAttribute](AdCodicem.ValueObjects.Annotations.NotDefaultAttribute.md)

#### Inherited Members

[ValidationAttribute.FormatErrorMessage\(string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.formaterrormessage), 
[ValidationAttribute.GetValidationResult\(object?, ValidationContext\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.getvalidationresult), 
[ValidationAttribute.IsValid\(object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.isvalid\#system\-componentmodel\-dataannotations\-validationattribute\-isvalid\(system\-object\)), 
[ValidationAttribute.Validate\(object?, ValidationContext\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.validate\#system\-componentmodel\-dataannotations\-validationattribute\-validate\(system\-object\-system\-componentmodel\-dataannotations\-validationcontext\)), 
[ValidationAttribute.Validate\(object?, string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.validate\#system\-componentmodel\-dataannotations\-validationattribute\-validate\(system\-object\-system\-string\)), 
[ValidationAttribute.ErrorMessage](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessage), 
[ValidationAttribute.ErrorMessageResourceName](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessageresourcename), 
[ValidationAttribute.ErrorMessageResourceType](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute.errormessageresourcetype), 
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
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> passes, so that an absent <code>TSelf?</code> is <xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref>'s to judge; a
<code>TSelf?</code> holding the default instance is refused. The check reads
<xref href="AdCodicem.ValueObjects.IValueObject%602.IsDefault" data-throw-if-not-resolved="false"></xref> through the descriptor of the value object's type, so that over a
value type a constructed zero, <code>OrderId.Create(Guid.Empty)</code>, is refused too, as FluentValidation's
<code>NotDefault</code> refuses it. A member that is no value object throws <xref href="System.InvalidOperationException" data-throw-if-not-resolved="false"></xref>, a misuse
<code>VO0041</code> reports at compile time, and so does a collection of value objects, whose elements are not checked.
</p>
<p>
A refusal is a <xref href="AdCodicem.ValueObjects.Annotations.ValueObjectValidationResult" data-throw-if-not-resolved="false"></xref> carrying <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref>, with
<xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref>'s message, <code>The {0} field is required.</code>, so that an answer reads the same
whichever refused. <xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessage" data-throw-if-not-resolved="false"></xref>, or a resource with its name, replace it as on any
attribute; <xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessageResourceType" data-throw-if-not-resolved="false"></xref> alone looks the message up under
<code>ValueObject.value_object.required</code>, <code>{0}</code> the member, as FluentValidation's <code>NotDefault</code> does.
</p>
<p>
The type is found in the registry, or described by reflection where the runtime supports dynamic code: under native
AOT, a value object written by hand and a construction of a generic one are registered by hand first.
</p>

## Constructors

### NotDefaultAttribute\(\) {#AdCodicem_ValueObjects_Annotations_NotDefaultAttribute__ctor}

Creates the attribute, whose message is <xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref>'s, <code>The {0} field is required.</code>.

```csharp
public NotDefaultAttribute()
```

## Methods

### FormatErrorMessage\(string\) {#AdCodicem_ValueObjects_Annotations_NotDefaultAttribute_FormatErrorMessage_System_String_}

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

The message DataAnnotations formats on its own, outside a validation: with
<xref href="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessageResourceType" data-throw-if-not-resolved="false"></xref> alone, which DataAnnotations would refuse,
<xref href="System.ComponentModel.DataAnnotations.RequiredAttribute" data-throw-if-not-resolved="false"></xref>'s.

#### Exceptions

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

The current attribute is malformed.

### IsValid\(object?, ValidationContext\) {#AdCodicem_ValueObjects_Annotations_NotDefaultAttribute_IsValid_System_Object_System_ComponentModel_DataAnnotations_ValidationContext_}

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

The member is no value object, or its type is not a value object the registry describes.

