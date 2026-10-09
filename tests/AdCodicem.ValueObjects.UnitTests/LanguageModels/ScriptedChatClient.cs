using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// A model that calls one tool with the arguments it is given, then answers once it reads the tool's result, which it
/// keeps: what a real model would read, with no network.
/// </summary>
/// <param name="tool">The name of the tool to call.</param>
/// <param name="arguments">The arguments to call it with, as a JSON object.</param>
internal sealed class ScriptedChatClient(string tool, string arguments) : IChatClient
{
    /// <summary>Gets the result of the tool, as <c>FunctionInvokingChatClient</c> handed it to the model.</summary>
    public FunctionResultContent? Result { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (messages.SelectMany(static message => message.Contents).OfType<FunctionResultContent>().LastOrDefault() is { } result)
        {
            Result = result;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        var call = new FunctionCallContent("call-1", tool, LanguageModelTools.Arguments(arguments));
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
