using System;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class Document
    {
        public string Id { get; set; }

        public string DocumentKeyWithExtension { get; set; }

        public string DocumentOriginalNameWithExtension { get; set; }

        public string ContentType { get; set; }

        public string Duration { get; set; }

        public string Extension { get; set; }
        public string AttachementUrl { get;  set; }
        public override bool Equals(object obj)
        {
            return obj is Document document &&
                   Id == document.Id &&
                   DocumentKeyWithExtension == document.DocumentKeyWithExtension &&
                   DocumentOriginalNameWithExtension == document.DocumentOriginalNameWithExtension &&
                   ContentType == document.ContentType;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, DocumentKeyWithExtension, DocumentOriginalNameWithExtension, ContentType);
        }
    }
}
