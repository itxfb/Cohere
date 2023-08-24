using Cohere.Entity.EntitiesAuxiliary.User;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ParticipantViewModel
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        public string NameSuffix { get; set; }

        public string AvatarUrl { get; set; }

        public ClientPreferences ClientPreferences { get; set; }

        public string Bio { get; set; }
        
        public bool IsAddedByAccessCode { get; set; }
    }
}
