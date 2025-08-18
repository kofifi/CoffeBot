using System.Threading.Channels;
using CoffeBot.Models;

namespace CoffeBot.Abstractions;

public interface IChatEventStream
{
    (Guid Id, ChannelReader<ChatEventMessage> Reader) Subscribe();
    void Unsubscribe(Guid id);
    void Publish(ChatEventMessage message);
}
