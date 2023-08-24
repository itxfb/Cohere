using Cohere.Domain.Models.ContributionViewModels.ForClient;

namespace Cohere.Domain.Infrastructure
{
    public class BookOneToOneResult
    {
        public static BookOneToOneResult Success(string message, BookOneToOneTimeResultViewModel bookTimesResult = null) =>
            new BookOneToOneResult(true, message, bookTimesResult);

        public static BookOneToOneResult Failure(string message, BookOneToOneTimeResultViewModel bookTimesResult = null) =>
            new BookOneToOneResult(false, message, bookTimesResult);

        public bool Succeeded { get; }

        public string Message { get; }

        public BookOneToOneTimeResultViewModel BookTimesResult { get; }

        public BookOneToOneResult(bool succeeded, string message, BookOneToOneTimeResultViewModel bookTimesResult = null)
        {
            Succeeded = succeeded;
            Message = message;
            BookTimesResult = bookTimesResult;
        }
    }
}
