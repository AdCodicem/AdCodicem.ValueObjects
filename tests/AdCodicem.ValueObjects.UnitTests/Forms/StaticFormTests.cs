using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// The pages of the Razor fixture served over TestHost, in French: a static form, which the framework maps from the post
/// through <see cref="IParsable{TSelf}"/> in the invariant culture before any component runs, so that the input writes the
/// invariant text, and a prerendered interactive page, which writes the current culture's. The framework's own messages are
/// pinned word for word, as the Blazor guide quotes them, so that a framework that changes them shows here.
/// </summary>
public sealed partial class StaticFormTests
{
    [Fact]
    public async Task A_static_form_writes_the_invariant_text_which_its_untouched_post_maps_back()
    {
        await using var application = await StartAsync();
        using var client = application.GetTestClient();

        var page = await client.GetAsync("/forms/order", TestContext.Current.CancellationToken);
        var form = await Form.ReadAsync(page);

        form.Fields.Should().Contain("Model.Amount", "12.5")
            .And.Contain("Model.Count", "0")
            .And.Contain("Model.Due", "2026-10-14")
            .And.Contain("Model.Pickup", "10:30:15.2500000")
            .And.Contain("Model.Placed", "2026-10-14T10:30:15.2500000Z")
            .And.Contain("Model.Confirmed", "2026-10-14T10:30:15.2500000+02:00")
            .And.Contain("Model.Hold", "1.01:01:01.0010000");

        var posted = await form.PostAsync(client, "/forms/order", "order");

        Messages(posted).Should().BeEmpty();
        Result(posted).Should().Be(
            "accepted=True account=FR7630006000011234567890189 quantity=3 amount=12.5 count=0 due=2026-10-14 "
            + "pickup=10:30:15.2500000 placed=2026-10-14T10:30:15.2500000Z confirmed=2026-10-14T10:30:15.2500000+02:00 "
            + "hold=1.01:01:01.0010000");
    }

    [Fact]
    public async Task A_static_form_is_refused_by_the_framework_s_mapping_with_its_own_message_quoting_the_text()
    {
        await using var application = await StartAsync();
        using var client = application.GetTestClient();
        var form = await Form.ReadAsync(await client.GetAsync("/forms/order", TestContext.Current.CancellationToken));

        var posted = await form.PostAsync(client, "/forms/order", "order", ("Model.Account", "XX"), ("Model.Quantity", "500"), ("Model.Amount", "12,5"));

        Messages(posted).Should().Equal(
            "The value 'XX' is not valid for 'Account'.",
            "The value '500' is not valid for 'Quantity'.",
            "The value '12,5' is not valid for 'Amount'.");
        Input(posted, "account").Should().Contain("aria-invalid=\"true\"").And.Contain("value=\"XX\"");

        // The framework leaves each member it refused at its default, whatever the model held, and the submit is refused.
        Result(posted).Should().StartWith("accepted=False account= quantity=0 amount=0 count=0 due=2026-10-14 ");
    }

    [Fact]
    public async Task A_static_form_refuses_an_optional_value_object_left_empty_and_holds_text_to_its_rules_through_ParsesAs()
    {
        await using var application = await StartAsync();
        using var client = application.GetTestClient();
        var form = await Form.ReadAsync(await client.GetAsync("/forms/optional", TestContext.Current.CancellationToken));

        var refused = await form.PostAsync(client, "/forms/optional", "optional", ("Model.Optional", string.Empty), ("Model.Text", "XX"));
        var accepted = await form.PostAsync(client, "/forms/optional", "optional", ("Model.Optional", "fr76 3000 6000 0112 3456 7890 189"), ("Model.Text", "FR7630006000011234567890189"));

        Messages(refused).Should().Equal("The value '' is not valid for 'Optional'.", "The value must be at least 15 characters long.");
        Messages(accepted).Should().BeEmpty();
        Result(accepted).Should().Be("accepted=True optional=FR7630006000011234567890189");
    }

    [Fact]
    public async Task An_interactive_page_prerenders_its_inputs_in_the_current_culture()
    {
        await using var application = await StartAsync();
        using var client = application.GetTestClient();

        var form = await Form.ReadAsync(await client.GetAsync("/forms/interactive", TestContext.Current.CancellationToken));

        form.Fields.Should().Contain("Model.Amount", "12,5").And.Contain("Model.Due", "2026-10-14");
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        var application = builder.Build();
        application.Use(static (context, next) =>
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            return next(context);
        });
        application.UseAntiforgery();
        application.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }

    private static IReadOnlyList<string> Messages(string page)
        => [.. ValidationMessage().Matches(page).Select(static match => WebUtility.HtmlDecode(match.Groups[1].Value))];

    private static string Result(string page) => WebUtility.HtmlDecode(ResultParagraph().Match(page).Groups[1].Value);

    private static string Input(string page, string id) => Regex.Match(page, $"<input id=\"{id}\"[^>]*>").Value;

    [GeneratedRegex("<div class=\"validation-message\">([^<]*)</div>")]
    private static partial Regex ValidationMessage();

    [GeneratedRegex("<p id=\"result\">([^<]*)</p>")]
    private static partial Regex ResultParagraph();

    [GeneratedRegex("<input id=\"[^\"]*\" name=\"([^\"]+)\"(?:[^>]*? value=\"([^\"]*)\")?[^>]*>")]
    private static partial Regex NamedInput();

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex Token();

    /// <summary>A form as a browser read it from a page: its fields, its antiforgery token and the cookie it came with.</summary>
    private sealed class Form
    {
        private string _token = string.Empty;
        private string _cookie = string.Empty;

        public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);

        public static async Task<Form> ReadAsync(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var form = new Form
            {
                _token = Token().Match(page).Groups[1].Value,
                _cookie = string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(static cookie => cookie.Split(';')[0]) : []),
            };

            foreach (Match input in NamedInput().Matches(page))
            {
                form.Fields[input.Groups[1].Value] = WebUtility.HtmlDecode(input.Groups[2].Value);
            }

            return form;
        }

        /// <summary>Posts the form's fields, as the browser would, changed by the pairs given.</summary>
        public async Task<string> PostAsync(HttpClient client, string path, string handler, params (string Name, string Value)[] changes)
        {
            var fields = new Dictionary<string, string>(Fields, StringComparer.Ordinal) { ["_handler"] = handler, ["__RequestVerificationToken"] = _token };
            foreach (var (name, value) in changes)
            {
                fields[name] = value;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", _cookie);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }
    }
}
