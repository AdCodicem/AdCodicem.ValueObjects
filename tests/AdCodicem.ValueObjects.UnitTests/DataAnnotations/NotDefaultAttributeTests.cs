using System.ComponentModel.DataAnnotations;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.Web;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.UnitTests.DataAnnotations;

/// <summary>
/// <see cref="NotDefaultAttribute"/> through <see cref="Validator"/>: a value-object member holding the instance equal to
/// its default, which <see cref="RequiredAttribute"/> lets through, refused with <see cref="ValueObjectErrorCodes.Required"/>.
/// </summary>
public partial class NotDefaultAttributeTests
{
#pragma warning disable VO0010 // The uninitialized instance is what the attribute refuses.
    private static Iban DefaultIban => default;
#pragma warning restore VO0010

    /// <summary>
    /// <see cref="RequiredAttribute"/> passes on a struct, which is never <see langword="null"/>, and so on a value object
    /// at its default; <see cref="NotDefaultAttribute"/> refuses it, with <see cref="RequiredAttribute"/>'s message.
    /// </summary>
    [Fact]
    public void A_value_object_left_at_its_default_is_refused_where_Required_lets_it_through()
    {
        var command = new SettleCommand { Fallback = DefaultIban };
        var results = new List<AnnotationResult>();

        Validator.TryValidateObject(command, new ValidationContext(command), results, validateAllProperties: true).Should().BeFalse();

        results.Should().AllBeOfType<ValueObjectValidationResult>();
        results.Cast<ValueObjectValidationResult>().Select(static result => (result.MemberNames.Single(), result.ErrorMessage, result.ErrorCode, result.ValueObjectType))
            .Should().Equal(
                (nameof(SettleCommand.Account), "The Account field is required.", ValueObjectErrorCodes.Required, typeof(Iban)),
                (nameof(SettleCommand.Customer), "The Customer field is required.", ValueObjectErrorCodes.Required, typeof(CustomerId)),
                (nameof(SettleCommand.Fallback), "The Fallback field is required.", ValueObjectErrorCodes.Required, typeof(Iban)));
    }

    [Fact]
    public void A_value_object_created_from_a_value_passes_and_an_absent_nullable_one_is_left_to_Required()
    {
        var command = new SettleCommand
        {
            Account = Iban.Create("FR7630006000011234567890189"),
            Customer = CustomerId.New(),
            Fallback = null,
        };

        Validator.TryValidateObject(command, new ValidationContext(command), [], validateAllProperties: true).Should().BeTrue();
        Annotated.Check(new NotDefaultAttribute(), null).Should().BeNull();
    }

    /// <summary>
    /// Over a value type, a constructed zero equals the default and is refused too, as FluentValidation's
    /// <c>NotDefault</c> refuses it, though the type's rules may accept it.
    /// </summary>
    [Fact]
    public void A_constructed_zero_equal_to_the_default_is_refused_too()
    {
        var zero = PageCount.Create(0);

        Annotated.Refusal(new NotDefaultAttribute(), zero).ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        Annotated.Check(new NotDefaultAttribute(), PageCount.Create(1)).Should().BeNull();
    }

    /// <summary>
    /// A value object written by hand, and a construction of a generic one, which no generated registration lists, are
    /// described by the registry by reflection the first time one is checked.
    /// </summary>
    [Fact]
    public void A_value_object_nothing_registered_is_described_the_first_time_one_is_checked()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<NotDefaultAttributeTests>), out _).Should().BeFalse("nothing asked for it yet");

#pragma warning disable VO0010 // The uninitialized instances are what the attribute refuses.
        var tag = Annotated.Refusal(new NotDefaultAttribute(), default(HandWrittenTag<NotDefaultAttributeTests>));
        var reference = Annotated.Refusal(new NotDefaultAttribute(), default(Reference<NotDefaultAttributeTests>));
