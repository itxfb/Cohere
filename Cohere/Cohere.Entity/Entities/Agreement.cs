using Cohere.Entity.Enums;

namespace Cohere.Entity.Entities
{
    public class Agreement : BaseEntity
    {
        public AgreementTypes AgreementType { get; set; }

        public string FileUrl { get; set; }

        public string FileNameWithExtension { get; set; }

        public bool IsLatest { get; set; }
    }
}
