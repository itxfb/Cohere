using Cohere.Domain.Utils;
using Cohere.Entity.Entities.Contrib;
using System.Collections.Generic;

namespace Cohere.Domain.Models.Payment
{
    public class PurchaseIncomeViewModel
    {
        private string _contributionType;

        public string ContributionType
        {
            get => _contributionType;
            set
            {
                switch (value)
                {
                    case nameof(ContributionOneToOne):
                        _contributionType = Constants.Contribution.Dashboard.SalesRepresentation.OneToOne;
                        break;
                    case nameof(ContributionCourse):
                        _contributionType = Constants.Contribution.Dashboard.SalesRepresentation.LiveCourse;
                        break;
                    case nameof(ContributionMembership):
                        _contributionType = Constants.Contribution.Dashboard.SalesRepresentation.Membership;
                        break;
                    case nameof(ContributionCommunity):
                        _contributionType = Constants.Contribution.Dashboard.SalesRepresentation.Community;
                        break;
                    default:
                        _contributionType = value;
                        break;
                }
            }
        }

        public decimal GrossIncomeAmount { get; set; }

        public decimal NetIncomeAmount { get; set; }

        public decimal EscrowIncomeAmount { get; set; }

        public decimal PendingIncomeAmount { get; set; }

        public string Currency { get; set; }

        public string Symbol { get; set; }
        public decimal GrossIncomeAmountWithTaxIncluded { get; set; }
        public decimal NetIncomeAmountWithTaxIncluded { get; set; }
        public decimal EscrowIncomeAmountWithTaxIncluded { get; set; }
        public decimal PendingIncomeAmountWithTaxIncluded { get; set; }
        public List<PurchaseIncomeViewModel> PurchaseIncomeList { get; set; }
    }

}