using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.AdminViewModels;
using Cohere.Entity.Enums.Contribution;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAccountUpdateService
    {
        Task<OperationResult> ChangeAgreementTypeAndAgreeToStripeAgreement(string email, string newCountry);

        Task<OperationResult> LinkStripePlanWithCohere(List<LinkingStripePurchasesViewModel> viewModel);
    }
}
