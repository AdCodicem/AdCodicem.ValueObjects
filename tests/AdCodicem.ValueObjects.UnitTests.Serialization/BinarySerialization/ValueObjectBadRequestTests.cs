using AdCodicem.ValueObjects.Grpc;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Google.Rpc;
using Grpc.Core;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectBadRequest"/> called directly, as a contract-first handler calls it while it maps a request.
/// </summary>
public sealed class ValueObjectBadRequestTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    [Fact]
    public void Accepted_values_are_created_and_nothing_is_thrown()
    {
        var fields = new ValueObjectBadRequest();

        fields.Create<Iban, string>("fr76 3000 6000 0112 3456 7890 189", "account").Should().Be(Iban.Create(ValidIban));
        fields.CreateAll<PageNumber, int>([1, 2], "pages").Should().Equal(PageNumber.Create(1), PageNumber.Create(2));
        fields.Violations.Should().BeEmpty();
        fields.ThrowIfAny();
    }

    /// <summary>
    /// Each refused value is recorded under its field's path, an element of a repeated field under its index, with its
    /// reason and its rule's message, and leaves the default instance where it was asked for; they are thrown together.
    /// </summary>
    [Fact]
    public void Every_refused_value_is_recorded_and_thrown_at_once()
    {
        var fields = new ValueObjectBadRequest();

#pragma warning disable VO0010 // The default instance is what a refused value leaves.
        fields.Create<Iban, string>("FR76", "order.account").Should().Be(default(Iban));
#pragma warning restore VO0010
        fields.CreateAll<PageNumber, int>([1, 0, 2], "order.pages").Select(page => page.Value).Should().Equal(1, 0, 2);
        fields.Create<CountryCode, string>("ZZ", "country");

        fields.Violations.Select(violation => (violation.Field, violation.Reason, violation.Description)).Should().Equal(
            ("order.account", "VALUE_OBJECT_TOO_SHORT", "The value must be at least 15 characters long."),
            ("order.pages[1]", "VALUE_OBJECT_OUT_OF_RANGE", "The value must be greater than or equal to 1."),
            ("country", "VALUE_OBJECT_NOT_A_KNOWN_VALUE", "The value is not one of the accepted values."));

        var thrown = FluentActions.Invoking(() => fields.ThrowIfAny()).Should().Throw<RpcException>().Which;
        thrown.StatusCode.Should().Be(StatusCode.InvalidArgument);
        thrown.Status.Detail.Should().Be("The request holds values its value objects refuse.");
        thrown.GetRpcStatus()!.GetDetail<BadRequest>()!.FieldViolations.Should().HaveCount(3);
        thrown.ShouldCarry(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public void The_status_message_can_be_the_service_s_own()
    {
        var fields = new ValueObjectBadRequest();
        fields.Create<PageNumber, int>(0, "page");

        FluentActions.Invoking(() => fields.ThrowIfAny("The order holds invalid lines.")).Should().Throw<RpcException>()
            .Which.GetRpcStatus()!.Message.Should().Be("The order holds invalid lines.");
    }

    /// <summary>
    /// A code that maps to no gRPC reason leaves the violation's reason empty, and a value object written by hand that
    /// refuses without a code is recorded as not parsable, with an empty description.
    /// </summary>
    [Fact]
    public void A_code_that_maps_to_no_reason_and_a_refusal_without_a_code_are_recorded_all_the_same()
    {
        var fields = new ValueObjectBadRequest();

        fields.Create<ProtoOddCode, string>("odd", "odd");
        fields.Create<HandWrittenRating, int>(9, "rating");

        fields.Violations.Select(violation => (violation.Field, violation.Reason, violation.Description)).Should().Equal(
            ("odd", string.Empty, "The value is odd."),
            ("rating", "VALUE_OBJECT_NOT_PARSABLE", string.Empty));
        FluentActions.Invoking(() => fields.ThrowIfAny()).Should().Throw<RpcException>().Which.ShouldCarry(ProtoOddCode.Code);
    }

    [Fact]
    public void A_blank_field_or_a_null_list_is_refused()
    {
        var fields = new ValueObjectBadRequest();

        FluentActions.Invoking(() => fields.Create<Iban, string>(ValidIban, " ")).Should().Throw<ArgumentException>().WithParameterName("field");
        FluentActions.Invoking(() => fields.CreateAll<Iban, string>(null!, "accounts")).Should().Throw<ArgumentNullException>().WithParameterName("values");
        FluentActions.Invoking(() => fields.CreateAll<Iban, string>([ValidIban], string.Empty)).Should().Throw<ArgumentException>().WithParameterName("field");
    }
}
