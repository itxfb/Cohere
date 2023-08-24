using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Cohere.Domain.Service.Abstractions;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
    public class EmailService : IEmailService
    {
        private readonly string _sourceAddress;
        private readonly ILogger<EmailService> _logger;
        private readonly IAmazonSimpleEmailService _emailServiceClient;

        public EmailService(
            string sourceAddress,
            ILogger<EmailService> logger,
            IAmazonSimpleEmailService emailServiceClient)
        {
            _sourceAddress = !string.IsNullOrWhiteSpace(sourceAddress)
                ? sourceAddress
                : throw new ArgumentException($"Source address {nameof(sourceAddress)} argument cannot be null or empty.");

            _logger = logger;
            _emailServiceClient = emailServiceClient;
        }

        public async Task SendAsync(string receiverAddress, string subject, string htmlContent)
        {
            await SendAsync(new[] { receiverAddress }, subject, htmlContent);
        }

        public async Task SendAsync(IEnumerable<string> receiverAddresses, string subject, string htmlContent)
        {
            var sendRequest = FormSendEmailRequest(receiverAddresses, subject, htmlContent);
            try
            {
                await _emailServiceClient.SendEmailAsync(sendRequest);
            }
            catch (AmazonSimpleEmailServiceException ex)
            {
                _logger.LogError($"Unable to send email to { string.Join(' ', sendRequest.Destination.ToAddresses)}. Error occured during email sending {ex.Message}");
            }
        }

        public void Send(string receiverAddress, string subject, string htmlContent)
        {
            Send(new[] { receiverAddress }, subject, htmlContent);
        }

        public void Send(IEnumerable<string> receiverAddresses, string subject, string htmlContent)
        {
            var sendRequest = FormSendEmailRequest(receiverAddresses, subject, htmlContent);
            try
            {
                _emailServiceClient.SendEmailAsync(sendRequest).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (AmazonSimpleEmailServiceException ex)
            {
                _logger.LogError($"Unable to send email to { string.Join(' ', sendRequest.Destination.ToAddresses)}. Error occured during email sending {ex.Message}");
            }
        }

        public async Task SendWithAttachmentsAsync(string recipient, string subject, string htmlContent, AttachmentCollection attachments)
        {
            await SendWithAttachmentsAsync(_sourceAddress, recipient, subject, htmlContent, attachments);
        }

        public async Task SendWithAttachmentsAsync(string sourceAddress, string recipient, string subject, string htmlContent, AttachmentCollection attachments, bool sendIcalAttachment = true)
        {
            if (!sendIcalAttachment)
                attachments = null;
            var rawMessageStream = GetMessageStream(sourceAddress, new[] { recipient }, subject, htmlContent, attachments);
            var sendRequest = new SendRawEmailRequest { RawMessage = new RawMessage(rawMessageStream) };
            try
            {
                await _emailServiceClient.SendRawEmailAsync(sendRequest);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error during sending email with attachment");
            }
        }

        private MimeEntity BuildRawMessage(string htmlContent, AttachmentCollection attachments = null)
        {
            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = htmlContent;
            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    bodyBuilder.Attachments.Add(attachment);
                }
            }

            return bodyBuilder.ToMessageBody();
        }

        private MimeMessage GetMessage(string sourceAddress, IEnumerable<string> receiverAddresses, string subject, string htmlContent, AttachmentCollection attachments = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(string.Empty, sourceAddress));
            message.To.AddRange(receiverAddresses.Select(e => new MailboxAddress(string.Empty, e)));
            message.Subject = subject;
            message.Body = BuildRawMessage(htmlContent, attachments);
            return message;
        }

        private MemoryStream GetMessageStream(string sourceAddress, IEnumerable<string> receiverAddresses, string subject, string htmlContent, AttachmentCollection attachments = null)
        {
            var stream = new MemoryStream();
            GetMessage(sourceAddress, receiverAddresses, subject, htmlContent, attachments).WriteTo(stream);
            return stream;
        }

        private SendEmailRequest FormSendEmailRequest(IEnumerable<string> receiverAddresses, string subject, string htmlContent)
        {
            return new SendEmailRequest
            {
                Source = _sourceAddress,
                Destination = new Destination
                {
                    ToAddresses = receiverAddresses.ToList(),
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = htmlContent,
                        },
                    },
                },
            };
        }
    }
}
