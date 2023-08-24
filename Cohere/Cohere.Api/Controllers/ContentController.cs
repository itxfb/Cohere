using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Cohere.Api.Filters;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Content;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Community;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;

using ResourceLibrary;

using Serilog;

namespace Cohere.Api.Controllers
{
    [Obsolete]
    [ApiVersion("1.0")]
    [Route("[controller]")]
    [ApiController]
    public class ContentController : CohereController
    {
        private const int FileSizeLimit5Mb = 1024 * 1024 * 5;
        private const int FileSizeLimit10Mb = 1024 * 1024 * 10;

        private readonly IContentService _contentService;
        private readonly IPostService _postService;
        private readonly IValidator<AttachmentBaseViewModel> _attachmentBaseValidator;
        private readonly IValidator<GetAttachmentViewModel> _getAttachmentValidator;
        private readonly IValidator<AttachmentWithKeyViewModel> _attachmentWithKeyValidator;
        private readonly IStringLocalizer<SharedResource> _localizer;

        private const long _fileSizeLimit = 5_368_709_120; //5 Gb
        private readonly string[] _permittedExtensions = { ".txt" };

        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        public ContentController(
            IContentService contentService,
            IPostService postService,
            IValidator<AttachmentBaseViewModel> attachmentBaseValidator,
            IValidator<GetAttachmentViewModel> getAttachmentValidator,
            IValidator<AttachmentWithKeyViewModel> attachmentWithKeyValidator,
            IStringLocalizer<SharedResource> localizer)
        {
            _contentService = contentService;
            _postService = postService;
            _attachmentBaseValidator = attachmentBaseValidator;
            _getAttachmentValidator = getAttachmentValidator;
            _attachmentWithKeyValidator = attachmentWithKeyValidator;
            _localizer = localizer;
        }

        // POST: /Content/AddAttachmentOnCreate
        [Authorize(Roles = "Cohealer")]
        [HttpPost("AddAttachmentOnCreate")]
        public async Task<IActionResult> AddAttachmentOnCreate(List<IFormFile> file) // size limit for Kestrel and IIS approx 26.4Mb
        {
            //Changes related to multiple upload
            if(AccountId == null) 
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (file.Count > 0 && file.Count < 6)
            {
                OperationResult result = null;
                List<Document> resultList = new List<Document>();
                foreach (var f in file)
                {
                    var fileDetails = GetFileDetails(f.FileName, null, f.ContentType, f.OpenReadStream());
                    result = await _contentService.UploadAttachmentOnContribCreateAsync(fileDetails);
                    if (result.Payload != null)
                    {
                        resultList.Add((Document)result.Payload);
                    }
                }
                if (result.Succeeded || resultList.Count > 0)
                {

                    return Ok(resultList);
                }
                else return BadRequest(new ErrorInfo("Error in uploading the File(s)."));
                
            }

            return BadRequest(new ErrorInfo { Message = "File must not be null and size must be greater than 0 or less than 6" });
        }

