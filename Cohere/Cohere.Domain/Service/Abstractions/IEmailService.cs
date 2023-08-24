using System.Collections.Generic;
using System.Threading.Tasks;
using MimeKit;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IEmailService
    {
        Task SendAsync(string receiverAddress, string subject, string htmlContent);

        Task SendAsync(IEnumerable<string> receiverAddresses, string subject, string htmlContent);

        void Send(string receiverAddress, string subject, string htmlContent);

        void Send(IEnumerable<string> receiverAddresses, string subject, string htmlContent);

        Task SendWithAttachmentsAsync(string sourceAddress, string recipient, string subject, string htmlContent, AttachmentCollection attachments, bool sendIcalAttachment = true);

        Task SendWithAttachmentsAsync(string recipient, string subject, string htmlContent, AttachmentCollection attachments);
    }
}
