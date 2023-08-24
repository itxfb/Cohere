namespace Cohere.Entity.Infrastructure.Options
{
    public class S3Settings
    {
        public string PublicBucketName { get; set; }

        public string NonPublicBucketName { get; set; }

        public string RegionName { get; set; }
    }
}