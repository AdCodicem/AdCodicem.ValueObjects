using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Fixtures.GraphQL;
using AdCodicem.ValueObjects.HotChocolate;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// A <c>[QueryType]</c> HotChocolate's source generator binds at compile time, with value objects declared beside it in
/// the fixture's assembly: the generator emits nothing for HotChocolate, so its module and <c>AddValueObjects()</c> work
/// together as they do for a query type HotChocolate infers at run time.
/// </summary>
public sealed class SourceGeneratedQueryTests
{
    [Fact]
    public async Task A_source_generated_query_type_takes_value_objects_as_scalars()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddGraphQLTypes().AddValueObjects());

        Regex.Replace(executor.Schema.ToString(), @"\s+", " ").Should()
            .Contain($"\"{Sku.Schema.Description}\" scalar Sku")
            .And.Contain($"\"{ItemCount.Schema.Description}\" scalar ItemCount")
            .And.Contain("line(sku: Sku!, count: ItemCount!): Line!");

        (await executor.ExecuteAsync("""{ line(sku: "AB-1", count: 2) { sku count } }""")).Data().Should().Be("""{"line":{"sku":"AB-1","count":2}}""");
        (await executor.ExecuteAsync("""{ skus(skus: ["AB-1", "CD-2"]) find(sku: null) other: find(sku: "EF-3") }"""))
            .Data().Should().Be("""{"skus":["AB-1","CD-2"],"find":null,"other":"EF-3"}""");

        var sku = (await executor.ExecuteAsync("""{ line(sku: "nodash", count: 2) { sku } }""")).Error();
        sku.Code().Should().Be(ValueObjectErrorCodes.InvalidFormat);
        sku.Message().Should().Be("A SKU holds a dash.");

        var count = (await executor.ExecuteAsync("query($c: ItemCount!) { line(sku: \"AB-1\", count: $c) { count } }", """{"c":0}""")).Error();
        count.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        count.Member("path").Should().Be("""["c"]""");
    }
}
