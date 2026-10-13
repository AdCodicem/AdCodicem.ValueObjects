using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.UnitTests.DataAnnotations;

/// <summary>
/// The attributes in the hosts DataAnnotations runs in outside a web request: options validated as the host starts,
/// through the reflection-based validation and through the one <c>[OptionsValidator]</c> generates, a view model
/// validating its properties as they are set, and <see cref="Validator"/>'s throwing methods.
/// </summary>
public partial class DataAnnotationsHostTests
{
    private const string Account = "FR7630006000011234567890189";

    /// <summary>
    /// Options keep the text of a value object, which the configuration binding generator could not create
    /// (<c>VO0032</c>), and hold it to the value object's rules: the host refuses to start, reporting each member with the
    /// message of its rule, a value object left at its default included.
    /// </summary>
    [Fact]
    public async Task Options_holding_text_are_held_to_the_rules_as_the_host_starts()
    {
        using var host = Host(new Dictionary<string, string?> { ["Settlement:Account"] = "XX", ["Settlement:MaxItems"] = "5000" });

        var start = () => host.StartAsync(TestContext.Current.CancellationToken);

        var refusal = (await start.Should().ThrowAsync<OptionsValidationException>()).Which;
        refusal.Failures.Should().Equal(
            "DataAnnotation validation failed for 'SettlementOptions' members: 'Account' with the error: 'The value must be at least 15 characters long.'.",
            "DataAnnotation validation failed for 'SettlementOptions' members: 'MaxItems' with the error: 'The value must be less than or equal to 1000.'.",
            "DataAnnotation validation failed for 'SettlementOptions' members: 'Fallback' with the error: 'The Fallback field is required.'.");
    }

