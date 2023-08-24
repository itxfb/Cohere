namespace Cohere.Entity.Entities
{
    public class Currency : BaseEntity
    {
        public string CountryCode { get; set; }

        public string Code { get; set; }

        public string Symbol { get; set; }

        public bool IsUserDefaultCurrency { set; get; }
    }
}