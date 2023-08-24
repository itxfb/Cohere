namespace Cohere.Api.Controllers.Models
{
    public class RescheduleOneToOneRequestModel
    {
        public string ContributionId { get; set; }

        public string RescheduleToId { get; set; }

        public string RescheduleFromId { get; set; }

        public string Note { get; set; }

        public int Offset { get; set; }
    }
}
