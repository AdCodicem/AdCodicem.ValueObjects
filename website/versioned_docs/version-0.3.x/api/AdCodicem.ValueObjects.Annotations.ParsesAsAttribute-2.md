# Class ParsesAsAttribute<TSelf, TValue\> {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute_2}

Namespace: [AdCodicem.ValueObjects.Annotations](AdCodicem.ValueObjects.Annotations.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Requires text to be accepted by a value object named at compile time, through its static members, with no registry.

```csharp
public sealed class ParsesAsAttribute<TSelf, TValue> : ParsesAsAttribute where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

The value object type.

`TValue` 

Its underlying type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) ← 
[ValidationAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.validationattribute) ← 
[ParsesAsAttribute](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.md) ← 
[ParsesAsAttribute<TSelf, TValue\>](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute\-2.md)

#### Inherited Members

[ParsesAsAttribute.ValueObjectType](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.md\#AdCodicem\_ValueObjects\_Annotations\_ParsesAsAttribute\_ValueObjectType), 
[ParsesAsAttribute.UseCurrentCulture](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.md\#AdCodicem\_ValueObjects\_Annotations\_ParsesAsAttribute\_UseCurrentCulture), 
[ParsesAsAttribute.FormatErrorMessage\(string\)](AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.md\#AdCodicem\_ValueObjects\_Annotations\_ParsesAsAttribute\_FormatErrorMessage\_System\_String\_), 
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

It checks and reports as <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref> does, through <xref href="AdCodicem.ValueObjects.IValueObject%602.TryParse(System.ReadOnlySpan%7bSystem.Char%7d%2cSystem.IFormatProvider%2c%600%40%2cAdCodicem.ValueObjects.ValidationResult%40)" data-throw-if-not-resolved="false"></xref>
and <xref href="AdCodicem.ValueObjects.IValueObject%602.Schema" data-throw-if-not-resolved="false"></xref>, so that a value object written by hand, or a construction of a
generic one, needs no registration under native AOT. It derives from <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute" data-throw-if-not-resolved="false"></xref>, so that a
document generator finds both forms as one, and reads <xref href="AdCodicem.ValueObjects.Annotations.ParsesAsAttribute.ValueObjectType" data-throw-if-not-resolved="false"></xref>.

## Constructors

### ParsesAsAttribute\(\) {#AdCodicem_ValueObjects_Annotations_ParsesAsAttribute_2__ctor}

Names the value object through the type arguments.

```csharp
public ParsesAsAttribute()
```

