using System.Collections;
using System.Collections.Generic;

namespace Cohere.Domain.Models.Community.Post
{
    public class PagedPostDto
    {
        public IEnumerable<PostDto> Posts { get; set; }
        public PostDto UserDraftPost { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}