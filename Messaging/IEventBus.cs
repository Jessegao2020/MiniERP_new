namespace MiniERP.UI.Messaging
{
    public interface IEventBus
    {
        void Publish<TMessage>(TMessage message);
        void Subscribe<TMessage>(Action<TMessage> handler);
        void Unsubscribe<TMessage>(Action<TMessage> handler);
    }
}
