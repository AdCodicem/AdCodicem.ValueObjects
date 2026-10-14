using System.Text.Encodings.Web;
using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// A Blazor form of every value object the registry describes, alone and as its nullable type, rendered through
/// <see cref="HtmlRenderer"/>, whose input is given each text the value object's underlying type is probed with: the text
/// the input then shows, whether it is marked invalid, the model, the messages under it and the code its form keeps. The
/// inputs close their text adapters through the descriptor's visitor, which the native binary must hold for every value
/// object, and a value object the registry does not describe is refused, naming the registration.
/// </summary>
internal static class Forms
{
    public static async Task RunAsync(Report report)
    {
        var registered = ValueObjectRegistry.GetRegistered().OrderBy(static d => Names.Of(d.ValueObjectType), StringComparer.Ordinal).ToList();
        await RenderAsync(report, "blazor", registered, localized: false);

        // The messages of a resource, read through the localizer the application's services hold, under the invariant
        // culture of InvariantGlobalization, which reads the neutral resource; a code it holds no key for keeps English.
        await RenderAsync(
            report,
            "blazor localized",
            [.. registered.Where(static d => d.ValueObjectType == typeof(Quantity) || d.ValueObjectType == typeof(EmailAddress))],
            localized: true);

        await UnregisteredAsync(report);
    }

    private static async Task RenderAsync(Report report, string subject, IReadOnlyList<ValueObjectDescriptor> descriptors, bool localized)
    {
        var services = new ServiceCollection().AddLogging();
        if (localized)
        {
            services.AddValueObjectInputs(static options => options.UseResource<ProblemMessages>());
        }

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var inputs = new List<ITypedInput>();
        var forms = descriptors.SelectMany(descriptor => descriptor.Accept(new FormsOf(inputs.Add))).ToList();

        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<BlazorForms>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BlazorForms.Forms)] = forms })));
        var markup = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);

        foreach (var input in inputs)
        {
            report.Line($"{subject} {input.Id}", $"renders {Element(markup, input.Id)}");
            foreach (var text in Underlying.Texts(input.UnderlyingType))
            {
                await renderer.Dispatcher.InvokeAsync(() => input.Type(text));
                var line = $"{subject} {input.Id} {Underlying.Quote(text)}";
                var outcome = input.Describe();

                // Two runs explaining nothing would agree: a field marked invalid keeps a code, a field that is not keeps
                // none, an optional field emptied binds null, and an address of no shape is refused for its format.
                if (input.Invalid != (input.Code is not null)
                    || (input.IsOptional && text.Length == 0 && (input.Model is not null || input.Code is not null))
                    || (input.Id == nameof(EmailAddress) && text == "not-an-email" && input.Code != ValueObjectErrorCodes.InvalidFormat))
                {
                    report.Fail(line, outcome);
                }
                else
                {
                    report.Line(line, outcome);
                }
            }
        }
    }

    private static async Task UnregisteredAsync(Report report)
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        RenderFragment form = static builder =>
        {
            builder.OpenComponent<BlazorForm<UnregisteredCode>>(0);
            builder.AddComponentParameter(1, nameof(BlazorForm<UnregisteredCode>.Id), nameof(UnregisteredCode));
            builder.AddComponentParameter(2, nameof(BlazorForm<UnregisteredCode>.Ready), static (ITypedInput _) => { });
            builder.CloseComponent();
        };

        try
        {
            await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<BlazorForms>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BlazorForms.Forms)] = new[] { form } })));
            report.Fail("blazor UnregisteredCode", "rendered");
        }
        catch (InvalidOperationException exception)
        {
            report.Line("blazor UnregisteredCode", $"threw {Report.Describe(exception)}: {exception.Message}");
        }
    }

    /// <summary>Gets the element of an input, by its id, in the document rendered.</summary>
    private static string Element(string markup, string id)
    {
        var start = markup.IndexOf($" id=\"{HtmlEncoder.Default.Encode(id)}\"", StringComparison.Ordinal);
        var open = markup.LastIndexOf("<input", start, StringComparison.Ordinal);
        return markup[open..(markup.IndexOf('>', start) + 1)];
    }

    /// <summary>
    /// Opens the form of a value object and the form of its nullable type, through the type arguments its descriptor hands
    /// back: a visitor that is not generic, as the input's own is not.
    /// </summary>
    private sealed class FormsOf(Action<ITypedInput> ready) : IValueObjectVisitor<RenderFragment[]>
    {
        public RenderFragment[] Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            =>
            [
                builder => Open<TSelf>(builder, Names.Of(typeof(TSelf))),
                builder => Open<TSelf?>(builder, $"{Names.Of(typeof(TSelf))}?"),
            ];

        private void Open<TForm>(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, string id)
        {
            builder.OpenComponent<BlazorForm<TForm>>(0);
            builder.AddComponentParameter(1, nameof(BlazorForm<TForm>.Id), id);
            builder.AddComponentParameter(2, nameof(BlazorForm<TForm>.Ready), ready);
            builder.CloseComponent();
        }
    }
}

