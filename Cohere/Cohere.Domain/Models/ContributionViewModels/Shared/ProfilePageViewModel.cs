using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ProfilePageViewModel
    {
        public string UserId { get; set; }
        public string PrimaryColor { get; set; }
        public string AccentColor { get; set; }
        public string TertiaryColor { get; set; }
        public string CustomLogo { get; set; }
        public string ImageOrVideoPath { get; set; }
        public string SubtagLine { get; set; }
        public string Tagline { get; set; }
        public string PersonalUrl { get; set; }
        public string PrimaryBannerUrl { get; set; }
        public string BioBannerUrl { get; set; }
        public bool IsCommunityEnabled { get; set; }
        public bool IsCommunityPostEnabled { get; set; }
        public bool IsDarkModeEnabled { get; set; }
        public bool IsMessagingEnabled { get; set; }
        public bool IsPrimaryBannerVideo { get; set; }
        public List<string> Tags { get; set; }
        public List<ContributionDTO> Contributions { get; set; }
        public List<ProfileFollowers> Followers { get; set; }
        public List<CustomLinks> CustomLinks { get; set; }
        public bool UpdationAllowed { get; set; } = false;
        public DateTime LastUpdatedTime { get; set; }

    }
}
