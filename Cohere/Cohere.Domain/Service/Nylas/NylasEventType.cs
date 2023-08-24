using System.Runtime.Serialization;

namespace Cohere.Domain.Service.Nylas
{
    public enum NylasEventType
    {
        [EnumMember(Value = "event.created")]
        EventCreated = 1,

        [EnumMember(Value = "event.updated")]
        EventUpdated = 2,

        [EnumMember(Value = "event.deleted")]
        EventDeleted = 3,

        [EnumMember(Value = "message.created")]
        MessageCreated = 4
    }
}