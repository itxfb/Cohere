using System.Collections.Generic;

namespace Cohere.Entity.Infrastructure.Options
{
    public class MongoSettings
    {
        public Dictionary<string, string> CollectionNames { get; set; }

        public string DatabaseName { get; set; }
    }
}
