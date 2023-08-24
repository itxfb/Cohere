using System;

namespace Cohere.Domain.Models.ContributionViewModels
{
    public class PresignedUrlViewModel
    {
        public string PresignedUrl { get; set; }

        public DateTime? DateAvailable { get; set; }

        public string Duration { get; set; }
    }
}