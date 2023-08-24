namespace Cohere.Domain.Models.ModelsAuxiliary
{
    public class ErrorInfo
    {
        public string Message { get; set; }

        public ErrorInfo()
        {
        }

        public ErrorInfo(string message)
        {
            Message = message;
        }

        public string ErrorCode { set; get; }

        public ErrorInfo(string message, string code)
        {
            Message = message;
            ErrorCode = code;
        }
    }
}
