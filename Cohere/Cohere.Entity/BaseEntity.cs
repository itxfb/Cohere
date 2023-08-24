using System;

namespace Cohere.Entity
{
    public class BaseEntity
    {
        public string Id { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }
    }
}