        // POST: /Content/AddAttachmentToExisted
        [Authorize(Roles = "Cohealer")]
        [HttpPost("AddAttachmentToExisted")]
        [DisableFormValueModelBinding]
        [RequestSizeLimit(_fileSizeLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = _fileSizeLimit)]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> AddAttachmentToExisted(string contributionId, string sessionId, int partNumber, bool isLastPart, string documentId, string fileName, string uploadId, string PrevETags) // size limit for Kestrel and IIS approx 26.4Mb
        {
            try
            {
                //string contributionId = Request?.Form["contributionId"];//"60b06b554ec402033a244dd7";
                //string sessionId = Request?.Form["sessionId"];//"6b9689bd-99f5-478c-87db-09827bf4102b";
                if (string.IsNullOrEmpty(contributionId) || string.IsNullOrEmpty(sessionId))
                {
                    ModelState.AddModelError("", $"Missing Parameters");
                    return BadRequest(ModelState);
                }

                if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                {
                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 1).");
                    return BadRequest(ModelState);
                }

                // Accumulate the form data key-value pairs in the request (formAccumulator).
                var formAccumulator = new KeyValueAccumulator();
                var trustedFileNameForDisplay = string.Empty;
                //var untrustedFileNameForStorage = string.Empty;
                var boundary = MultipartRequestHelper.GetBoundary(
                    MediaTypeHeaderValue.Parse(Request.ContentType),
                    _defaultFormOptions.MultipartBoundaryLengthLimit);

                var reader = new MultipartReader(boundary, Request.Body);
                var section = await reader.ReadNextSectionAsync();

                OperationResult result = null;

                while (section != null)
                {
                    var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                    if (hasContentDispositionHeader)
                    {
                        if (MultipartRequestHelper
                            .HasFileContentDisposition(contentDisposition))
                        {
                            // Don't trust the file name sent by the client. To display
                            // the file name, HTML-encode the value.
                            trustedFileNameForDisplay = WebUtility.HtmlEncode(
                                    contentDisposition.FileName.Value);

                            var model = new AttachmentWithFileViewModel()
                            {
                                ContributionId = contributionId,
                                SessionId = sessionId
                            };

                            var fileDetails = GetFileDetails(fileName/*trustedFileNameForDisplay*/, null, Request.ContentType, section.Body, uploadId, PrevETags);

                            result = await _contentService.UploadAttachmentForExistedContribAsync(fileDetails, model, partNumber, isLastPart, documentId);

                            if (!ModelState.IsValid)
                            {
                                return BadRequest(ModelState);
                            }
                        }
                        else if (MultipartRequestHelper
                            .HasFormDataContentDisposition(contentDisposition))
                        {
                            // Don't limit the key name length because the
                            // multipart headers length limit is already in effect.
                            var key = HeaderUtilities
                                .RemoveQuotes(contentDisposition.Name).Value;
                            var encoding = GetEncoding(section);

                            if (encoding == null)
                            {
                                ModelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                                // Log error

                                return BadRequest(ModelState);
                            }

                            using (var streamReader = new StreamReader(
                                section.Body,
                                encoding,
                                detectEncodingFromByteOrderMarks: true,
                                bufferSize: 1024,
                                leaveOpen: false))
                            {
                                // The value length limit is enforced by
                                // MultipartBodyLengthLimit
                                var value = await streamReader.ReadToEndAsync();

                                if (string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                                {
                                    value = string.Empty;
                                }

                                formAccumulator.Append(key, value);

                                if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
                                {
                                    // Form key count limit of
                                    // _defaultFormOptions.ValueCountLimit
                                    // is exceeded.
                                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 3).");
                                    // Log error

                                    return BadRequest(ModelState);
                                }
                            }
                        }
                    }

                    // Drain any remaining section body that hasn't been consumed and
                    // read the headers for the next section.
                    section = await reader.ReadNextSectionAsync();
                }

                // **WARNING!**
                // In the following example, the file is saved without
                // scanning the file's contents. In most production
                // scenarios, an anti-virus/anti-malware scanner API
                // is used on the file before making the file available
                // for download or for use by other systems.
                // For more information, see the topic that accompanies
                // this sample app.


                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }

