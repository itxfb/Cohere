using System;

namespace Cohere.Entity.Entities.Contrib
{
    public class PrivateNote : BaseEntity
    {
        public string UserId { get; set; }

        public string Text { get; set; }

        public DateTime DateTimeAdded { get; set; }
    }
}
