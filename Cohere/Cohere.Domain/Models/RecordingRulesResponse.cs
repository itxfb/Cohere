using System;

namespace Cohere.Domain.Models
{
    public class RecordingRulesResponse
    {
        public string Room_sid { get; set; }

        public Rules[] Rules { get; set; }

        public DateTime Date_Updated { get; set; }

        public DateTime Date_Created { get; set; }
    }
}