        [Authorize(Roles = "Cohealer")]
        [HttpDelete("DeleteSessionTimeRecording")]
        public async Task<IActionResult> DeleteSessionTimeRecording(string contributionId, string sessionTimeId, string roomId, string compositionFileName)
        {
            var result = await _contentService.DeleteSessionTimeRecording(contributionId, sessionTimeId, roomId, compositionFileName);
            return result.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("AddAttachmentToExistedSessionTime")]
        [DisableFormValueModelBinding]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> AddAttachmentToExistedSessionTime(string contributionId, string sessionTimeId, string roomId, int partNumber, bool isLastPart, string documentId, string fileName, string uploadId, string PrevETags, int duration, string replyLink) // size limit for Kestrel and IIS approx 26.4Mb
        {
            try
            {
                if (string.IsNullOrEmpty(contributionId) || string.IsNullOrEmpty(sessionTimeId))
                {
                    ModelState.AddModelError("", $"Missing Parameters");
                    return BadRequest(ModelState);
                }

                if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                {
                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 1).");
                    return BadRequest(ModelState);
                }

                // Accumulate the form data key-value pairs in the request (formAccumulator).
                var formAccumulator = new KeyValueAccumulator();
                var trustedFileNameForDisplay = string.Empty;
                //var untrustedFileNameForStorage = string.Empty;
                var boundary = MultipartRequestHelper.GetBoundary(
                    MediaTypeHeaderValue.Parse(Request.ContentType),
                    _defaultFormOptions.MultipartBoundaryLengthLimit);

                var reader = new MultipartReader(boundary, Request.Body);
                var section = await reader.ReadNextSectionAsync();

                OperationResult result = null;

                while (section != null)
                {
                    var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                    if (hasContentDispositionHeader)
                    {
                        if (MultipartRequestHelper
                            .HasFileContentDisposition(contentDisposition))
                        {
                            // Don't trust the file name sent by the client. To display
                            // the file name, HTML-encode the value.
                            trustedFileNameForDisplay = WebUtility.HtmlEncode(
                                    contentDisposition.FileName.Value);

                            var model = new AttachmentWithFileViewModel()
                            {
                                ContributionId = contributionId
                            };

                            var fileDetails = GetFileDetails(fileName/*trustedFileNameForDisplay*/, null, Request.ContentType, section.Body, uploadId, PrevETags);
                            if (fileDetails.Extension != ".mp4" && fileDetails.Extension != ".avi" &&
                                fileDetails.Extension != ".webm" && fileDetails.Extension != ".mkv" &&
                                fileDetails.Extension != ".mov")
                            {
                                return BadRequest("Only MP4, Avi, Webm, Mkv and Mov formats are allowed.");
                            }
                            result = await _contentService.UploadSessionTimeRecord(fileDetails, model, sessionTimeId, roomId, partNumber, isLastPart, documentId, duration, replyLink);

                            if (!ModelState.IsValid)
                            {
                                return BadRequest(ModelState);
                            }
                        }
                        else if (MultipartRequestHelper
                            .HasFormDataContentDisposition(contentDisposition))
                        {
                            // Don't limit the key name length because the
                            // multipart headers length limit is already in effect.
                            var key = HeaderUtilities
                                .RemoveQuotes(contentDisposition.Name).Value;
                            var encoding = GetEncoding(section);

                            if (encoding == null)
                            {
                                ModelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                                // Log error

                                return BadRequest(ModelState);
                            }

                            using (var streamReader = new StreamReader(
                                section.Body,
                                encoding,
                                detectEncodingFromByteOrderMarks: true,
                                bufferSize: 1024,
                                leaveOpen: false))
                            {
                                // The value length limit is enforced by
                                // MultipartBodyLengthLimit
                                var value = await streamReader.ReadToEndAsync();

                                if (string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                                {
                                    value = string.Empty;
                                }

                                formAccumulator.Append(key, value);

                                if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
                                {
                                    // Form key count limit of
                                    // _defaultFormOptions.ValueCountLimit
                                    // is exceeded.
                                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 3).");
                                    // Log error

                                    return BadRequest(ModelState);
                                }
                            }
                        }
                    }

                    // Drain any remaining section body that hasn't been consumed and
                    // read the headers for the next section.
                    section = await reader.ReadNextSectionAsync();
                }

                // **WARNING!**
                // In the following example, the file is saved without
                // scanning the file's contents. In most production
                // scenarios, an anti-virus/anti-malware scanner API
                // is used on the file before making the file available
                // for download or for use by other systems.
                // For more information, see the topic that accompanies
                // this sample app.


                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }


