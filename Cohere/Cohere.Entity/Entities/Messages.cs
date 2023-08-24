using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class Messages : BaseEntity
    {
        public string title { get; set; }
        [JsonIgnore]
        public int priority { get; set; }
    }
}
