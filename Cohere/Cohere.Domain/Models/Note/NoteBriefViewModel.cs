namespace Cohere.Domain.Models.Note
{
    public class NoteBriefViewModel : BaseDomain
    {
        public string ContributionId { get; set; }

        public string ClassId { get; set; }

        public string Title { get; set; }
        public string SubClassId { get; set; }
        public string TextContent { get; set; }
        public bool IsPrerecorded { get; set; } = false;
}
}
