namespace Cohere.Entity.Infrastructure.Options
{
    public class SqsSettings
    {
        public string ActiveCampaignQueueUrl { get; set; }

        public string VideoRetrievalQueueUrl { get; set; }

        public string VideoCompletedQueueUrl { get; set; }

        public string ZoomVideoCompletedQueueUrl { get; set; }

        public string RegionName { get; set; }
    }
}
