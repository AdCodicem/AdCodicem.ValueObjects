using System.Globalization;
using AdCodicem.ValueObjects.AspNetCore.ModelBinding;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Primitives;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Which model types the value object binder provider claims. MVC asks it about every parameter and property type
/// of every action, before any request, so a type it cannot bind must be declined, not answered with an exception.
/// </summary>
public class ModelBinderProviderTests
{
    private static readonly Dictionary<string, BindingSource> Sources = new()
    {
        [nameof(BindingSource.Header)] = BindingSource.Header,
        [nameof(BindingSource.Body)] = BindingSource.Body,
        [nameof(BindingSource.Services)] = BindingSource.Services,
        [nameof(BindingSource.Custom)] = BindingSource.Custom,
        [nameof(BindingSource.ModelBinding)] = BindingSource.ModelBinding,
        [nameof(BindingSource.Path)] = BindingSource.Path,
        [nameof(BindingSource.Query)] = BindingSource.Query,
        [nameof(BindingSource.Form)] = BindingSource.Form,
    };

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
    /// A greedy binding source, a header, the body, the services or a binder the parameter names, is bound by MVC's own
    /// provider for it, which the value object binder, inserted first, would otherwise shadow, reading a route, query or
    /// form value of the same name instead. MVC's header binder asks for the value object binder again, under
    /// <see cref="BindingSource.ModelBinding"/>.
    /// </summary>
    /// <param name="source">The name of the binding source.</param>
    [Theory]
    [InlineData(nameof(BindingSource.Header))]
    [InlineData(nameof(BindingSource.Body))]
    [InlineData(nameof(BindingSource.Services))]
    [InlineData(nameof(BindingSource.Custom))]
    public void A_greedy_binding_source_is_declined(string source)
        => GetBinder(new ValueObjectModelBinderProvider(), typeof(CountryCode), Sources[source]).Should().BeNull();

    /// <summary>
    /// A source the value providers serve, narrowed to it, is the binder's, as a parameter with no source is.
    /// </summary>
    /// <param name="source">The name of the binding source.</param>
    [Theory]
    [InlineData(nameof(BindingSource.ModelBinding))]
    [InlineData(nameof(BindingSource.Path))]
    [InlineData(nameof(BindingSource.Query))]
    [InlineData(nameof(BindingSource.Form))]
    public void A_source_the_value_providers_serve_gets_the_binder(string source)
        => GetBinder(new ValueObjectModelBinderProvider(), typeof(CountryCode), Sources[source])
            .Should().BeOfType<ValueObjectModelBinder<CountryCode, string>>();

    /// <summary>
    /// A greedy source is declined before the binders are cached, by type alone: declining one leaves the binder of the
    /// type to every other source, in either order.
    /// </summary>
    [Fact]
    public void Declining_a_greedy_source_leaves_the_binder_of_the_type_to_the_other_sources()
    {
        var provider = new ValueObjectModelBinderProvider();

        GetBinder(provider, typeof(Reference<GreedySourceOwner>), BindingSource.Header).Should().BeNull();
        GetBinder(provider, typeof(Reference<GreedySourceOwner>), BindingSource.Query)
            .Should().BeOfType<ValueObjectModelBinder<Reference<GreedySourceOwner>, string>>();
        GetBinder(provider, typeof(Reference<GreedySourceOwner>), BindingSource.Header).Should().BeNull();
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

    /// <summary>
    /// A binder an application builds itself, outside any provider, has no messages configured, and answers a refusal
    /// with the English message of the rule.
    /// </summary>
    [Fact]
    public async Task A_binder_built_by_hand_refuses_with_the_English_message_of_the_rule()
    {
        var context = new DefaultModelBindingContext
        {
            ActionContext = new ActionContext { HttpContext = new DefaultHttpContext() },
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(Quantity)),
            ModelName = "quantity",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                new QueryCollection(new Dictionary<string, StringValues> { ["quantity"] = "5000" }),
                CultureInfo.InvariantCulture),
        };

        await new ValueObjectModelBinder<Quantity, short>().BindModelAsync(context);

        context.ModelState["quantity"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(MinimalApiProblemDetailsTests.Refusal<Quantity, short>("5000").Message);
    }

    private static IModelBinder? GetBinder(Type modelType)
    {
        var context = Substitute.For<ModelBinderProviderContext>();
        context.Metadata.Returns(new EmptyModelMetadataProvider().GetMetadataForType(modelType));

        return new ValueObjectModelBinderProvider().GetBinder(context);
    }

    private static IModelBinder? GetBinder(ValueObjectModelBinderProvider provider, Type modelType, BindingSource source)
    {
        var context = Substitute.For<ModelBinderProviderContext>();
        context.Metadata.Returns(new EmptyModelMetadataProvider().GetMetadataForType(modelType));
        context.BindingInfo.Returns(new BindingInfo { BindingSource = source });

        return provider.GetBinder(context);
    }

    /// <summary>
    /// The owner of the construction only this class registers. The registry and the binder cache are process-wide: a
    /// construction another test uses may already have its binder, and would no longer be described by reflection
    /// there once registered here.
    /// </summary>
    private sealed class BinderOwner;

    /// <summary>
    /// The owner of the construction only the test of the cache binds, so that no other test has cached its binder.
    /// </summary>
    private sealed class GreedySourceOwner;
}
