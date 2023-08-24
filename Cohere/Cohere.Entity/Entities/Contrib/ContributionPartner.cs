namespace Cohere.Entity.Entities.Contrib
{
    public class ContributionPartner
    {
        public string UserId { get; set; }

        public bool IsAssigned { get; set; }

        public string AssignCode { get; set; }

        public string PartnerEmail { get; set; }
    }
}