#pragma warning restore VO0010

        tag.ValueObjectType.Should().Be<HandWrittenTag<NotDefaultAttributeTests>>();
        reference.ValueObjectType.Should().Be<Reference<NotDefaultAttributeTests>>();
        ValueObjectRegistry.TryGet(typeof(Reference<NotDefaultAttributeTests>), out _).Should().BeTrue();
        Annotated.Check(new NotDefaultAttribute(), HandWrittenTag<NotDefaultAttributeTests>.Create("tag")).Should().BeNull();
        Annotated.Check(new NotDefaultAttribute(), Reference<NotDefaultAttributeTests>.Create("PO-1")).Should().BeNull();
    }

    /// <summary>A member that is no value object, a collection of them included, is a misuse, named with its type.</summary>
    [Fact]
    public void A_member_that_is_no_value_object_is_refused_naming_its_type()
    {
        FluentActions.Invoking(() => Annotated.Check(new NotDefaultAttribute(), 0, "Count")).Should().Throw<InvalidOperationException>()
            .WithMessage("[NotDefault] checks a value object, and 'Count' is of type 'Int32'.");
        FluentActions.Invoking(() => Annotated.Check(new NotDefaultAttribute(), new List<Iban>(), "Accounts")).Should().Throw<InvalidOperationException>()
            .WithMessage("[NotDefault] checks a value object, and 'Accounts' is of type 'List`1'.");
    }

    /// <summary>
    /// A type that carries the marker alone is no value object the registry describes: refused, and asked again the next
    /// time, so that a registration made later is found.
    /// </summary>
    [Fact]
    public void A_type_carrying_the_marker_alone_is_refused_each_time()
    {
        var check = () => Annotated.Check(new NotDefaultAttribute(), default(MarkerOnlyValue), "Marker");

        check.Should().Throw<InvalidOperationException>()
            .WithMessage("[NotDefault] checks 'MarkerOnlyValue', which is not a value object the registry describes. Under native AOT, *"
                         + "have to be registered before a value is validated.");
        check.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_message_the_attribute_declares_replaces_Requireds_and_keeps_the_code()
    {
        var refusal = Annotated.Refusal(new NotDefaultAttribute { ErrorMessage = "{0} must be chosen." }, DefaultIban);

        refusal.ErrorMessage.Should().Be("Account must be chosen.");
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// A resource type named alone has the message looked up under <c>ValueObject.value_object.required</c>, as
    /// FluentValidation's <c>NotDefault</c> looks it up, and keeps <see cref="RequiredAttribute"/>'s for a culture the
    /// resource lacks.
    /// </summary>
    [Fact]
    public void A_resource_named_alone_gives_the_message_of_the_required_code()
    {
        var attribute = new NotDefaultAttribute { ErrorMessageResourceType = typeof(ValidationMessages) };

        using (Annotated.Culture("fr-FR"))
        {
            Annotated.Refusal(attribute, DefaultIban).ErrorMessage.Should().Be("« Account » est obligatoire.");
        }

        using (Annotated.Culture("en-US"))
        {
            Annotated.Refusal(attribute, DefaultIban).ErrorMessage.Should().Be("The Account field is required.");
        }

        using (Annotated.Culture("fr-FR"))
        {
            var sparse = new NotDefaultAttribute { ErrorMessageResourceType = typeof(SparseMessages) };
            Annotated.Refusal(sparse, DefaultIban).ErrorMessage.Should().Be("The Account field is required.", "the resource holds no such key");
        }
    }

    [Fact]
    public void The_message_DataAnnotations_formats_on_its_own_is_Requireds()
    {
        new NotDefaultAttribute { ErrorMessageResourceType = typeof(ValidationMessages) }.FormatErrorMessage("Legacy")
            .Should().Be("The Legacy field is required.");
        new NotDefaultAttribute().FormatErrorMessage("Legacy").Should().Be("The Legacy field is required.");
        new NotDefaultAttribute { ErrorMessage = "{0}!" }.FormatErrorMessage("Legacy").Should().Be("Legacy!");
    }

    /// <summary>Validated with no context, as <see cref="ValidationAttribute.IsValid(object?)"/> does.</summary>
    [Fact]
    public void Validated_without_a_context_the_default_is_refused()
    {
        new NotDefaultAttribute().IsValid(DefaultIban).Should().BeFalse();
        new NotDefaultAttribute().IsValid(Iban.Create("FR7630006000011234567890189")).Should().BeTrue();
    }

    /// <summary>The check of a type is closed once, through its descriptor, and serves every value checked after.</summary>
    [Fact]
    public void The_check_of_a_type_serves_every_value_after_it()
    {
        var attribute = new NotDefaultAttribute();

        Annotated.Refusal(attribute, DefaultIban).ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        Annotated.Refusal(new NotDefaultAttribute(), DefaultIban).ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        Annotated.Check(attribute, Iban.Create("DE89370400440532013000")).Should().BeNull();
    }

    /// <summary>A count of pages, whose rules accept zero, which is also its default.</summary>
    [ValueObject<int>]
    public readonly partial struct PageCount : IValueObjectMinimum<int>
    {
        public static int Minimum => 0;
    }
}

/// <summary>The result <see cref="ParsesAsAttribute"/> and <see cref="NotDefaultAttribute"/> refuse a value with.</summary>
public class ValueObjectValidationResultTests
{
    [Fact]
    public void The_result_carries_the_message_the_members_the_type_and_the_code()
    {
        var result = new ValueObjectValidationResult("refused", ["Account"], typeof(Iban), ValueObjectErrorCodes.TooShort);

        result.Should().BeAssignableTo<AnnotationResult>();
        result.ErrorMessage.Should().Be("refused");
        result.MemberNames.Should().Equal("Account");
        result.ValueObjectType.Should().Be<Iban>();
        result.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
        new ValueObjectValidationResult("refused", null, typeof(Iban), "iban.check_digits").MemberNames.Should().BeEmpty();
    }

    [Fact]
    public void A_result_without_a_type_or_a_code_is_refused()
    {
        FluentActions.Invoking(() => new ValueObjectValidationResult("refused", null, null!, ValueObjectErrorCodes.TooShort))
            .Should().Throw<ArgumentNullException>().WithParameterName("valueObjectType");
        FluentActions.Invoking(() => new ValueObjectValidationResult("refused", null, typeof(Iban), null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("errorCode");
        FluentActions.Invoking(() => new ValueObjectValidationResult("refused", null, typeof(Iban), string.Empty))
            .Should().Throw<ArgumentException>().WithParameterName("errorCode");
    }
}