        // POST: /Content/AddAttachmentToExisted
        [Authorize(Roles = "Cohealer, Client")]
        [HttpPost("AddAttachmentToPost")]
        [DisableFormValueModelBinding]
        [RequestSizeLimit(_fileSizeLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = _fileSizeLimit)]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> AddAttachmentToPost(string postId, int partNumber, bool isLastPart, string documentId, string fileName, string fileType, string uploadId, string PrevETags, bool isCommenttype = false) // size limit for Kestrel and IIS approx 26.4Mb  
        {
            try
            {
                if (string.IsNullOrEmpty(postId))
                {
                    ModelState.AddModelError("", $"Missing Parameters");
                    return BadRequest(ModelState);
                }

                if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                {
                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 1).");
                    return BadRequest(ModelState);
                }

                // Accumulate the form data key-value pairs in the request (formAccumulator).
                var formAccumulator = new KeyValueAccumulator();
                var trustedFileNameForDisplay = string.Empty;
                //var untrustedFileNameForStorage = string.Empty;
                var boundary = MultipartRequestHelper.GetBoundary(
                    MediaTypeHeaderValue.Parse(Request.ContentType),
                    _defaultFormOptions.MultipartBoundaryLengthLimit);

                var reader = new MultipartReader(boundary, Request.Body);
                var section = await reader.ReadNextSectionAsync();

                OperationResult result = null;

                while (section != null)
                {
                    var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                    if (hasContentDispositionHeader)
                    {
                        if (MultipartRequestHelper
                            .HasFileContentDisposition(contentDisposition))
                        {
                            // Don't trust the file name sent by the client. To display
                            // the file name, HTML-encode the value.
                            trustedFileNameForDisplay = WebUtility.HtmlEncode(
                                    contentDisposition.FileName.Value);


                            var fileDetails = GetFileDetails(fileName/*trustedFileNameForDisplay*/, fileType, Request.ContentType, section.Body, uploadId, PrevETags);
                            result = await _postService.UploadAttachmentForPostAsync(fileDetails, postId, partNumber, isLastPart, documentId, isCommenttype);
                            if (!ModelState.IsValid)
                            {
                                return BadRequest(ModelState);
                            }
                        }
                        else if (MultipartRequestHelper
                            .HasFormDataContentDisposition(contentDisposition))
                        {
                            // Don't limit the key name length because the
                            // multipart headers length limit is already in effect.
                            var key = HeaderUtilities
                                .RemoveQuotes(contentDisposition.Name).Value;
                            var encoding = GetEncoding(section);

                            if (encoding == null)
                            {
                                ModelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                                // Log error

                                return BadRequest(ModelState);
                            }

                            using (var streamReader = new StreamReader(
                                section.Body,
                                encoding,
                                detectEncodingFromByteOrderMarks: true,
                                bufferSize: 1024,
                                leaveOpen: false))
                            {
                                // The value length limit is enforced by
                                // MultipartBodyLengthLimit
                                var value = await streamReader.ReadToEndAsync();

                                if (string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                                {
                                    value = string.Empty;
                                }

                                formAccumulator.Append(key, value);

                                if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
                                {
                                    // Form key count limit of
                                    // _defaultFormOptions.ValueCountLimit
                                    // is exceeded.
                                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 3).");
                                    // Log error

                                    return BadRequest(ModelState);
                                }
                            }
                        }
                    }

                    // Drain any remaining section body that hasn't been consumed and
                    // read the headers for the next section.
                    section = await reader.ReadNextSectionAsync();
                }

                // **WARNING!**
                // In the following example, the file is saved without
                // scanning the file's contents. In most production
                // scenarios, an anti-virus/anti-malware scanner API
                // is used on the file before making the file available
                // for download or for use by other systems.
                // For more information, see the topic that accompanies
                // this sample app.


                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }

        private FileDetails GetFileDetails(string fileName, string fileType, string contentType, Stream fileStream, string uploadId = null, string prevETags = null)
        {
            var fileExtension = Path.GetExtension(fileName);

            var fileDetails = new FileDetails
            {
                AccountId = AccountId,
                FileStream = fileStream,
                OriginalNameWithExtension = fileName,
                Extension = fileExtension,
                ContentType = contentType,
                FileType = fileType,
                UploadId = uploadId,
                PrevETags = prevETags,
            };

            return fileDetails;
        }

