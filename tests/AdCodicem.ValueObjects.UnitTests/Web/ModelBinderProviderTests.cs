using AdCodicem.ValueObjects.AspNetCore.ModelBinding;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Which model types the value object binder provider claims. MVC asks it about every parameter and property type
/// of every action, before any request, so a type it cannot bind must be declined, not answered with an exception.
/// </summary>
public class ModelBinderProviderTests
{
    [Theory]
    [InlineData(typeof(Iban), typeof(ValueObjectModelBinder<Iban, string>))]
    [InlineData(typeof(CountryCode?), typeof(ValueObjectModelBinder<CountryCode, string>))]
    [InlineData(typeof(AccountId), typeof(ValueObjectModelBinder<AccountId, string>))]
    [InlineData(typeof(HandWrittenCounter), typeof(ValueObjectModelBinder<HandWrittenCounter, int>))]
    [InlineData(typeof(Reference<PurchaseOrder>), typeof(ValueObjectModelBinder<Reference<PurchaseOrder>, string>))]
    [InlineData(typeof(Catalog<string>.Stock?), typeof(ValueObjectModelBinder<Catalog<string>.Stock, int>))]
    [InlineData(typeof(IShipping.Carrier), typeof(ValueObjectModelBinder<IShipping.Carrier, string>))]
    [InlineData(typeof(LedgerBalance), typeof(ValueObjectModelBinder<LedgerBalance, Int128>))]
    [InlineData(typeof(Fingerprint?), typeof(ValueObjectModelBinder<Fingerprint, UInt128>))]
    public void A_value_object_and_its_nullable_get_the_binder_closed_over_the_value_object(Type modelType, Type binderType)
        => GetBinder(modelType).Should().BeOfType(binderType);

    /// <summary>
    /// A construction of a generic value object registered by hand, as native AOT asks, is described by the
    /// descriptor built in code, never by reflection, and its binder is closed over the type arguments that descriptor
    /// hands back.
    /// </summary>
    [Fact]
    public void A_construction_registered_by_hand_gets_the_binder_closed_over_it()
    {
        ValueObjectRegistry.Register<Reference<BinderOwner>, string>(static () => new Reference<BinderOwner>.ValueJsonConverter());

        GetBinder(typeof(Reference<BinderOwner>?)).Should().BeOfType<ValueObjectModelBinder<Reference<BinderOwner>, string>>();
    }

    /// <summary>
    /// Each of these carries the value object marker, and none is a struct implementing
    /// <see cref="IValueObject{TSelf, TValue}"/> over itself, the only shape the binder can be closed over. MVC's
    /// own binders then get their turn.
    /// </summary>
    [Theory]
    [InlineData(typeof(MarkerOnlyValue))]
    [InlineData(typeof(ClassBackedValue))]
    [InlineData(typeof(SelflessValue))]
    [InlineData(typeof(SelflessValue?))]
    [InlineData(typeof(IEntityId))]
    [InlineData(typeof(IValueObject<string>))]
    public void A_type_the_binder_cannot_be_closed_over_is_declined(Type modelType)
        => GetBinder(modelType).Should().BeNull();

    [Fact]
    public void A_type_that_is_no_value_object_is_declined()
        => GetBinder(typeof(string)).Should().BeNull();

    private static IModelBinder? GetBinder(Type modelType)
    {
        var context = Substitute.For<ModelBinderProviderContext>();
        context.Metadata.Returns(new EmptyModelMetadataProvider().GetMetadataForType(modelType));

        return new ValueObjectModelBinderProvider().GetBinder(context);
    }

    /// <summary>
    /// The owner of the construction only this class registers. The registry and the binder cache are process-wide: a
    /// construction another test uses may already have its binder, and would no longer be described by reflection
    /// there once registered here.
    /// </summary>
    private sealed class BinderOwner;
}
