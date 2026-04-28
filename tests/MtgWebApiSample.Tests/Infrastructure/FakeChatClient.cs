using Microsoft.Extensions.AI;

namespace MtgWebApiSample.Tests.Infrastructure;

internal sealed class FakeChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = new("fake", null, "fake-model");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fake answer from test client."));
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming not used in tests.");

    public object? GetService(Type serviceType, object? serviceKey) => null;

    public void Dispose() { }
}