        // GET: /Content/DownloadAttachment
        [Authorize]
        [HttpGet("DownloadAttachment")]
        public async Task<IActionResult> DownloadAttachment([FromQuery] GetAttachmentViewModel model)
        {
            if (AccountId == null)
            {
                return BadRequest("Unable to find Id in bearer token");
            }

            var validationResult = await _getAttachmentValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var downloadResult = await _contentService.DownloadAttachmentAsync(model, AccountId);

            if (!downloadResult.Succeeded)
            {
                return BadRequest(downloadResult.Message);
            }

            return Ok((string)downloadResult.Payload);

            //return new FileStreamResult(fileDetails.FileStream, fileDetails.ContentType)
            //{
            //    FileDownloadName = fileDetails.OriginalNameWithExtension
            //};
        }
        [Authorize]
        [HttpGet("DownloadAttachment-SelfPaced")]
        public async Task<IActionResult> DownloadAttachmentsSelfPacedSession([FromQuery] GetAttachmentViewModel model)
        {
            if (AccountId == null)
            {
                return BadRequest("Unable to find Id in bearer token");
            }
            var validationResult = await _getAttachmentValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }
            var downloadResult = await _contentService.DownloadAttachmentSelfPacedAsync(model, AccountId);
            if (!downloadResult.Succeeded)
            {
                return BadRequest(downloadResult.Message);
            }
            return Ok((string)downloadResult.Payload);
        }
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("DeleteAttachment-Selfpaced")]
        public async Task<IActionResult> DeleteAttachmentSelfPacedSession([FromBody] AttachmentWithKeyViewModel deleteModel, bool isVideo = false)
        {
            if (deleteModel == null)
            {
                return BadRequest(new ErrorInfo("Delete model is null"));
            }
            if (string.IsNullOrEmpty(AccountId))
            {
                return Forbid();
            }
            var validationResult = await _attachmentWithKeyValidator.ValidateAsync(deleteModel);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }
            var result = await _contentService.DeleteAttachmentSelfPacedAsync(deleteModel, AccountId, isVideo);
            if (result.Succeeded)
            {
                return NoContent();
            }
            return BadRequest(new ErrorInfo(result.Message));
        }
        // POST: /Content/DeleteAttachment
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("DeleteAttachment")]
        public async Task<IActionResult> DeleteAttachment([FromBody] AttachmentWithKeyViewModel deleteModel)
        {
            if (deleteModel == null)
            {
                return BadRequest(new ErrorInfo("Delete model is null"));
            }

            if (string.IsNullOrEmpty(AccountId))
            {
                return Forbid();
            }

            var validationResult = await _attachmentWithKeyValidator.ValidateAsync(deleteModel);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var result = await _contentService.DeleteAttachmentAsync(deleteModel, AccountId);

            if (result.Succeeded)
            {
                return NoContent();
            }

            return BadRequest(new ErrorInfo(result.Message));
        }
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("DeleteSelfpacedVideoOnContributionCreation")]
        public async Task<IActionResult> DeleteSelfpacedVideoOnContributionCreation([FromBody] AttachmentWithKeyViewModel deleteModel)
        {
            if (deleteModel == null)
            {
                return BadRequest(new ErrorInfo("Delete model is null"));
            }
            if (string.IsNullOrEmpty(AccountId))
            {
                return Forbid();
            }
            var validationResult = await _attachmentWithKeyValidator.ValidateAsync(deleteModel);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }
            var result = await _contentService.DeleteSelfpacedVideoOnContributionCreation(deleteModel, AccountId);
            if (result.Succeeded)
            {
                return NoContent();
            }
            return BadRequest(new ErrorInfo(result.Message));
        }
        // POST: /Content/AddAvatar
        [Authorize]
        [HttpPost("AddAvatar")]
        public async Task<IActionResult> AddAvatar(IFormFile file) // size limit for Kestrel and IIS approx 26.4Mb
        {
            if (file == null)
            {
                return BadRequest(new ErrorInfo(_localizer["File is null or empty"]));
            }

            if (!file.ContentType.Contains("image"))
            {
                return BadRequest(new ErrorInfo(_localizer["Provide image"]));
            }

            if (file.Length > 0)
            {
                var fileDetails = GetFileDetails(file.FileName, null, file.ContentType, file.OpenReadStream());

                try
                {
                    var result = await _contentService.UploadAvatarAsync(fileDetails);

                    if (result.Succeeded)
                    {
                        return Ok((string)result.Payload);
                    }

                    return BadRequest(new ErrorInfo { Message = result.Message });
                }
                catch (Exception ex)
                {
                    Log.Error($"Uploading avatar for account {fileDetails.AccountId} caused exception {ex.Message}");
                    return StatusCode(500);
                }
            }

            return BadRequest(new ErrorInfo(_localizer["File is null or empty"]));
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("AddRecordedAttachmentOnContribution")]
        [DisableFormValueModelBinding]
        [RequestSizeLimit(_fileSizeLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = _fileSizeLimit)]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> AddRecordedAttachmentOnContribution(string contributionId, string duration, int partNumber, bool isLastPart, string documentId, string fileName, string uploadId, string prevETags)
        {
            try
            {
                if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                {
                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 1).");
                    return BadRequest(ModelState);
                }

                var formAccumulator = default(KeyValueAccumulator);

                var boundary = MultipartRequestHelper.GetBoundary(
                    MediaTypeHeaderValue.Parse(Request.ContentType),
                    _defaultFormOptions.MultipartBoundaryLengthLimit);

                var reader = new MultipartReader(boundary, Request.Body);
                var section = await reader.ReadNextSectionAsync();

                OperationResult result = null;

                while (section != null)
                {
                    var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                    if (hasContentDispositionHeader)
                    {
                        if (MultipartRequestHelper
                            .HasFileContentDisposition(contentDisposition))
                        {
                            // Don't trust the file name sent by the client. To display
                            // the file name, HTML-encode the value.
                            WebUtility.HtmlEncode(contentDisposition.FileName.Value);


                            var fileDetails = GetFileDetails(fileName/*trustedFileNameForDisplay*/, Request.ContentType, null, section.Body, uploadId, prevETags);

                            result = await _contentService.UploadRecordedAttachmentOnContribCreateAsync(fileDetails, partNumber, isLastPart, documentId, contributionId, duration);

                            if (!ModelState.IsValid)
                            {
                                return BadRequest(ModelState);
                            }
                        }
                        else if (MultipartRequestHelper
                            .HasFormDataContentDisposition(contentDisposition))
                        {
                            // Don't limit the key name length because the
                            // multipart headers length limit is already in effect.
                            var key = HeaderUtilities
                                .RemoveQuotes(contentDisposition.Name).Value;
                            var encoding = GetEncoding(section);

                            if (encoding == null)
                            {
                                ModelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                                // Log error

                                return BadRequest(ModelState);
                            }

                            using (var streamReader = new StreamReader(
                                section.Body,
                                encoding,
                                detectEncodingFromByteOrderMarks: true,
                                bufferSize: 1024,
                                leaveOpen: false))
                            {
                                // The value length limit is enforced by
                                // MultipartBodyLengthLimit
                                var value = await streamReader.ReadToEndAsync();

                                if (string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                                {
                                    value = string.Empty;
                                }

                                formAccumulator.Append(key, value);

                                if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
                                {
                                    // Form key count limit of
                                    // _defaultFormOptions.ValueCountLimit
                                    // is exceeded.
                                    ModelState.AddModelError("File", $"The request couldn't be processed (Error 3).");
                                    // Log error

                                    return BadRequest(ModelState);
                                }
                            }
                        }
                    }

                    // Drain any remaining section body that hasn't been consumed and
                    // read the headers for the next section.
                    section = await reader.ReadNextSectionAsync();
                }

                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }

        // POST: /Content/AddRecordedAttachmentOnContribution
        // [Authorize(Roles = "Cohealer")]
        // [HttpPost("AddRecordedAttachmentOnContribution")]// size limit for Kestrel and IIS approx 26.4 mb
        // public async Task<IActionResult> AddRecordedAttachmentOnContribution(IFormFile file, string contributionId, string duration)
        // {
        //     if (file?.Length == 0)
        //     {
        //         return BadRequest(new ErrorInfo("File must not be null and size must be greater than 0"));
        //     }
        //
        //     var fileDetails = GetFileDetails(file?.FileName, file?.ContentType, file?.OpenReadStream());
        //
        //     var result = await _contentService.UploadAttachmentOnContribCreateAsync(fileDetails, contributionId, duration);
        //
        //     if (result.Succeeded)
        //     {
        //         return Ok((Document) result.Payload);
        //     }
        //
        //     return BadRequest(new ErrorInfo(result.Message));
        // }

        // POST: /Content/AddPublicImage
        [Authorize]
        [HttpPost("AddPublicImage")]
        public async Task<IActionResult> AddPublicImage(IFormFile file) // size limit for Kestrel and IIS approx 26.4Mb
        {
            if (file is null || file.Length <= 0)
            {
                return BadRequest(new ErrorInfo(_localizer["File is null or empty"]));
            }

            if (!file.ContentType.Contains("image"))
            {
                return BadRequest(new ErrorInfo(_localizer["Provide image"]));
            }

            var fileDetails = GetFileDetails(file.FileName, null, file.ContentType, file.OpenReadStream());

            if (file.Length > (1024*1024*30))
            {
                return BadRequest("Too large file. Max size is 10MB");
            }

            try
            {
                var result = await _contentService.UploadPublicFileAsync(fileDetails);

                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                Log.Error($"Uploading public image for account {fileDetails.AccountId} caused exception {ex.Message}");
                return StatusCode(500);
            }
        }
      
        [Authorize]
        [HttpPost("AddPublicFile")]
        public async Task<IActionResult> AddPublicFile(IFormFile file) // size limit for Kestrel and IIS approx 26.4Mb
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new ErrorInfo(_localizer["File is null or empty"]));
            }

            var fileDetails = GetFileDetails(file.FileName, null, file.ContentType, file.OpenReadStream());

            try
            {
                var result = await _contentService.UploadPublicFileAsync(fileDetails);

                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                Log.Error($"Uploading public image for account {fileDetails.AccountId} caused exception {ex.Message}");
                return StatusCode(500);
            }
        }

        [Authorize]
        [HttpPost("AddTestimonialAvatar")]
        public async Task<IActionResult> AddTestimonialAvatar(IFormFile file, [FromQuery] string contributionId, [FromQuery] string previousUrl)
        {
            if (file == null)
            {
                return BadRequest(new ErrorInfo(_localizer["File is null or empty"]));
            }

            if (!file.ContentType.Contains("image"))
            {
                return BadRequest(new ErrorInfo(_localizer["Provide image"]));
            }

            if (file.Length > 0)
            {
                var fileDetails = GetFileDetails(file.FileName, null, file.ContentType, file.OpenReadStream());

                try
                {
                    var result = await _contentService.UploadTestimonialAvatarAsync(fileDetails, contributionId, previousUrl);

                    if (result.Succeeded)
                    {
                        return Ok((string)result.Payload);
                    }

                    return BadRequest(new ErrorInfo { Message = result.Message });
                }
                catch (Exception ex)
                {
                    Log.Error($"Uploading testimonial avatar for contribution {contributionId} caused exception {ex.Message}");
                    return StatusCode(500);
                }
            }

            return BadRequest(new ErrorInfo(_localizer["File is null or empty"]));
        }
        [Authorize]
        [HttpPost("RemoveCustomLogo")]
        public async Task<IActionResult> RemoveCustomLogo([FromQuery] string imageUrl)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return Forbid();
            }
            var result = await _contentService.RemoveCustomLogo(AccountId, imageUrl);

            if (result.Succeeded)
            {
                return Ok("Custom logo removed.");
            }

            return BadRequest(new ErrorInfo(result.Message));
        }
        private FileDetails GetFileDetails(IFormFile file)
        {
            var fileExtension = Path.GetExtension(file.FileName);

            var fileDetails = new FileDetails
            {
                AccountId = AccountId,
                FileStream = file.OpenReadStream(),
                OriginalNameWithExtension = file.FileName,
                Extension = fileExtension,
                ContentType = file.ContentType
            };

            return fileDetails;
        }

        // added when upgraded .net core 3.1 to .net 6		
        // Todo: UTF7 is being used here just for validation. todo: workaround without using it (then remove Obsolete attribute from the class)
        [Obsolete]
        private static Encoding GetEncoding(MultipartSection section)
        {
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out var mediaType);

            // UTF-7 is insecure and shouldn't be honored. UTF-8 succeeds in most cases.
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }

            return mediaType.Encoding;
        }
    }

    public static class MultipartRequestHelper
    {
        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec at https://tools.ietf.org/html/rfc2046#section-5.1 states that 70 characters is a reasonable limit.
        public static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException(
                    $"Multipart boundary length limit {lengthLimit} exceeded.");
            }

            return boundary;
        }

        public static bool IsMultipartContentType(string contentType)
        {
            return !string.IsNullOrEmpty(contentType)
                   && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="key";
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && string.IsNullOrEmpty(contentDisposition.FileName.Value)
                && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
        }

        public static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                    || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
        }
    }
}