using System.Collections.Concurrent;
using System.Threading.Channels;
using CoffeBot.Abstractions;
using CoffeBot.Models;

namespace CoffeBot.Services;

public class ChatEventStream : IChatEventStream
{
    private readonly ConcurrentDictionary<Guid, Channel<ChatEventMessage>> _channels = new();

    public (Guid Id, ChannelReader<ChatEventMessage> Reader) Subscribe()
    {
        var channel = Channel.CreateUnbounded<ChatEventMessage>();
        var id = Guid.NewGuid();
        _channels[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_channels.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public void Publish(ChatEventMessage message)
    {
        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryWrite(message);
        }
    }
}
