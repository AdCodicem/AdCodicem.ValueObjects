namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

/// <summary>
/// Every member the generator emits, run on every value object of the domain — one per underlying type and per hook
/// (docs/adr/0006-coverage-is-a-signal-not-a-goal.md). The other test classes state the behaviour that matters to a
/// consumer on one representative type; these make sure no emitted variant is only ever compiled.
/// </summary>
public class GeneratedSurfaceTests
{
    public static TheoryData<string> Every => Samples.Names;

    public static TheoryData<string> EveryArithmetic => Samples.NumericNames;

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_value_object_converts_through_its_TypeConverter(string type)
        => Samples.All[type].ConvertsThroughItsTypeConverter();

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_value_object_round_trips_as_a_JSON_dictionary_key(string type)
        => Samples.All[type].RoundTripsAsADictionaryKey();

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_parsing_overload_agrees_with_the_others(string type)
        => Samples.All[type].ParsesThroughEveryOverload();

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_comparison_operator_agrees_with_CompareTo(string type)
        => Samples.All[type].ComparesThroughEveryOperator();

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_accessor_hands_back_the_carried_value(string type)
        => Samples.All[type].ExposesItsValueThroughEveryAccessor();

    [Theory]
    [MemberData(nameof(Every))]
    public void Formatting_without_a_provider_uses_the_invariant_culture(string type)
        => Samples.All[type].FormatsWithoutAProvider();

    [Theory]
    [MemberData(nameof(Every))]
    public void JSON_of_the_wrong_shape_is_refused(string type)
        => Samples.All[type].RefusesJsonOfTheWrongShape();

    [Theory]
    [MemberData(nameof(EveryArithmetic))]
    public void Every_arithmetic_member_validates_its_result(string type)
        => Samples.All[type].ComputesThroughEveryArithmeticMember();
}
