namespace MiniERP.UI.Messaging
{
    public enum EntityChangeType
    {
        Created,
        Updated,
        Deleted
    }

    public class EntityChangedMessage<T>
    {
        public EntityChangedMessage(T entity, EntityChangeType changeType)
        {
            Entity = entity;
            ChangeType = changeType;
        }

        public T Entity { get; }
        public EntityChangeType ChangeType { get; }
    }
}
