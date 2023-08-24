using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using System.Collections.Generic;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class MoveIncomeFromEscrowJob : IMoveIncomeFromEscrowJob
    {
        private readonly ContributionPurchaseService _contributionPurchaseService;

        public MoveIncomeFromEscrowJob(ContributionPurchaseService contributionPurchaseService)
        {
            _contributionPurchaseService = contributionPurchaseService;
        }

        public void Execute(params object[] args)
        {
            var contributionId = args[0] as string;
            var classId = args[1] as string;
            var classParticipantsIds = args[2] as List<string>;

            _contributionPurchaseService.MoveParticipantsPaymentsFromEscrowAsync(
                contributionId, classId, classParticipantsIds).GetAwaiter().GetResult();
        }
    }
}
