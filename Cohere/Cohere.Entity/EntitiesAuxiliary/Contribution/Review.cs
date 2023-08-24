using System;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class Review
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public string FirstName { get; set; }

        public string AvatarUrl { get; set; }

        public int Rate { get; set; }

        public string Text { get; set; }

        public DateTime DateTimeAdded { get; set; }
    }
}
