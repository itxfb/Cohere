namespace Cohere.Entity.Entities.Payment
{
    public class StripeEvent : BaseEntity
    {
        public string StripeEventId { get; set; }

        public string LastErrorMessage { get; set; }

        public bool IsProcessed { get; set; }
    }
}
