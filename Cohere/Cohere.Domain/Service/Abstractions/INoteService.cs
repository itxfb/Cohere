using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Note;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface INoteService
    {
        Task<NoteViewModel> GetClassNoteAsync(string accountId, string contributionId, string classId, string subclassId);

        Task<IEnumerable<NoteViewModel>> GetContributionNotesAsync(string accountId, string contributionId);

        Task<IEnumerable<NoteViewModel>> GetNotesAsync(string accountId);

        Task<OperationResult> Insert(string accountId, NoteBriefViewModel model);

        Task<OperationResult> Update(string accountId, NoteBriefViewModel model);

        Task<OperationResult> Delete(string accountId, string id);

        Task<OperationResult> Delete(string accountId, string contributionId, string classId, string subclassId);
    }
}
