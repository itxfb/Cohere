namespace Cohere.Domain.Models.Payment.Stripe
{
    public class CreateProductViewModel
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string StandardAccountId { get; set; } = null;
    }
}
