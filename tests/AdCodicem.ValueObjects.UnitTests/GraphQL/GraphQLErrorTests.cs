using System.Text.Json;
using AdCodicem.ValueObjects.HotChocolate;
using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// The error a refused value becomes: HotChocolate's shape, with the rule's message and its code under
/// <c>extensions.code</c>, the code on the exception, and the value never quoted.
/// </summary>
public sealed class GraphQLErrorTests
{
    private const string Valid = "FR7630006000011234567890189";

    /// <summary>
    /// A refused argument is a field error: its path is the field, HotChocolate adds the argument's path, the coordinate
    /// and the type, and the code is the rule's, with no extension of the package's own.
    /// </summary>
    [Fact]
    public async Task A_refused_argument_is_a_field_error_carrying_the_rules_code()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());

        var result = await executor.ExecuteAsync("""{ account(iban: "XX", qty: 3) { iban } }""");

        var error = result.Error();
        error.Message().Should().Be("The value must be at least 15 characters long.");
        error.Member("path").Should().Be("""["account"]""");
        error["extensions"]!.ToJsonString().Should().Be(
            """{"code":"value_object.too_short","inputPath":["iban"],"coordinate":"ShopQuery.account(iban:)","fieldType":"Iban"}""");
        result["data"].Should().BeNull();
    }

    /// <summary>A refused variable is a request error: its path is the variable, and its extensions the code alone.</summary>
    [Fact]
    public async Task A_refused_variable_is_a_request_error_carrying_the_rules_code()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());

        var result = await executor.ExecuteAsync("query($i: Iban!) { lookup(iban: $i) }", """{"i":"XX"}""");

        var error = result.Error();
        error.Member("path").Should().Be("""["i"]""");
        error["extensions"]!.ToJsonString().Should().Be("""{"code":"value_object.too_short"}""");
        result.ContainsKey("data").Should().BeFalse();
    }

    /// <summary>
    /// The refusal is a <see cref="LeafCoercionException"/> carrying the code in its data, under
    /// <see cref="ValueObjectErrors.ErrorCodeKey"/>, and its GraphQL error the code and a value object exception with the
    /// type and the code, never the refused value.
    /// </summary>
    [Fact]
    public async Task The_refusal_carries_the_code_on_the_exception_and_never_the_value()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());

        var exception = GraphQLScalarTests.Scalar(executor, "Iban")
            .Invoking(scalar => scalar.CoerceInputLiteral(new StringValueNode("XX")))
            .Should().Throw<LeafCoercionException>().Which;

        exception.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.TooShort);
        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);

        var error = exception.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(ValueObjectErrorCodes.TooShort);
        error.Message.Should().Be("The value must be at least 15 characters long.");
        var refusal = error.Exception.Should().BeAssignableTo<ValueObjectException>().Subject;
        refusal.ValueObjectType.Should().Be<Iban>();
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
        refusal.AttemptedValue.Should().BeNull();
        refusal.Message.Should().NotContain("XX");
    }

    /// <summary>
    /// The refusal tells a literal of the document from the JSON value of a variable and from a value written, which the
    /// localizing filter reads to tell the path HotChocolate reads a variable's default value under from a variable's
    /// name.
    /// </summary>
    [Fact]
    public async Task The_refusal_tells_a_literal_from_a_JSON_value_and_from_a_value_written()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());
        var output = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<OutputQuery>().AddValueObjects());
        var iban = GraphQLScalarTests.Scalar(executor, "Iban");
        var quantity = GraphQLScalarTests.Scalar(executor, "Quantity");

        Literal(() => iban.CoerceInputLiteral(new StringValueNode("XX"))).Should().BeTrue();
        Literal(() => iban.CoerceInputLiteral(new IntValueNode(3))).Should().BeTrue();
        Literal(() => quantity.CoerceInputLiteral(new IntValueNode(5000))).Should().BeTrue();
        Literal(() => quantity.CoerceInputLiteral(new IntValueNode(40000))).Should().BeTrue();
        Literal(() => quantity.CoerceInputLiteral(new StringValueNode("3"))).Should().BeTrue();
        Literal(() => iban.CoerceInputValue(Json("\"XX\""), null!)).Should().BeFalse();
        Literal(() => iban.CoerceInputValue(Json("3"), null!)).Should().BeFalse();
        Literal(() => quantity.CoerceInputValue(Json("5000"), null!)).Should().BeFalse();
        Literal(() => quantity.CoerceInputValue(Json("40000"), null!)).Should().BeFalse();
        Literal(() => quantity.CoerceInputValue(Json("\"3\""), null!)).Should().BeFalse();
#pragma warning disable VO0010 // The uninitialized instance is what the scalar refuses to write.
        Literal(() => iban.ValueToLiteral(default(Iban))).Should().BeFalse();
#pragma warning restore VO0010
        Literal(() => GraphQLScalarTests.Scalar(output, "Tolerance").ValueToLiteral(Tolerance.Create(double.NegativeInfinity)))
            .Should().BeFalse();
    }

    /// <summary>
    /// An error filter of the application's own finds the rule's code on the error's exception, an output refusal's
    /// included, whose code HotChocolate replaces with its own.
    /// </summary>
    [Fact]
    public async Task An_applications_error_filter_finds_the_code_on_the_errors_exception()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<ShopQuery>()
            .AddValueObjects()
            .AddErrorFilter(static error => error.Exception is { } exception && ValueObjectErrors.TryGetCode(exception, out var code)
                ? error.SetExtension("rule", code)
                : error));

        (await executor.ExecuteAsync("""{ account(iban: "XX", qty: 3) { iban } }""")).Error().Member("rule")
            .Should().Be("\"value_object.too_short\"");
        (await executor.ExecuteAsync("query($q: Quantity!) { account(iban: \"" + Valid + "\", qty: $q) { iban } }", """{"q":"3"}"""))
            .Error().Member("rule").Should().Be("\"value_object.not_parsable\"");

        var output = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<OutputQuery>()
            .AddValueObjects()
            .AddErrorFilter(static error => error.Exception is { } exception && ValueObjectErrors.TryGetCode(exception, out var code)
                ? error.SetExtension("rule", code)
                : error));
        var unset = (await output.ExecuteAsync("{ unset }")).Error();
        unset.Code().Should().Be("EXEC_INVALID_LEAF_VALUE");
        unset.Member("rule").Should().Be("\"value_object.required\"");
    }

    /// <summary>
    /// Under <c>IncludeExceptionDetails</c>, HotChocolate adds the exception's message, which is the rule's and never
    /// quotes the value.
    /// </summary>
    [Fact]
    public async Task The_exception_details_quote_the_rule_never_the_value()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<ShopQuery>()
            .AddValueObjects()
            .ModifyRequestOptions(static options => options.IncludeExceptionDetails = true));

        var error = (await executor.ExecuteAsync("""{ account(iban: "ZZ9", qty: 3) { iban } }""")).Error();

        error["extensions"]!["exception"]!["message"]!.GetValue<string>().Should().Be("The value must be at least 15 characters long.");
        error.ToJsonString().Should().NotContain("ZZ9");
    }

    private static bool Literal(Action coercion)
        => coercion.Should().Throw<LeafCoercionException>().Which.Errors.Should().ContainSingle()
            .Which.Exception.Should().BeAssignableTo<ValueObjectScalarRefusal>().Subject.Literal;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;
}
