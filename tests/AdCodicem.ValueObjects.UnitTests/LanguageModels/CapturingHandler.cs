using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// Stands for the OpenAI endpoint: keeps the body of every request the OpenAI adapter sends, and answers with a chat
/// completion of its script, with no socket.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly Func<int, string> _reply;

    private CapturingHandler(Func<int, string> reply) => _reply = reply;

    /// <summary>Gets the body of each request sent, in order.</summary>
    public List<JsonNode> Requests { get; } = [];

    /// <summary>Answers the first request with a call of a tool, and the next with text.</summary>
    public static CapturingHandler CallingTool(string name, string arguments)
        => new(request => request == 0
            ? Completion(
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = null,
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["id"] = "call-1",
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = name, ["arguments"] = arguments },
                    }),
                },
                "tool_calls")
            : Completion(new JsonObject { ["role"] = "assistant", ["content"] = "done" }, "stop"));

    /// <summary>Answers every request with the same text.</summary>
    public static CapturingHandler Answering(string content)
        => new(_ => Completion(new JsonObject { ["role"] = "assistant", ["content"] = content }, "stop"));

    /// <summary>Builds the chat client of the OpenAI adapter, sending its requests here.</summary>
    public IChatClient CreateClient()
        => new OpenAIClient(
                new ApiKeyCredential("test"),
                new OpenAIClientOptions { Endpoint = new Uri("http://localhost/v1"), Transport = new HttpClientPipelineTransport(new HttpClient(this)) })
            .GetChatClient("test-model")
            .AsIChatClient();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = Requests.Count;
        Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_reply(index), Encoding.UTF8, "application/json"),
        };
    }

    private static string Completion(JsonObject message, string finishReason)
        => new JsonObject
        {
            ["id"] = "completion",
            ["object"] = "chat.completion",
            ["created"] = 0,
            ["model"] = "test-model",
            ["choices"] = new JsonArray(new JsonObject { ["index"] = 0, ["finish_reason"] = finishReason, ["message"] = message }),
        }.ToJsonString();
}
