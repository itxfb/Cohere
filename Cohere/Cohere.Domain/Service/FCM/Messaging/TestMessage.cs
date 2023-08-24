using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.FCM.Messaging
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Android
    {
        public Notification notification { get; set; }
    }

    public class Apns
    {
        public Payload payload { get; set; }
        public FcmOptions fcm_options { get; set; }
    }

    public class Aps
    {
        [JsonProperty("mutable-content")]
        public int MutableContent { get; set; }
    }

    public class FcmOptions
    {
        public string image { get; set; }
    }

    public class Headers
    {
        public string image { get; set; }
    }

    public class TestMessage
    {
        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }
        public string topic { get; set; }
        public Notification notification { get; set; }
        public Android android { get; set; }
        public Apns apns { get; set; }
        public Webpush webpush { get; set; }
        [JsonProperty(PropertyName = "headers")]
        public IDictionary<string, string> Headers { get; set; }
    }

    public class Notification
    {
        public string title { get; set; }
        public string image { get; set; }
    }

    public class Payload
    {
        public Aps aps { get; set; }
    }

    public class Root
    {
        public Message message { get; set; }
    }

    public class Webpush
    {
        public Headers headers { get; set; }
    }
}
