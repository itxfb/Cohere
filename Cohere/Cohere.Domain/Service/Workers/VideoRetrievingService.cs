using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SQS;
using Amazon.SQS.Model;
using Cohere.Domain.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Video.V1;
using Twilio.Rest.Video.V1.Room;

namespace Cohere.Domain.Service.Workers
{
    [Obsolete]
    public class VideoRetrievingService : BackgroundService
    {
        private const int DefaultPartSize = 25 * 1024 * 1024;
        private readonly ILogger<VideoRetrievingService> _logger;
        private readonly IAmazonSQS _amazonSQS;
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly string _videoRetrievalQueueUrl;
        private readonly string _videoCompletedQueueUrl;
        private readonly string _twilioAccountSid;
        private readonly string _twilioAccessToken;

        public VideoRetrievingService(
            ILogger<VideoRetrievingService> logger,
            IAmazonSQS amazonSQS,
            string videoRetrievalQueueUrl,
            string videoCompletedQueueUrl,
            IAmazonS3 amazonS3,
            string bucketName,
            string twilioAccountSid,
            string twilioAccessToken)
        {
            _logger = logger;
            _amazonSQS = amazonSQS;
            _amazonS3 = amazonS3;
            _bucketName = bucketName;
            _videoRetrievalQueueUrl = videoRetrievalQueueUrl;
            _videoCompletedQueueUrl = videoCompletedQueueUrl;
            _twilioAccountSid = twilioAccountSid;
            _twilioAccessToken = twilioAccessToken;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = _videoRetrievalQueueUrl,
                        WaitTimeSeconds = 5
                    };

                    var messages = (await _amazonSQS.ReceiveMessageAsync(request, cancellationToken)).Messages;
                    foreach (var message in messages)
                    {
                        try
                        {
                            _logger.LogInformation("Start processing message {message} | {time}", message.Body, DateTime.UtcNow);
                            var videoRetrievalRequest = JsonSerializer.Deserialize<VideoRetrievalMessage>(message.Body);
                            (var compositionFileName, var compositionDuration) = await DownloadVideo(videoRetrievalRequest);
                            await SendVideoCompletedStatus(videoRetrievalRequest.ContributionId, videoRetrievalRequest.RoomId, compositionFileName, compositionDuration);
                            await _amazonSQS.DeleteMessageAsync(_videoRetrievalQueueUrl, message.ReceiptHandle);
                            await DeleteRecordingsFromTwilio(videoRetrievalRequest.RoomId);
                            await DeleteCompositionsFromTwilio(videoRetrievalRequest.RoomId);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error during retrieving video");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error during retrieving video");
                }
            }
        }

        private async Task DeleteCompositionsFromTwilio(string roomId)
        {
            TwilioClient.Init(_twilioAccountSid, _twilioAccessToken);

            var allCompositions = await CompositionResource.ReadAsync(new ReadCompositionOptions() { RoomSid = roomId });

            foreach (var composition in allCompositions)
            {
                await CompositionResource.DeleteAsync(composition.Sid);

                _logger.LogInformation("Composition({compositionId}) was deleted in Room({roomId})", composition.Sid, roomId);
            }
        }

        private async Task DeleteRecordingsFromTwilio(string roomId)
        {
            TwilioClient.Init(_twilioAccountSid, _twilioAccessToken);

            var allRecordings = await RoomRecordingResource.ReadAsync(roomId);

            foreach (var recording in allRecordings)
            {
                await RecordingResource.DeleteAsync(recording.Sid);
                _logger.LogInformation("Recording({recordingId}) was deleted in Room({roomId})", recording.Sid, roomId);
            }
        }

        private async Task SendVideoCompletedStatus(string contributionId, string roomId, string compositionFileName, int? compositionDuration)
        {
            var message = new VideoCompletedMessage
            {
                RoomId = roomId,
                ContributionId = contributionId,
                CompositionDuration = compositionDuration,
                CompositionFileName = compositionFileName
            };

            await _amazonSQS.SendMessageAsync(_videoCompletedQueueUrl, JsonSerializer.Serialize(message));
        }

        private async Task<(string, int?)> DownloadVideo(VideoRetrievalMessage videoRetrievalRequest)
        {
            var compositionSid = videoRetrievalRequest.CompositionId;

            TwilioClient.Init(_twilioAccountSid, _twilioAccessToken);

            var composition = await CompositionResource.FetchAsync(compositionSid);
            var format = composition.Format.ToString();

            using (var resp = GetCompositionRemoteFile(compositionSid))
            using (var s3Stream = new RemoteFileStream(resp.GetResponseStream(), resp.ContentLength))
            {
                var fileName = $"{videoRetrievalRequest.CompositionId}.{format}";
                var fileTransferUtilityRequest = new TransferUtilityUploadRequest
                {
                    BucketName = _bucketName,
                    InputStream = s3Stream,
                    AutoResetStreamPosition = false,
                    AutoCloseStream = false,
                    Key = $"Videos/Rooms/{videoRetrievalRequest.RoomId}/Compositions/{fileName}",
                    PartSize = DefaultPartSize
                };

                using (var util = new TransferUtility(_amazonS3))
                {
                    util.Upload(fileTransferUtilityRequest);
                }

                return (fileName, composition.Duration);
            }
        }

        private WebResponse GetCompositionRemoteFile(string compositionSid)
        {
            var request = (HttpWebRequest)WebRequest.Create(
                $"https://video.twilio.com/v1/Compositions/{compositionSid}/Media?Ttl=3600");

            request.Headers.Add("Authorization", BuildAuthHeader());
            return request.GetResponse();
        }

        private string BuildAuthHeader()
        {
            return "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(_twilioAccountSid + ":" + _twilioAccessToken));
        }

        //Due to file downloading stream not have Length property
        //we need to wrap and implement method required for multipart uploads
        private class RemoteFileStream : Stream
        {
            private readonly Stream _stream;
            private readonly long _contentLength;
            private long _currentPosition;

            public RemoteFileStream(Stream stream, long contentLength)
            {
                _stream = stream;
                _contentLength = contentLength;
            }

            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => _contentLength;

            public override long Position { get => _currentPosition; set => throw new NotImplementedException(); }

            public override void Flush() => throw new NotImplementedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var readCount = _stream.Read(buffer, offset, count);
                _currentPosition += readCount;
                return readCount;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (offset == 0 && origin == SeekOrigin.Begin)
                {
                    return 0;
                }

                throw new NotImplementedException();
            }

            public override void SetLength(long value) => throw new NotImplementedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

            public new void Dispose()
            {
                _stream.Dispose();
            }

            public override ValueTask DisposeAsync()
            {
                return _stream.DisposeAsync();
            }
        }
    }
}
