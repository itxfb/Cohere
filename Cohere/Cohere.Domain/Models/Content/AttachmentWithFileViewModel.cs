using Microsoft.AspNetCore.Http;

namespace Cohere.Domain.Models.Content
{
    public class AttachmentWithFileViewModel : AttachmentBaseViewModel
    {
        public IFormFile File { get; set; }
    }
}
