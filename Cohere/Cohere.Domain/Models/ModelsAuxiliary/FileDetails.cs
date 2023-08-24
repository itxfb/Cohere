using System.IO;

namespace Cohere.Domain.Models.ModelsAuxiliary
{
    public class FileDetails
    {
        public string AccountId { get; set; }

        public Stream FileStream { get; set; }

        public string OriginalNameWithExtension { get; set; }

        public string Extension { get; set; }

        public string ContentType { get; set; }

        public string FileType { get; set; }

        public string UploadId { get; set; }

        public string PrevETags { get; set; }
    }
}
