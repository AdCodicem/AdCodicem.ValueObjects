using System.Globalization;
using System.Linq.Expressions;
using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// Renders the forms of the Razor fixture through bUnit, as an application's test of a form does, and reads what a person
/// sees of a field: its text, its state, the messages under it, and the code its form keeps.
/// </summary>
internal static class FormsHarness
{
    /// <summary>Gets what an interactive component reports, as on a Blazor Server circuit.</summary>
    public static RendererInfo Interactive { get; } = new("Server", true);

    /// <summary>Gets what a statically rendered component reports, and a prerendered one.</summary>
    public static RendererInfo Static { get; } = new("Static", false);

    /// <summary>
    /// Creates a bUnit context whose components report <paramref name="renderer"/>: one reading
    /// <c>RendererInfo</c> otherwise fails with bUnit's <c>MissingRendererInfoException</c>.
    /// </summary>
    /// <param name="renderer">What the components report, <see cref="Interactive"/> by default.</param>
    /// <param name="services">Adds the application's services.</param>
    /// <returns>The context.</returns>
    public static BunitContext Context(RendererInfo? renderer = null, Action<IServiceCollection>? services = null)
    {
        var context = new BunitContext();

        // Before the renderer is asked for, which builds the service provider.
        services?.Invoke(context.Services);
        context.SetRendererInfo(renderer ?? Interactive);
        return context;
    }

    /// <summary>Sets the current cultures until the scope is disposed, which gives the earlier ones back.</summary>
    /// <param name="culture">The culture numbers and dates are read and written in.</param>
    /// <param name="uiCulture">The culture a resource is chosen in, <paramref name="culture"/> by default.</param>
    /// <returns>The scope.</returns>
    public static IDisposable Culture(string culture, string? uiCulture = null) => new CultureScope(culture, uiCulture ?? culture);

    /// <summary>Renders a form of one field over a model.</summary>
    /// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
    /// <param name="context">The bUnit context.</param>
    /// <param name="model">The model.</param>
    /// <param name="configure">Sets the other parameters of the form.</param>
    /// <returns>The field.</returns>
    public static FormField<TValue> Form<TValue>(
        this BunitContext context,
        Holder<TValue> model,
        Action<ComponentParameterCollectionBuilder<ValueObjectForm<TValue>>>? configure = null)
        => new(context.Render<ValueObjectForm<TValue>>(parameters =>
        {
            parameters.Add(static form => form.Model, model);
            configure?.Invoke(parameters);
        }), model);

    /// <summary>Renders a form whose input sets its text from code, as a subclass of the component may.</summary>
    /// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
    /// <param name="context">The bUnit context.</param>
    /// <param name="model">The model.</param>
    /// <returns>The form's edit context, and the input.</returns>
    public static (EditContext Form, TypedInput<TValue> Input, IRenderedComponent<EditForm> Rendered) Typed<TValue>(this BunitContext context, Holder<TValue> model)
    {
        var form = new EditContext(model);
        var rendered = context.Render<EditForm>(parameters => parameters
            .Add(static editForm => editForm.EditContext, form)
            .Add(static editForm => editForm.ChildContent, _ => builder => TypedInput<TValue>.Render(builder, model)));

        return (form, rendered.FindComponent<TypedInput<TValue>>().Instance, rendered);
    }
}

/// <summary>The cultures of a test, and the ones it gives back.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public CultureScope(string culture, string uiCulture)
    {
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        CultureInfo.CurrentUICulture = new CultureInfo(uiCulture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}

/// <summary>The field of a form the harness rendered.</summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
internal sealed class FormField<TValue>(IRenderedComponent<ValueObjectForm<TValue>> form, Holder<TValue> model)
{
    /// <summary>Gets the rendered form.</summary>
    public IRenderedComponent<ValueObjectForm<TValue>> Rendered => form;

    /// <summary>Gets the field, as the form's edit context identifies it.</summary>
    public FieldIdentifier Identifier => new(model, nameof(Holder<TValue>.Value));

    /// <summary>Gets the model's value.</summary>
    public TValue? Model => model.Value;

    /// <summary>Gets the text the input shows.</summary>
    public string? Text => form.Find("input").GetAttribute("value");

    /// <summary>Gets the classes of the input.</summary>
    public string? Classes => form.Find("input").GetAttribute("class");

    /// <summary>Gets a value indicating whether the input is marked invalid for assistive technologies.</summary>
    public bool Invalid => form.Find("input").GetAttribute("aria-invalid") == "true";

    /// <summary>Gets the messages the form holds for the field.</summary>
    public IReadOnlyList<string> Messages => [.. form.Instance.Context.GetValidationMessages(Identifier)];

    /// <summary>Gets the messages the <c>ValidationMessage</c> under the field shows.</summary>
    public IReadOnlyList<string> Shown => [.. form.FindAll("div.validation-message").Select(static element => element.TextContent)];

    /// <summary>Gets the messages the <c>ValidationSummary</c> of the form shows.</summary>
    public IReadOnlyList<string> Summary => [.. form.FindAll("li.validation-message").Select(static element => element.TextContent)];

    /// <summary>Gets the code the form keeps for the field, or <see langword="null"/>.</summary>
    public string? Code => form.Instance.Context.TryGetValueObjectErrorCode(Identifier, out var code) ? code : null;

    /// <summary>Gets the attributes of the input.</summary>
    public IReadOnlyDictionary<string, string> Attributes
        => form.Find("input").Attributes
            .Where(static attribute => !attribute.Name.StartsWith("blazor:", StringComparison.Ordinal))
            .ToDictionary(static attribute => attribute.Name, static attribute => attribute.Value, StringComparer.Ordinal);

    /// <summary>Types text into the input, as a person does before leaving the field.</summary>
    /// <param name="text">The text.</param>
    public void Type(string text) => form.Find("input").Change(text);

    /// <summary>Submits the form.</summary>
    public void Submit() => form.Find("form").Submit();
}

/// <summary>An input whose text is set from code, through the protected <c>CurrentValueAsString</c>, as a subclass may.</summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
internal sealed class TypedInput<TValue> : InputValueObject<TValue>
{
    /// <summary>Gets or sets what is handed the input once it is initialized, where no renderer exposes it.</summary>
    [Parameter]
    public Action<TypedInput<TValue>>? Ready { get; set; }

    /// <summary>Sets the text of the input, as the component's change event does.</summary>
    /// <param name="text">The text.</param>
    public void Type(string? text) => CurrentValueAsString = text;

    /// <summary>Renders the input bound to a model.</summary>
    /// <param name="builder">The render tree builder.</param>
    /// <param name="model">The model.</param>
    /// <param name="ready">What is handed the input once it is initialized.</param>
    public static void Render(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, Holder<TValue> model, Action<TypedInput<TValue>>? ready = null)
    {
        builder.OpenComponent<TypedInput<TValue>>(0);
        builder.AddComponentParameter(1, nameof(Value), model.Value);
        builder.AddComponentParameter(2, nameof(ValueChanged), EventCallback.Factory.Create<TValue>(model, value => model.Value = value));
        builder.AddComponentParameter(3, nameof(ValueExpression), (Expression<Func<TValue>>)(() => model.Value!));
        builder.AddComponentParameter(4, nameof(Ready), ready);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override void OnInitialized() => Ready?.Invoke(this);
}
