using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.AdminViewModels;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers
{
	[ApiController]
	[Authorize(Roles = "Admin, SuperAdmin")]
	[Route("[controller]")]
	public class AdminController : CohereController
	{
		private readonly IAdminService _adminService;
		private readonly IChatService _chatService;
		private readonly IContributionRootService _contributionRootService;
		private readonly IContributionService _contributionService;
        private readonly IAccountUpdateService _accountUpdateService;
        private readonly IStripeService _stripeService;
		private readonly IUnitOfWork _unitOfWork;
        private readonly IUserService<UserViewModel, User> _userService;

        public AdminController(IAdminService adminService, IChatService chatService, IContributionRootService contributionRootService,
			IContributionService contributionService,
			IAccountUpdateService accountUpdateService,
			IStripeService stripeService, IUnitOfWork unitOfWork,
            IUserService<UserViewModel, User> userService)
		{
			_adminService = adminService;
			_chatService = chatService;
			_contributionRootService = contributionRootService;
			_contributionService = contributionService;
			_accountUpdateService = accountUpdateService;
            _stripeService = stripeService;
			_unitOfWork = unitOfWork;
            _userService = userService;
        }

        [HttpGet("GetKpiReport")]
		public async Task<IActionResult> GetKpiReport([FromQuery] KpiReportRequestViewModel viewModel)
		{
			var result = await _adminService.GetKpiReportAsync(viewModel);
			return result.ToActionResult();
		}

		[HttpGet("GetActiveCampaignReport")]
		public async Task<IActionResult> GetActiveCampaignReport()
		{
			var result = await _adminService.GetActiveCampaignReportAsync();
			return result.ToActionResult();
		}

		[HttpGet("GetPurchasesWithCouponCode")]
		public async Task<IActionResult> GetPurchasesWithCouponCode()
		{
			var result = await _adminService.GetPurchasesWithCouponCode();
			return result.ToActionResult();
		}

		[HttpPost("CreateChat/{contributionId}")]
		public async Task<IActionResult> CreateChat([FromRoute] string contributionId)
		{
			var contribution =
				await _contributionRootService.GetOne(contributionId);
			if (contribution != null)
			{
				var result = await _chatService.CreateChatForContribution(contribution);
				if(result.Succeeded && contribution.Chat?.Sid != result?.Payload?.Sid)
                {
					contribution.Chat = result.Payload;
					await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

				}
				return result.ToActionResult();
			}
			return null;
		}

		[HttpPost("CreateAllMissingChats/{checkRecordsThatHasChatAlreadyAsWell}")]
		public async Task<IActionResult> CreateAllMissingChats([FromRoute] bool checkRecordsThatHasChatAlreadyAsWell = false)
		{
			var contributions =
				await _contributionRootService.Get(c => c.Status == Entity.Enums.Contribution.ContributionStatuses.Approved &&
				(checkRecordsThatHasChatAlreadyAsWell || c.Chat == null));
			if (contributions?.Count() > 0)
			{
				foreach (var contribution in contributions)
				{
					GroupChat groupChat = contribution.Chat;
					if (groupChat == null)
					{
						var existingChatRsesult = await _chatService.GetExistingChatSidByUniueName(contribution);
						if (existingChatRsesult.Succeeded && !string.IsNullOrEmpty(existingChatRsesult?.Payload))
						{
							groupChat = new GroupChat
							{
								Sid = existingChatRsesult.Payload,
								FriendlyName = contribution.Title,
								PreviewImageUrl = contribution.PreviewContentUrls.FirstOrDefault()
							};
						}
						else
						{
							var result = await _chatService.CreateChatForContribution(contribution);
							if (result.Succeeded)
							{
								groupChat = result.Payload;
							}
						}
						if (!string.IsNullOrEmpty(groupChat?.Sid))
						{
							contribution.Chat = groupChat;
							await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
						}
					}

					if (!string.IsNullOrEmpty(groupChat?.Sid))
					{
						// check if we need to add users to the chat
						var participants = await _contributionService.GetParticipantsVmsAsync(contribution.Id);
						participants = participants?.Where(p => p.Id != contribution.UserId)?.ToList();
						foreach (var participant in participants)
						{
							if(!groupChat.CohealerPeerChatSids.ContainsKey(participant.Id))
                            {
								await _chatService.AddClientToContributionRelatedChat(participant.Id, contribution);
							}
						}

						// check if we need to assign partner check logic
						bool needToUpdateContr = false;
						foreach (var partner in contribution.Partners)
						{
							var partnerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == partner.UserId);
							if (partnerUser != null)
							{
								if (groupChat.PartnerChats.FirstOrDefault(p => p.PartnerUserId == partnerUser.Id) == null)
								{
									var partnerAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == partnerUser.AccountId);
									if (partnerAccount != null)
									{
										var existingChatsUserIds = groupChat.CohealerPeerChatSids.Select(x => x.Key);
										List<PartnerPeerChat> partnerChats = new List<PartnerPeerChat>();
										foreach (var clientUserId in existingChatsUserIds)
										{
											var peerChatResult = await _chatService.CreatePeerChat(partnerAccount.Id, clientUserId);
											if (peerChatResult.Succeeded)
											{
												var peerChat = peerChatResult.Payload as PeerChat;
												partnerChats.Add(new PartnerPeerChat
												{
													UserId = clientUserId,
													ChatSid = peerChat.Sid
												});
											}
										}
										needToUpdateContr = true;
										groupChat.PartnerChats.Add(new PartnerChats
										{
											PartnerUserId = partnerUser.Id,
											PeerChats = partnerChats
										});
									}
								}
							}
						}

						if (needToUpdateContr)
						{
							contribution.Chat = groupChat;
							await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
						}
                        }

                    }
                return Ok();
			}
			return null;
		}

		[HttpPost("AgreeToStripeAgreement/{stripeConnectedAccountId}/{ipAddress}")]
		public async Task<IActionResult> AgreeToStripeAgreement([FromRoute] string stripeConnectedAccountId, [FromRoute] string ipAddress)
		{
			var result = await _stripeService.AgreeToStripeAgreement(stripeConnectedAccountId, ipAddress);
			return result.ToActionResult();
		}
		[HttpPost("ChangeAgreementTypeAndAgreeToStripeAgreement/{email}/{country}")]
		public async Task<IActionResult> ChangeAgreementTypeAndAgreeToStripeAgreement([FromRoute] string email, [FromRoute]string country)
		{
			var result = await _accountUpdateService.ChangeAgreementTypeAndAgreeToStripeAgreement(email, country);
			return result.ToActionResult();
		}
		[HttpPost("UpdateAllClientPurchasesWithStripeData/{previewOnly}")]
		public async Task<IActionResult> UpdateAllClientPurchasesWithStripeData([FromRoute] bool previewOnly = true)
		{
			var result = await _adminService.UpdateAllClientPurchasesWithStripeData(previewOnly);
			return result.ToActionResult();
		}

		//linking accounts with stripe
		[HttpPost("LinkStripePlanWithCohere")]
		public async Task<IActionResult> LinkStripePlanWithCohere(List<LinkingStripePurchasesViewModel> listof_viewModel)
		{				
			var result = await _accountUpdateService.LinkStripePlanWithCohere(listof_viewModel);
			return result.ToActionResult();
		}
        [HttpPost("EnableCustomEmailNotification")]
        public async Task<IActionResult> EnableCustomEmailNotification([FromRoute] string accountId, [FromRoute] bool enableEmailNotification)
        {
            var result = await _userService.EnableCustomEmailNotification(accountId, enableEmailNotification);
            return result.ToActionResult();
        }
    }
}
