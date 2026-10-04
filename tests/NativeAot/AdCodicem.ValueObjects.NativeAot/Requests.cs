using System.Text;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>The requests the script sends to its own endpoints, each written with the status and the body answered.</summary>
internal static class Requests
{
    public static async Task RunAsync(Report report, HttpClient client)
    {
        string[] gets =
        [
            "/quantities/7", "/quantities/1000", "/quantities/0", "/quantities/abc",
            "/customers?id=0f8fad5b-d9cb-469f-a165-70867728950e", "/customers?id=00000000-0000-0000-0000-000000000000", "/customers?id=nope", "/customers",
            "/pages", "/pages?number=", "/pages?number=3", "/pages?number=0",
            "/rates/5.5", "/rates/20.0", "/rates/7",
            "/documents/po-1042", "/documents/PO-1042-0001-X",
            "/emails/%20Ada@Example.com", "/emails/ada",
        ];
        foreach (var path in gets)
        {
            await SendAsync(report, client, new HttpRequestMessage(HttpMethod.Get, path), path);
        }

        var id = OrderId.New().Value;
        foreach (var header in (string?[])[id, id[..^1] + (id[^1] == '0' ? "1" : "0"), "acc_0", null])
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/orders/current");
            if (header is not null)
            {
                request.Headers.Add("X-Order-Id", header);
            }

            await SendAsync(report, client, request, $"/orders/current X-Order-Id: {header ?? "(none)"}");
        }

        const string Order = """
            {"Email":" Ada@Example.com","Quantity":3,"Amount":12.345,"Rate":5.5,"Status":"FINAL","Customer":"0f8fad5b-d9cb-469f-a165-70867728950e","Number":"po-1042","Id":null,"Opens":"09:30:00"}
            """;
        foreach (var body in (string[])[
            Order,
            Order.Replace("\"Id\":null", $"\"Id\":\"{id}\"", StringComparison.Ordinal),
            Order.Replace(" Ada@Example.com", "ada", StringComparison.Ordinal),
            Order.Replace("\"Quantity\":3", "\"Quantity\":0", StringComparison.Ordinal),
            Order.Replace("\"Rate\":5.5", "\"Rate\":7", StringComparison.Ordinal),
            Order.Replace("\"Quantity\":3", "\"Quantity\":\"3\"", StringComparison.Ordinal)])
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            await SendAsync(report, client, request, $"/orders {body}");
        }
    }

    private static async Task SendAsync(Report report, HttpClient client, HttpRequestMessage request, string subject)
    {
        using (request)
        {
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            report.Line($"{request.Method} {subject}", $"{(int)response.StatusCode} {body}");
        }
    }
}
