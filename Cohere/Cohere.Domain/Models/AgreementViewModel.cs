namespace Cohere.Domain.Models
{
    public class AgreementViewModel : BaseDomain
    {
        public string AgreementType { get; set; }

        public string FileUrl { get; set; }

        public string FileNameWithExtension { get; set; }

        public bool IsLatest { get; set; }
    }
}
