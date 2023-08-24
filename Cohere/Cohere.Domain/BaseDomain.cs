using System;

namespace Cohere.Domain
{
    public class BaseDomain
    {
        public string Id { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }
    }
}