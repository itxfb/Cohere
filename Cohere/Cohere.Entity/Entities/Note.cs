namespace Cohere.Entity.Entities
{
    public class Note : BaseEntity
    {
        public string UserId { get; set; }

        public string ContributionId { get; set; }

        public string ClassId { get; set; }
        public bool IsPrerecorded { get; set; }
        public string Title { get; set; }
        public string SubClassId { get; set; }
        public string TextContent { get; set; }
    }
}