/// <summary>The model of a form of one field.</summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
public sealed class FormModel<TValue>
{
    /// <summary>Gets or sets the field.</summary>
    public TValue? Value { get; set; }
}

/// <summary>What the script reads of an input, whatever its type.</summary>
public interface ITypedInput
{
    /// <summary>Gets the id of the input.</summary>
    string Id { get; }

    /// <summary>Gets the underlying type of the value object the input binds.</summary>
    Type UnderlyingType { get; }

    /// <summary>Gets a value indicating whether the input binds the nullable type.</summary>
    bool IsOptional { get; }

    /// <summary>Gets the model's value, boxed.</summary>
    object? Model { get; }

    /// <summary>Gets a value indicating whether the input is marked invalid.</summary>
    bool Invalid { get; }

    /// <summary>Gets the code its form keeps, or <see langword="null"/>.</summary>
    string? Code { get; }

    /// <summary>Types text into the input, as its change event does.</summary>
    /// <param name="text">The text.</param>
    void Type(string text);

    /// <summary>Describes the input: its text, its state, the model, the messages under it and the code.</summary>
    /// <returns>The description.</returns>
    string Describe();
}

/// <summary>An input whose text the script sets through the protected <c>CurrentValueAsString</c>, as a subclass may.</summary>
/// <typeparam name="TValue">The value object, or its nullable type.</typeparam>
public sealed class TypedInput<TValue> : InputValueObject<TValue>, ITypedInput
{
    /// <summary>Gets or sets what is handed the input once it is initialized.</summary>
    [Parameter]
    public Action<ITypedInput>? Ready { get; set; }

    /// <inheritdoc />
    public string Id => (string)AdditionalAttributes!["id"];

    /// <inheritdoc />
    public Type UnderlyingType => ValueObjectRegistry.GetUnderlyingType(Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue))!;

    /// <inheritdoc />
    public bool IsOptional => Nullable.GetUnderlyingType(typeof(TValue)) is not null;

    /// <inheritdoc />
    public object? Model => CurrentValue;

    /// <inheritdoc />
    public bool Invalid => AdditionalAttributes?.ContainsKey("aria-invalid") == true;

    /// <inheritdoc />
    public string? Code => EditContext.TryGetValueObjectErrorCode(FieldIdentifier, out var code) ? code : null;

    /// <inheritdoc />
    public void Type(string text) => CurrentValueAsString = text;

    /// <inheritdoc />
    public string Describe()
        => $"text {Underlying.Show(CurrentValueAsString)}, invalid {Invalid}, model {Underlying.Show(Model)}, "
           + $"messages [{string.Join(" | ", EditContext.GetValidationMessages(FieldIdentifier))}], code {Code ?? "none"}";

    /// <inheritdoc />
    protected override void OnInitialized() => Ready?.Invoke(this);
}
