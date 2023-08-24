namespace Cohere.Domain.Models.Video
{
    public class GetVideoTokenViewModel
    {
        public string ContributionId { get; set; }

        public string ClassId { get; set; }

        public string IdentityName { get; set; }

        public bool? RecordParticipantsOnConnect { get; set; }
    }
}
