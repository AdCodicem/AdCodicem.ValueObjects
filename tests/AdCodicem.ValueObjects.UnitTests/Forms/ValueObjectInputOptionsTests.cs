using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// <see cref="ValueObjectInputOptions"/> and <see cref="ValueObjectInputServiceCollectionExtensions.AddValueObjectInputs"/>:
/// the resource the messages of refusals are read from, and the localization services they are read through.
/// </summary>
public sealed class ValueObjectInputOptionsTests
{
    [Fact]
    public void A_resource_is_a_type()
    {
        var options = new ValueObjectInputOptions();

        options.ResourceType.Should().BeNull();
        options.UseResource<ValidationMessages>().Should().BeSameAs(options);
        options.ResourceType.Should().Be<ValidationMessages>();

        var none = () => options.UseResource(null!);
        none.Should().Throw<ArgumentNullException>().WithParameterName("resourceType");
    }

    [Fact]
    public void Registering_the_options_takes_services_and_a_configuration()
    {
        var noServices = () => ValueObjectInputServiceCollectionExtensions.AddValueObjectInputs(null!, static _ => { });
        var noConfiguration = () => new ServiceCollection().AddValueObjectInputs(null!);

        noServices.Should().Throw<ArgumentNullException>().WithParameterName("services");
        noConfiguration.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void Registering_the_options_registers_the_localization_services_keeping_the_applications()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddLocalization(static options => options.ResourcesPath = "Resources");

        services.AddValueObjectInputs(static options => options.UseResource<ValidationMessages>()).Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<ValueObjectInputOptions>>().Value.ResourceType.Should().Be<ValidationMessages>();
        provider.GetRequiredService<IStringLocalizerFactory>().Should().BeOfType<ResourceManagerStringLocalizerFactory>();
        provider.GetRequiredService<IOptions<LocalizationOptions>>().Value.ResourcesPath.Should().Be("Resources");
        services.Count(static service => service.ServiceType == typeof(IStringLocalizerFactory)).Should().Be(1);
    }

    [Fact]
    public async Task An_input_over_services_holding_no_options_keeps_the_English_message()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        var model = new Holder<Iban>();
        var form = new EditContext(model);
        TypedInput<Iban>? input = null;

        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<EditForm>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(EditForm.EditContext)] = form,
            [nameof(EditForm.ChildContent)] = (RenderFragment<EditContext>)(_ => builder => TypedInput<Iban>.Render(builder, model, typed => input = typed)),
        })));
        await renderer.Dispatcher.InvokeAsync(() => input!.Type("XX"));

        form.GetValidationMessages().Should().Equal("The value must be at least 15 characters long.");
    }

    [Fact]
    public void A_resource_named_without_the_localization_services_fails_the_refusal_naming_the_call()
    {
        using var context = FormsHarness.Context(services: static services => services.Configure<ValueObjectInputOptions>(static options => options.UseResource<ValidationMessages>()));
        var field = context.Form(new Holder<Iban>());

        var refuse = () => field.Type("XX");

        refuse.Should().Throw<InvalidOperationException>().WithMessage("*through an IStringLocalizerFactory, which this application's services do not hold. Call builder.Services.AddLocalization().");
    }
}
