using System.Collections.Concurrent;

namespace MiniERP.UI.Messaging
{
    internal class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
        public void Publish<TMessage>(TMessage message)
        {
            if (message == null) return;

            var messageType = typeof(TMessage);

            if (_handlers.TryGetValue(messageType, out var handlerList))
            {
                var snapshot = handlerList.ToArray();
                foreach (var handler in snapshot)
                {
                    if (handler is Action<TMessage> action)
                        action(message);
                }
            }
        }

        public void Subscribe<TMessage>(Action<TMessage> handler)
        {
            var messageType = typeof(TMessage);

            var list = _handlers.GetOrAdd(messageType, _ => new List<Delegate>());

            lock (list)
            {
                if (!list.Contains(handler))
                    list.Add(handler);
            }
        }

        public void Unsubscribe<TMessage>(Action<TMessage> handler)
        {
            var messageType = typeof(TMessage);

            if (_handlers.TryGetValue(messageType, out var list))
            {
                lock (list)
                    list.Remove(handler);
            }
        }
    }
}