    [Fact]
    public async Task Options_holding_text_the_rules_accept_let_the_host_start()
    {
        using var host = Host(new Dictionary<string, string?>
        {
            ["Settlement:Account"] = "fr76 3000 6000 0112 3456 7890 189",
            ["Settlement:MaxItems"] = "10",
            ["Settlement:Fallback"] = Account,
        });

        await host.StartAsync(TestContext.Current.CancellationToken);

        host.Services.GetRequiredService<IOptions<SettlementOptions>>().Value.Account.Should().Be("fr76 3000 6000 0112 3456 7890 189");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The validator <c>[OptionsValidator]</c> generates creates the attributes in its own code, the generic form
    /// included, and reports each member as it names it.
    /// </summary>
    [Fact]
    public void The_validator_OptionsValidator_generates_holds_the_text_to_the_rules()
    {
        var result = new SettlementOptionsValidator().Validate(null, new SettlementOptions { Account = "XX", MaxItems = "5000" });

        result.Failed.Should().BeTrue();
        result.Failures.Should().Equal(
            "Account: The value must be at least 15 characters long.",
            "MaxItems: The value must be less than or equal to 1000.",
            "Fallback: The SettlementOptions.Fallback field is required.");
        new SettlementOptionsValidator().Validate(null, new SettlementOptions { Account = Account, Fallback = Iban.Create(Account) })
            .Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// A view model validating its properties as they are set keeps the results the attributes report, codes included,
    /// both forms, as the toolkit copies them onto the property it generates.
    /// </summary>
    [Fact]
    public void A_view_model_keeps_the_code_of_each_refusal()
    {
        var model = new TransferViewModel();
        var changed = new List<string?>();
        model.ErrorsChanged += (_, e) => changed.Add(e.PropertyName);

        model.AccountText = "XX";
        model.QuantityText = "5000";
        model.ValidateEveryProperty();

        changed.Should().Contain([nameof(TransferViewModel.AccountText), nameof(TransferViewModel.QuantityText), nameof(TransferViewModel.Account)]);
        Codes(model, nameof(TransferViewModel.AccountText)).Should().Equal(ValueObjectErrorCodes.TooShort);
        Codes(model, nameof(TransferViewModel.QuantityText)).Should().Equal(ValueObjectErrorCodes.OutOfRange);
        Codes(model, nameof(TransferViewModel.Account)).Should().Equal(ValueObjectErrorCodes.Required);
        model.GetErrors(nameof(TransferViewModel.Account)).Single().ErrorMessage.Should().Be("The Account field is required.");

        model.AccountText = Account;
        model.HasErrors.Should().BeTrue();
        model.GetErrors(nameof(TransferViewModel.AccountText)).Should().BeEmpty();

        static IEnumerable<string> Codes(TransferViewModel model, string property)
            => model.GetErrors(property).Cast<ValueObjectValidationResult>().Select(static result => result.ErrorCode);
    }

    /// <summary>
    /// <see cref="Validator"/>'s throwing methods wrap the first refusal in a <see cref="ValidationException"/>, which
    /// <see cref="ValueObjectErrors.TryGetCode"/> reads the code of, around another exception too.
    /// </summary>
    [Fact]
    public void Validators_throwing_methods_carry_the_code_TryGetCode_reads()
    {
        var form = new TransferForm { Account = "FR76" };

        var refusal = FluentActions.Invoking(() => Validator.ValidateObject(form, new ValidationContext(form), validateAllProperties: true))
            .Should().Throw<ValidationException>().Which;
        var property = FluentActions.Invoking(() => Validator.ValidateProperty(
                "FR76",
                new ValidationContext(form) { MemberName = nameof(TransferForm.Account) }))
            .Should().Throw<ValidationException>().Which;
        var value = FluentActions.Invoking(() => Validator.ValidateValue(
                "FR76",
                Annotated.Context(),
                [new ParsesAsAttribute<Iban, string>()]))
            .Should().Throw<ValidationException>().Which;

        foreach (var exception in (Exception[])[refusal, property, value, new InvalidOperationException("outer", refusal)])
        {
            exception.ShouldCarry(ValueObjectErrorCodes.TooShort);
        }

        refusal.ValidationResult.Should().BeOfType<ValueObjectValidationResult>().Which.ValueObjectType.Should().Be<Iban>();
        refusal.Message.Should().Be("The value must be at least 15 characters long.");
    }

    [Fact]
    public void A_ValidationException_around_another_attributes_result_carries_no_code()
    {
        var refusal = new ValidationException(new AnnotationResult("The field is required."), new RequiredAttribute(), null);

        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeFalse();
        code.Should().BeNull();
        ValueObjectErrors.TryGetCode(new ValidationException("plain"), out _).Should().BeFalse();
    }

    private static IHost Host(Dictionary<string, string?> settings)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddOptions<SettlementOptions>().BindConfiguration("Settlement").ValidateDataAnnotations().ValidateOnStart();

        return builder.Build();
    }

    /// <summary>The validator <c>[OptionsValidator]</c> generates for <see cref="SettlementOptions"/>.</summary>
    [OptionsValidator]
    public partial class SettlementOptionsValidator : IValidateOptions<SettlementOptions>;

    /// <summary>
    /// A view model whose properties the toolkit generates, copying the attributes onto them.
    /// </summary>
    public partial class TransferViewModel : ObservableValidator
    {
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [ParsesAs(typeof(Iban))]
        public partial string? AccountText { get; set; }

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [ParsesAs<Quantity, short>]
        public partial string? QuantityText { get; set; }

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required]
        [NotDefault]
        public partial Iban Account { get; set; }

        /// <summary>Validates every property, as a view model does before it submits.</summary>
        public void ValidateEveryProperty() => ValidateAllProperties();
    }
}

/// <summary>
/// The settings of a settlement, as the configuration holds them: the text of value objects, which the options keep and
/// hold to the value objects' rules, and a value object the configuration binder creates through its type converter.
/// </summary>
public sealed class SettlementOptions
{
    [Required]
    [ParsesAs(typeof(Iban))]
    public string? Account { get; set; }

    [ParsesAs<Quantity, short>]
    public string? MaxItems { get; set; }

    [NotDefault]
    public Iban Fallback { get; set; }
}
