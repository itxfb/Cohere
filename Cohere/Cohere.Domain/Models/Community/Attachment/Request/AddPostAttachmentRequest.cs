using Microsoft.AspNetCore.Http;

namespace Cohere.Domain.Models.Community.Attachment.Request
{
    public class AddPostAttachmentRequest
    {
        public string PostId { get; set; }
        public IFormFile File { get; set; }
        public string FileName { get; set; }
    }
}