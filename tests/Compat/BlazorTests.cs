using System.Globalization;
using System.Linq.Expressions;
using AdCodicem.ValueObjects.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>The model of a Blazor form of three value objects, one of them optional.</summary>
public sealed class BlazorOrder
{
    /// <summary>Gets or sets the account.</summary>
    public Iban Account { get; set; }

    /// <summary>Gets or sets an optional account.</summary>
    public Iban? Optional { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public Quantity Quantity { get; set; } = Quantity.Create(3);
}

/// <summary>An input whose text the tests set through the protected <c>CurrentValueAsString</c>, as a subclass may.</summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
public sealed class TypedInput<TValue> : InputValueObject<TValue>
{
    /// <summary>Gets or sets what is handed the input once it is initialized.</summary>
    [Parameter]
    public Action<TypedInput<TValue>>? Ready { get; set; }

    /// <summary>Gets the text the input shows.</summary>
    public string? Text => CurrentValueAsString;

    /// <summary>Sets the text of the input, as its change event does.</summary>
    /// <param name="text">The text.</param>
    public void Type(string text) => CurrentValueAsString = text;

    /// <inheritdoc />
    protected override void OnInitialized() => Ready?.Invoke(this);
}

/// <summary>
/// The Blazor input of value objects on the next major, rendered through <see cref="HtmlRenderer"/> on the shared
/// framework's Components, whose <c>InputBase</c> moved into Microsoft.AspNetCore.Components.Web, forwarded: text read
/// through the rules and normalized, a refusal kept in the field with its rule's message and its code on the form, empty
/// text required of a value object and <see langword="null"/> for its nullable type, and the messages of a resource.
/// </summary>
public sealed class BlazorTests
{
    [Fact]
    public async Task Text_typed_into_a_form_is_read_through_the_rules_and_a_refusal_keeps_its_code_on_the_form()
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        var order = new BlazorOrder();
        var form = new EditContext(order);
        var (account, optional, quantity) = await RenderAsync(renderer, order, form);

        await renderer.Dispatcher.InvokeAsync(() => account.Type("fr76 3000 6000 0112 3456 7890 189"));
        order.Account.Should().Be(Iban.Create("FR7630006000011234567890189"));
        account.Text.Should().Be("FR7630006000011234567890189");

        await renderer.Dispatcher.InvokeAsync(() => account.Type("XX"));
        order.Account.Should().Be(Iban.Create("FR7630006000011234567890189"));
        account.Text.Should().Be("XX");
        form.GetValidationMessages(Field(order, nameof(BlazorOrder.Account))).Should().Equal("The value must be at least 15 characters long.");
        Code(form, order, nameof(BlazorOrder.Account)).Should().Be(ValueObjectErrorCodes.TooShort);
        // EditContext.Validate() is obsolete on .NET 11, for ValidateAsync().
        (await renderer.Dispatcher.InvokeAsync(() => form.ValidateAsync(TestContext.Current.CancellationToken))).Should().BeFalse();

        await renderer.Dispatcher.InvokeAsync(() => account.Type(string.Empty));
        form.GetValidationMessages(Field(order, nameof(BlazorOrder.Account))).Should().Equal("The Account field is required.");
        Code(form, order, nameof(BlazorOrder.Account)).Should().Be(ValueObjectErrorCodes.Required);

        await renderer.Dispatcher.InvokeAsync(() => optional.Type("XX"));
        await renderer.Dispatcher.InvokeAsync(() => optional.Type(string.Empty));
        order.Optional.Should().BeNull();
        Code(form, order, nameof(BlazorOrder.Optional)).Should().BeNull();

        await renderer.Dispatcher.InvokeAsync(() => quantity.Type("500"));
        order.Quantity.Should().Be(Quantity.Create(3));
        Code(form, order, nameof(BlazorOrder.Quantity)).Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task A_refusal_is_answered_from_the_application_s_resource_in_the_current_UI_culture()
    {
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
        try
        {
            await using var provider = new ServiceCollection()
                .AddLogging()
                .AddValueObjectInputs(static options => options.UseResource<Messages>())
                .BuildServiceProvider();
            await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
            var order = new BlazorOrder();
            var form = new EditContext(order);
            var (account, _, quantity) = await RenderAsync(renderer, order, form);

            await renderer.Dispatcher.InvokeAsync(() => account.Type("XX"));
            await renderer.Dispatcher.InvokeAsync(() => quantity.Type("500"));

            form.GetValidationMessages(Field(order, nameof(BlazorOrder.Account))).Should().Equal("« Account » : au moins 15 caractères.");
            form.GetValidationMessages(Field(order, nameof(BlazorOrder.Quantity))).Should().Equal("« Quantity » doit être compris entre 1 et 100.");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    private static async Task<(TypedInput<Iban> Account, TypedInput<Iban?> Optional, TypedInput<Quantity> Quantity)> RenderAsync(
        HtmlRenderer renderer,
        BlazorOrder order,
        EditContext form)
    {
        TypedInput<Iban>? account = null;
        TypedInput<Iban?>? optional = null;
        TypedInput<Quantity>? quantity = null;
        RenderFragment<EditContext> fields = _ => builder =>
        {
            // A region each, so that every input's sequence numbers start from the same literals.
            builder.OpenRegion(0);
            Input(builder, () => order.Account, value => order.Account = value, input => account = input);
            builder.CloseRegion();
            builder.OpenRegion(1);
            Input(builder, () => order.Optional, value => order.Optional = value, input => optional = input);
            builder.CloseRegion();
            builder.OpenRegion(2);
            Input(builder, () => order.Quantity, value => order.Quantity = value, input => quantity = input);
            builder.CloseRegion();
        };

        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<EditForm>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(EditForm.EditContext)] = form,
            [nameof(EditForm.ChildContent)] = fields,
        })));

        return (account!, optional!, quantity!);
    }

    private static void Input<TValue>(RenderTreeBuilder builder, Expression<Func<TValue>> field, Action<TValue> set, Action<TypedInput<TValue>> ready)
    {
        var read = field.Compile();
        builder.OpenComponent<TypedInput<TValue>>(0);
        builder.AddComponentParameter(1, nameof(TypedInput<TValue>.Value), read());
        builder.AddComponentParameter(2, nameof(TypedInput<TValue>.ValueChanged), EventCallback.Factory.Create<TValue>(field, set));
        builder.AddComponentParameter(3, nameof(TypedInput<TValue>.ValueExpression), field);
        builder.AddComponentParameter(4, nameof(TypedInput<TValue>.Ready), ready);
        builder.CloseComponent();
    }

    private static FieldIdentifier Field(BlazorOrder order, string name) => new(order, name);

    private static string? Code(EditContext form, BlazorOrder order, string name)
        => form.TryGetValueObjectErrorCode(Field(order, name), out var code) ? code : null;
}
