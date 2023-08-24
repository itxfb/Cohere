using System.Threading.Tasks;

using Cohere.Api.Utils;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Note;
using Cohere.Domain.Service.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NoteController : CohereController
    {
        private readonly IValidator<NoteBriefViewModel> _createNoteValidator;
        private readonly INoteService _noteService;

        public NoteController(
            INoteService noteService,
            IValidator<NoteBriefViewModel> createNoteValidator)
        {
            _noteService = noteService;
            _createNoteValidator = createNoteValidator;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Add(NoteBriefViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _createNoteValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var result = await _noteService.Insert(AccountId, model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> Update(NoteBriefViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _createNoteValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var result = await _noteService.Update(AccountId, model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetUserNotes()
        {
            var notes = await _noteService.GetNotesAsync(AccountId);
            return Ok(notes);
        }

        [Authorize]
        [HttpDelete("{contributionId}/{classId}")]
        public async Task<IActionResult> Delete(string contributionId, string classId, string subclassId)
        {
            if (contributionId == null || classId == null)
            {
                return BadRequest();
            }

            var result = await _noteService.Delete(AccountId, contributionId, classId, subclassId);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return BadRequest();
            }

            var result = await _noteService.Delete(AccountId, id);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [Authorize]
        [HttpGet("{contributionId}")]
        public async Task<IActionResult> GetContributionNotes(string contributionId)
        {
            if (contributionId == null)
            {
                return BadRequest();
            }

            var notes = await _noteService.GetContributionNotesAsync(AccountId, contributionId);
            return Ok(notes);
        }

        [Authorize]
        [HttpGet("{contributionId}/{classId}")]
        public async Task<IActionResult> GetClassNote(string contributionId, string classId, string subclassId)
        {
            if (contributionId == null || classId == null)
            {
                return BadRequest();
            }

            var note = await _noteService.GetClassNoteAsync(AccountId, contributionId, classId, subclassId);
            if (note != null && !string.IsNullOrEmpty(note.SubClassId))
            {
               note.IsPrerecorded = true;
            }
            return Ok(note);
        }
    }
}