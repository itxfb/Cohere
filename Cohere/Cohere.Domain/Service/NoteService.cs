using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Note;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;

namespace Cohere.Domain.Service
{
    public class NoteService : INoteService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IContributionRootService _contributionRootService;
        private readonly IMemoryCache _memoryCache;
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;

        public NoteService(IUnitOfWork unitOfWork, IMapper mapper,
            IContributionRootService contributionRootService,
            IMemoryCache memoryCache, IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _contributionRootService = contributionRootService;
            _memoryCache = memoryCache;
            _paidTiersService = paidTiersService;
        }

        public async Task<NoteViewModel> GetClassNoteAsync(string accountId, string contributionId, string classId, string subclassId)
        {
            var note = new Note();
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if(!string.IsNullOrEmpty(subclassId))
            {
                 note = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n =>
               n.UserId == user.Id && n.ContributionId == contributionId && n.ClassId == classId && n.SubClassId == subclassId);
            }
            else
            {
                note = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n =>
                n.UserId == user.Id && n.ContributionId == contributionId && n.ClassId == classId);
            }
            return _mapper.Map<NoteViewModel>(note);
        }

        public async Task<IEnumerable<NoteViewModel>> GetContributionNotesAsync(string accountId, string contributionId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var notes = await _unitOfWork.GetRepositoryAsync<Note>().Get(n => n.UserId == user.Id && n.ContributionId == contributionId);
            return _mapper.Map<IEnumerable<NoteViewModel>>(notes);
        }

        public async Task<IEnumerable<NoteViewModel>> GetNotesAsync(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var notes = await _unitOfWork.GetRepositoryAsync<Note>().Get(n => n.UserId == user.Id);
            return _mapper.Map<IEnumerable<NoteViewModel>>(notes);
        }

        public async Task<OperationResult> Insert(string accountId, NoteBriefViewModel model)
        {
            var existedNote = new Note();
            var contribution = await _contributionRootService.GetOne(model.ContributionId);
            if (contribution == null)
            {
                return OperationResult.Failure("Contribution was not found");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if (user == null)
            {
                return OperationResult.Failure("User was not found");
            }

            if (string.IsNullOrEmpty(model.SubClassId))
            {
                existedNote = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n =>
                n.UserId == user.Id && n.ContributionId == model.ContributionId && n.ClassId == model.ClassId);
            }
            else
            {
                existedNote = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n =>
                 n.UserId == user.Id && n.ContributionId == model.ContributionId && n.ClassId == model.ClassId && n.SubClassId == model.SubClassId);
            }
            if (existedNote != null)
            {
                return OperationResult.Failure("Unable to save note. You have already note for that session");
            }

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(p => p.ContributionId == model.ContributionId && (p.ClientId == user.Id || p.ContributorId==user.Id));

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);

            // check if this is an acedemy course
            bool isAccessiableAcademyContribution = false;
            var bundleInfos = await _unitOfWork.GetRepositoryAsync<BundleInfo>()
                .Get(e => e.BundleParentType == BundleParentType.PaidTierProduct
                          || e.BundleParentType == BundleParentType.PaidTierOption);
            if (bundleInfos?.Count() > 0)
            {
                var contributionBoundles = bundleInfos?.Where(b => b.ItemId == contribution.Id);
                if (contributionBoundles.Any())
                {

                    var currentPaidTier = await _memoryCache.GetOrCreateAsync("currentPaidTier_" + accountId, async entry =>
                    {
                        entry.SetSlidingExpiration(TimeSpan.FromDays(1));
                        return await _paidTiersService.GetCurrentPaidTier(accountId);
                    });
                    contributionBoundles = contributionBoundles.Where(c =>
                        (c.BundleParentType == BundleParentType.PaidTierProduct && c.ParentId == currentPaidTier?.CurrentProductPlanId) ||
                        (c.BundleParentType == BundleParentType.PaidTierOption && c.ParentId == currentPaidTier?.PaidTierOption?.Id));
                    isAccessiableAcademyContribution = contributionBoundles?.Count() > 0;
                }
            }

            var hasAccessToContribution = contribution.ParticipantHasAccessToContribution(contribution, user.Id);

            if (!hasAccessToContribution && !IsCohealer(user, contribution) && (purchaseVm == null || (!purchaseVm.HasSucceededPayment && !isAccessiableAcademyContribution)))
            {
                return OperationResult.Failure("Unable to save the note for unpurchased contribution");
            }

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            switch (contributionVm.Type)
            {
                case nameof(ContributionMembership):
                case nameof(ContributionCommunity):
                case nameof(ContributionCourse):                   
                    if (!(contributionVm is SessionBasedContributionViewModel contribCourseVm)
                        || (contribCourseVm.Sessions.All(s => s.Id != model.ClassId) && contribCourseVm.Sessions.Where(s => s.SessionTimes.Count(st => st.Id != model.ClassId) > 0).ToList().Count > 0)) 
                    {
                        return OperationResult.Failure("Session with provided id doesn't exist");
                    }

                    break;
                case nameof(ContributionOneToOne):
                    if (!(contributionVm is ContributionOneToOneViewModel contribOneToOneVm)
                        || contribOneToOneVm.AvailabilityTimes.SelectMany(s => s.BookedTimes).All(bt => bt.Id != model.ClassId))
                    {
                        return OperationResult.Failure("Session with provided id doesn't exist");
                    }

                    if (!IsCohealer(user, contribution) && !purchaseVm.Payments.Exists(p => p.HasBookedClassId(model.ClassId) && p.PaymentStatus == PaymentStatus.Succeeded))
                    {
                        return OperationResult.Failure("Unable to save the note for unpurchased session");
                    }

                    break;
                default:
                    return OperationResult.Failure("Unable to save the note. Unsupported contribution type");
            }

            var note = _mapper.Map<Note>(model);
            note.UserId = user.Id;

            await _unitOfWork.GetRepositoryAsync<Note>().Insert(note);

            return OperationResult.Success(null, _mapper.Map<NoteViewModel>(note));
        }

        public async Task<OperationResult> Update(string accountId, NoteBriefViewModel model)
        {
            var contribution = await _contributionRootService.GetOne(model.ContributionId);
            var existedNote = new Note();
            if (contribution == null)
            {
                return OperationResult.Failure("Contribution was not found");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if (user == null)
            {
                return OperationResult.Failure("User was not found");
            }

            if (string.IsNullOrEmpty(model.SubClassId))
            {
                existedNote = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n =>
                n.Id == model.Id && n.UserId == user.Id && n.ContributionId == model.ContributionId && n.ClassId == model.ClassId);
            }
            else
            {
                existedNote = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n =>
               n.Id == model.Id && n.UserId == user.Id && n.ContributionId == model.ContributionId && n.ClassId == model.ClassId && n.SubClassId == model.SubClassId);
            }

            if (existedNote == null || existedNote.Id != model.Id)
            {
                return OperationResult.Failure("Unable to update the note. Note was not found");
            }

            var note = _mapper.Map<Note>(model);
            note.UserId = existedNote.UserId;
            await _unitOfWork.GetRepositoryAsync<Note>().Update(note.Id, note);
            return OperationResult.Success(null, _mapper.Map<NoteViewModel>(note));
        }

        public async Task<OperationResult> Delete(string accountId, string id)
        {
            var note = await _unitOfWork.GetRepositoryAsync<Note>().GetOne(n => n.Id == id);
            if (note == null)
            {
                return OperationResult.Failure("Unable to delete the note. Note was not found");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == note.UserId);

            if (user == null || user.AccountId != accountId)
            {
                return OperationResult.Failure("Unable to delete the note. Note was not found");
            }

            await _unitOfWork.GetRepositoryAsync<Note>().Delete(note.Id);
            return OperationResult.Success(null);
        }

        public async Task<OperationResult> Delete(string accountId, string contributionId, string classId, string subclassId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            Note note = new Note();
            if (user == null)
            {
                return OperationResult.Failure("Unable to delete the note. User was not found");
            }

            if (string.IsNullOrEmpty(subclassId))
            {
                note = await _unitOfWork.GetRepositoryAsync<Note>()
                .GetOne(n => n.ContributionId == contributionId && n.ClassId == classId && n.UserId == user.Id);
            }
            else
            {
                note = await _unitOfWork.GetRepositoryAsync<Note>()
                .GetOne(n => n.ContributionId == contributionId && n.ClassId == classId && n.UserId == user.Id && n.SubClassId == subclassId);
            }

            if (note == null)
            {
                return OperationResult.Failure("Unable to delete the note. Note was not found");
            }

            await _unitOfWork.GetRepositoryAsync<Note>().Delete(note.Id);
            return OperationResult.Success(null);
        }

        private static bool IsCohealer(BaseEntity user, ContributionBase contributionBase)
        {
            return user.Id == contributionBase.UserId ||
                   contributionBase.Partners.Any(x => x.IsAssigned && x.UserId == user.Id);
        }
    }
}
