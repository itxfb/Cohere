using AutoMapper;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums.Payments;
using System.Collections.Concurrent;
using AngleSharp.Common;

namespace Cohere.Domain.Service
{
    public class CohealerIncomeService : ICohealerIncomeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServiceAsync<AccountViewModel, Account> _accountService;
        private readonly ICouponService _couponService;
        private readonly IMapper _mapper;
        private readonly ICommonService _commonService;
        public CohealerIncomeService(
            IUnitOfWork unitOfWork,
            IServiceAsync<AccountViewModel, Account> _accountService,
            ICouponService couponService,
            IMapper mapper,
            ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            this._accountService = _accountService;
            _couponService = couponService;
            _mapper = mapper;
            _commonService = commonService;
        }
        static object incomeCalculationlock = new object();

        public async Task<IEnumerable<PurchaseIncomeViewModel>> GetDashboardIncomeAsync(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (user == null)
            {
                return null;
            }

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributorId == user.Id);
            var contributions = _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Get(c => c.UserId == user.Id).Result;

            Dictionary<PurchaseViewModel, PurchaseIncomeViewModel> purchaseIncomeList = new Dictionary<PurchaseViewModel, PurchaseIncomeViewModel>();
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();



            var tasks = new List<Task>();

            List<List<PurchaseViewModel>> PurchaseVmChunks = new List<List<PurchaseViewModel>>();

            var totalpurchases = purchaseVms.Count();
            var NoOfTasks = totalpurchases > 200 ? 20 : 10;
            var sizeOfChunk = (int)Math.Ceiling(totalpurchases / (double)NoOfTasks);
            var skipCount = 0;
            while (skipCount < totalpurchases)
            {
                PurchaseVmChunks.Add(purchaseVms.Skip(skipCount).Take(sizeOfChunk).ToList());
                skipCount += sizeOfChunk;
            }

            
            foreach (var purchaseVmChunk in PurchaseVmChunks)
            {
               tasks.Add(new Task(() =>
                {
                    foreach (var purchaseVm in purchaseVmChunk)
                    {
                        var grossIncomeAmount = 0m;
                        var netIncomeAmount = 0m;
                        var escrowIncomeAmount = 0m;
                        var grossIncomeAmountWithTaxIncluded = 0m;
                        var netIncomeAmountWithTaxIncluded = 0m;
                        var escrowIncomeAmountWithTaxIncluded = 0m;
                        var currency = "USD";

                        var contribution = contributions.Where(c => c.Id == purchaseVm.ContributionId).FirstOrDefault();//purchaseVm.GetContribution();
                        bool coachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee;
                        bool isOneToOne = contribution is ContributionOneToOne;
                        purchaseVm.FetchActualPaymentStatuses(_commonService.GetStripeStandardAccounIdFromContribution(contribution).GetAwaiter().GetResult());
                        foreach (var payment in purchaseVm.Payments.Where(p => p.PaymentStatus == PaymentStatus.Succeeded))
                        {
                            if (payment.DateTimeCharged <= new DateTime(2021, 7, 19) || coachPaysStripeFee || isOneToOne)
                            {
                                if (purchaseVm.PaymentType == PaymentTypes.Advance.ToString())
                                {
                                    grossIncomeAmountWithTaxIncluded += payment.ExchangeRate == 0 ? payment.PurchaseAmount : payment.PurchaseAmount / payment.ExchangeRate;
                                }
                                grossIncomeAmount += payment.ExchangeRate == 0 ? payment.PurchaseAmount : payment.PurchaseAmount / payment.ExchangeRate;
                            }
                            else
                            {
                                if (purchaseVm.PaymentType == PaymentTypes.Advance.ToString())
                                {
                                    grossIncomeAmountWithTaxIncluded += (payment.PurchaseAmount - payment.ProcessingFee);
                                }
                                grossIncomeAmount += (payment.PurchaseAmount - payment.ProcessingFee);
                            }
                            if (purchaseVm.PaymentType == PaymentTypes.Advance.ToString())
                            {
                                netIncomeAmountWithTaxIncluded += payment.TransferAmount;
                                escrowIncomeAmountWithTaxIncluded += payment.IsInEscrow ? payment.TransferAmount : 0;
                            }
                            netIncomeAmount += payment.ExchangeRate == 0 ? payment.TransferAmount : payment.TransferAmount / payment.ExchangeRate;
                            escrowIncomeAmount += payment.IsInEscrow ? payment.TransferAmount : 0; 
                            currency = String.IsNullOrEmpty(payment.PurchaseCurrency) ? "USD" : payment.PurchaseCurrency.ToUpper();
                        }
                        var purchaseIncome = new PurchaseIncomeViewModel
                        {
                            GrossIncomeAmount = grossIncomeAmount,
                            NetIncomeAmount = netIncomeAmount,
                            EscrowIncomeAmount = escrowIncomeAmount,
                            GrossIncomeAmountWithTaxIncluded = grossIncomeAmountWithTaxIncluded,
                            NetIncomeAmountWithTaxIncluded = netIncomeAmountWithTaxIncluded,
                            EscrowIncomeAmountWithTaxIncluded = escrowIncomeAmountWithTaxIncluded,
                            Currency = currency,
                            Symbol = contribution.DefaultSymbol == null ? "$" : contribution.DefaultSymbol
                        };

                        if (purchaseVm.ContributionType == nameof(ContributionCourse)
                        && purchaseVm.RecentPaymentOption == PaymentOptions.SplitPayments)
                        {
                            if (purchaseVm.PaymentType == PaymentTypes.Advance.ToString())
                            {
                                purchaseIncome.PendingIncomeAmountWithTaxIncluded = purchaseVm.PendingSplitPaymentAmount ?? 0;
                            }
                            purchaseIncome.PendingIncomeAmount = purchaseVm.PendingSplitPaymentAmount ?? 0;
                        }
                        if (purchaseVm.ContributionType == nameof(ContributionCourse) && contribution.IsWorkshop)
                        {
                            purchaseVm.ContributionType = "WorkShop";
                        }
                        lock (incomeCalculationlock)
                            purchaseIncomeList.Add(purchaseVm, purchaseIncome);
                        
                    }
                }));
            }

            tasks.ForEach(t => t.Start());

            Task.WaitAll(tasks.ToArray());


            List < PurchaseIncomeViewModel > list = new List<PurchaseIncomeViewModel>();
            // var userCountry = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(c => c.Id == user.CountryId);
            //var userCurrency = await _unitOfWork.GetRepositoryAsync<Currency>().GetOne(e => (e.CountryCode == userCountry.Alpha2Code));



            return purchaseIncomeList.OrderBy(x => x.Value.ContributionType)
                .GroupBy(p => p.Key.ContributionType, p => p.Value,
                    (type, incomes) =>
                    {

                        var purchasesIncomes = incomes.ToList();

                        return new PurchaseIncomeViewModel
                        {
                            ContributionType = type,
                            GrossIncomeAmount = purchasesIncomes.Sum(i => i.GrossIncomeAmount),
                            NetIncomeAmount = purchasesIncomes.Sum(i => i.NetIncomeAmount),
                            EscrowIncomeAmount = purchasesIncomes.Sum(i => i.EscrowIncomeAmount),
                            PendingIncomeAmount = purchasesIncomes.Sum(i => i.PendingIncomeAmount),
                            GrossIncomeAmountWithTaxIncluded = purchasesIncomes.Sum(i => i.GrossIncomeAmountWithTaxIncluded),
                            NetIncomeAmountWithTaxIncluded = purchasesIncomes.Sum(i => i.NetIncomeAmountWithTaxIncluded),
                            EscrowIncomeAmountWithTaxIncluded = purchasesIncomes.Sum(i => i.EscrowIncomeAmountWithTaxIncluded),
                            PurchaseIncomeList = getPurchaseList(type, purchaseIncomeList)
                            // Currency = userCurrency.Code,
                        };
                    });
        }

        public List<PurchaseIncomeViewModel> getPurchaseList(string type, Dictionary<PurchaseViewModel, PurchaseIncomeViewModel> purchaseIncomeList)
        {

            List<PurchaseIncomeViewModel> purchaseincomeList = new List<PurchaseIncomeViewModel>();

            foreach (var item in purchaseIncomeList.Select(x => x.Value.Currency).Distinct().ToList())
            {
                var symbol = purchaseIncomeList.Where(x => x.Value.Currency == item).FirstOrDefault();
                var GrossIncomeAmount = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.GrossIncomeAmount);
                var NetIncomeAmount = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.NetIncomeAmount);
                var EscrowIncomeAmount = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.EscrowIncomeAmount);
                var PendingIncomeAmount = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.PendingIncomeAmount);
                var GrossIncomeAmountWithTax = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.GrossIncomeAmountWithTaxIncluded);
                var NetIncomeAmountWithTax = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.NetIncomeAmountWithTaxIncluded);
                var EscrowIncomeAmountWithTax = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.EscrowIncomeAmountWithTaxIncluded);
                var PendingIncomeAmountWithTax = purchaseIncomeList.Where(x => x.Value.Currency == item && x.Key.ContributionType == type).Sum(x => x.Value.PendingIncomeAmountWithTaxIncluded);

                PurchaseIncomeViewModel obj = new PurchaseIncomeViewModel()
                {
                    Currency = item,
                    Symbol = symbol.Value.Symbol,
                    ContributionType = type,
                    GrossIncomeAmount = GrossIncomeAmount,
                    NetIncomeAmount = NetIncomeAmount,
                    EscrowIncomeAmount = EscrowIncomeAmount,
                    PendingIncomeAmount = PendingIncomeAmount,
                    GrossIncomeAmountWithTaxIncluded = GrossIncomeAmountWithTax,
                    NetIncomeAmountWithTaxIncluded = NetIncomeAmountWithTax,
                    EscrowIncomeAmountWithTaxIncluded = EscrowIncomeAmountWithTax,
                    PendingIncomeAmountWithTaxIncluded = PendingIncomeAmountWithTax,
                };

                purchaseincomeList.Add(obj);

            }
            return purchaseincomeList.OrderByDescending(x=>x.Currency).ToList();
        }
        public async Task<IEnumerable<ContributionSaleViewModel>> GetContributionSalesAsync(string accountId)
        {
            var contributorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(c => c.Id == accountId);
            var contributor = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(u => u.AccountId == accountId);

            bool isUserOnTrial = !contributorAccount.PaidTierOptionBannerHidden;

            if (contributor == null)
            {
                return null;
            }

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(p => p.ContributorId == contributor.Id);
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases);

            var contributionSales = new List<ContributionSaleViewModel>();
            //purchaseVms = purchaseVms.Where(x => x.ContributorId == "62bf5b0b6e28ba0ff83cfef1" && x.ClientId == "62bf113194bb7f7463b2f8ab" && x.CreateTime>= new DateTime(2022,07,25)).ToList();
            foreach (var purchase in purchaseVms)
            {
                var contribution = purchase.GetContribution();
                var clientUser = await purchase.GetClient();
                var clientAccount = clientUser is null ? null : await _accountService.GetOne(clientUser.AccountId);
                var isPaidAsEntireCourse = purchase.IsPaidAsEntireCourse;
                var isPaidAsSessionPackage = purchase.IsPaidAsSessionPackage;

                var isPaidAsMonthlySessionSubscription = purchase.IsPaidAsSessionPackage;
                var pendingPaymentsFlag = purchase.HasPendingPayments ? "yes" : "no";

                string couponName = string.Empty;
                //string couponId = purchases.FirstOrDefault(p => p.Id == purchase.Id)?.CouponId;
                //if (!string.IsNullOrEmpty(couponId))
                //{
                //    var coupon = await _couponService.GetCouponAsync(couponId);
                //    couponName = coupon?.Name;
                //}

                foreach (var payment in purchase.Payments.Where(p => p.PaymentStatus == PaymentStatus.Succeeded ||
                    // include manually added clients as well
                    p.IsTrial == true))
                {
                    // if there is a coupn on the purchasePayment level, fetch it
                    if (!string.IsNullOrEmpty(payment.CouponId))
                    {
                        var coupon = await _couponService.GetCouponAsync(payment.CouponId);
                        couponName = coupon?.Name;
                    }

                    decimal totalCost = 0m;

                    if (payment.TotalCost > 0)
                    {
                        totalCost = payment.TotalCost;
                    }
                    else
                    {
                        switch (purchase.ContributionType)
                        {
                            case nameof(ContributionCourse):
                                totalCost = CalculateTotalCostForContibutionCourse(isPaidAsEntireCourse,
                                contribution as ContributionCourse);
                                break;

                            case nameof(ContributionOneToOne):
                                totalCost = CalculateTotalCostForContributionOneToOne(isPaidAsSessionPackage,
                                contribution as ContributionOneToOne);
                                break;

                            case nameof(ContributionMembership):
                                totalCost = CalculateTotalCostForContributionMembership(payment,
                                contribution as ContributionMembership);
                                break;

                            case nameof(ContributionCommunity):
                                totalCost = CalculateTotalCostForContributionCommunity(payment,
                                contribution as ContributionCommunity);
                                break;

                            default:
                                throw new Exception("Unsupported contribution type");
                        }
                    }
                    if (payment.PaymentOption.ToString() == "MonthlySessionSubscription")
                    {
                        payment.TotalCost = (decimal)contribution.PaymentInfo.BillingPlanInfo.TotalBillingPureCost;
                        totalCost = payment.TotalCost;
                    }

                    bool purchasePaymentContainsFeeBreakdown =
                        payment.ClientFee > 0 || payment.CoachFee > 0 || payment.CohereFee > 0 || payment.ProcessingFee > 0;
                    bool coachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee;
                    var fee = purchasePaymentContainsFeeBreakdown ? payment.CohereFee :
                        Math.Max(0, payment.PurchaseAmount -
                            ((!coachPaysStripeFee && payment.PaymentOption == PaymentOptions.SessionsPackage) ? 0 : payment.ProcessingFee) -
                            payment.TransferAmount);
                    decimal clientFee;
                    if (payment.PaymentOption.ToString() == "MonthlySessionSubscription" || payment.PaymentOption.ToString() == "SessionsPackage")
                    {
                        clientFee = purchasePaymentContainsFeeBreakdown ? payment.ClientFee :
                        coachPaysStripeFee ? 0 : Math.Max(0, payment.GrossPurchaseAmount - payment.PurchaseAmount);
                    }
                    else
                    {
                        clientFee = purchasePaymentContainsFeeBreakdown ? payment.ClientFee :
                        coachPaysStripeFee ? 0 : Math.Max(0, payment.GrossPurchaseAmount - totalCost);
                    }

                    decimal coachFee = purchasePaymentContainsFeeBreakdown ? payment.CoachFee :
                        Math.Max(0, payment.PurchaseAmount - payment.TransferAmount - clientFee - fee);
                    if (payment.PaymentOption.ToString() == "MonthlySessionSubscription" && payment.TransferAmount!=payment.PurchaseAmount)
                    {
                        coachFee=payment.PurchaseAmount-payment.TransferAmount;
                    }

                        if (fee > 0 && !isUserOnTrial)
                    {
                        if(coachPaysStripeFee && coachFee == 0) coachFee = fee;
                        else if (!coachPaysStripeFee && clientFee == 0) clientFee = fee;
                    }
                    //TODO: Will remove this redundant code after confirming the issue that was happening i.e. Seeing ClientFee + Coach Fee at the same time.

                    //if (!coachPaysStripeFee)
                    //{
                    //    var percent = 2.9;//2.9%
                    //    var cents = 0.30; // $0.30 USD

                    //    if (contribution.DefaultCurrency?.ToUpper() != "USD")
                    //    {
                    //        if (payment?.ExchangeRate != 0) cents = cents / Convert.ToDouble(payment.ExchangeRate);
                    //    }

                    //    var client_fee = Math.Round(Convert.ToDecimal(((Convert.ToDouble(payment.GrossPurchaseAmount) * percent) / 100) + cents), 2);
                    //    if (clientFee > client_fee)
                    //    {
                    //        var difference = Math.Abs(clientFee - client_fee);
                    //        clientFee = Math.Abs(clientFee - difference);
                    //        clientFee += difference;
                    //    }
                    //}

                    //FIX: This will fix the issue related to rounding of cents with Client Fee + Coach fee i.e. ClientFee was comming as 1.68 whereas the actuall payment Amount was 101.67
                    if(payment.GrossPurchaseAmount > totalCost && !isUserOnTrial)
                    {
                        if(!coachPaysStripeFee && clientFee != 0 && (payment.GrossPurchaseAmount - totalCost) < clientFee)
                        {
                            clientFee = payment.GrossPurchaseAmount - totalCost;
                        }
                        else if (coachPaysStripeFee && coachFee != 0 && (payment.GrossPurchaseAmount - totalCost) < coachFee)
                        {
                            coachFee = payment.GrossPurchaseAmount - totalCost;
                        }
                        //else if(clientFee != 0 && (payment.GrossPurchaseAmount - totalCost) > clientFee)
                        //{
                        //    clientFee = payment.GrossPurchaseAmount - totalCost;
                        //}
                        //else if(coachFee != 0 && (payment.GrossPurchaseAmount - totalCost) > coachFee)
                        //{
                        //    coachFee = payment.GrossPurchaseAmount - totalCost;
                        //}
                    }

                    couponName = string.Empty;
                    //First condition is for PerSession Case -> a client can purchase different availability times for the same contribution
                    //and since couponId is saved in Purchases rather than payments object so code needs to work for both the cases i.e.when contribution is purchased with or without coupon.
                    //Second condition is for the case when 100% off coupon_code is applied 
                    if ((payment.TotalCost>0 && payment.TotalCost!=payment.TransferAmount) || (payment.TotalCost == 0 && payment.TransferAmount == 0 && payment.GrossPurchaseAmount == 0))
                    {
                        string couponId_temp = purchases.FirstOrDefault(p => p.Id == purchase.Id)?.CouponId;
                        if (!string.IsNullOrEmpty(couponId_temp))
                        {
                            var coupon = await _couponService.GetCouponAsync(couponId_temp);
                            couponName = coupon?.Name;
                        }
                    }

                    var isExchangeRateInvlove = payment.DestinationBalanceTransaction != null && 
                        payment.DestinationBalanceTransaction.ExchangeRate != null && payment.DestinationBalanceTransaction.ExchangeRate != 0;

                    ContributionSaleViewModel contributionSaleViewModel = new ContributionSaleViewModel()
                    {
                        PaymentDate = payment.DateTimeCharged.ToString("M/d/yyyy"),
                        FirstName = clientUser is null ? "deleted user" : $"{clientUser.FirstName}",
                        LastName = clientUser is null ? "deleted user" : $"{clientUser.LastName}",
                        Contact = contribution.InvitationOnly ? clientAccount?.Email ?? "deleted user" : string.Empty,
                        ContributionName = contribution.Title,
                        Currency = contribution.DefaultCurrency?.ToUpper() ?? "USD",
                        TotalCost = totalCost,
                        PaymentAmount = payment.GrossPurchaseAmount,
                        //ProcessingFee = payment.ProcessingFee,
                        CouponName = couponName,
                        Fee = isUserOnTrial ? fee : 0,
                        CoachFee = coachFee,//payment.ProcessingFee - (payment.GrossPurchaseAmount - totalCost),
                        ClientFee = clientFee,
                        ReveueEarned = isExchangeRateInvlove ? Math.Round(payment.TransferAmount / Convert.ToDecimal(payment.DestinationBalanceTransaction?.ExchangeRate), 2) : payment.TransferAmount,
                        //GrossSales = payment.GrossPurchaseAmount - fee,
                        //BankTransferDate
                        InEscrow = payment.IsInEscrow ? payment.TransferAmount : 0,
                        PendingPayments = pendingPaymentsFlag,
                        Source = "INVITE", // or marketplace
                        PaymentType = contribution.PaymentType == PaymentTypes.Advance ?  "Direct Stripe Processor" : "Cohere Payment Processor" ,
                        TaxType = contribution.PaymentType == PaymentTypes.Simple ? String.Empty : contribution.TaxType.ToString(),
                        TaxHistoryLink = contribution.PaymentType == PaymentTypes.Advance ? "https://dashboard.stripe.com/tax/reporting" : string.Empty,
                        HasAccess = _commonService.IsUserRemovedFromContribution(purchase) ? "No" : "Yes",
                        RevenueCollection = contribution.IsInvoiced ? "Paid by Invoice" : totalCost > 0 ? "Paid by Checkout" : string.Empty
                    };

                    if (payment.DestinationBalanceTransaction != null)
                    {
                        //         var _curency = await _unitOfWork.GetRepositoryAsync<Currency>()
                        //.GetOne(p => p.Code == payment.DestinationBalanceTransaction.Currency);
                        contributionSaleViewModel.ReveuePayout = Convert.ToString(Convert.ToString(payment.DestinationBalanceTransaction.Amount) + " " + payment.DestinationBalanceTransaction.Currency.ToUpper());
                    }
                    if (payment.DestinationBalanceTransaction != null && payment.DestinationBalanceTransaction?.ExchangeRate != null && contribution.DefaultCurrency?.ToUpper() == "USD" )
                    {
                        if (payment.DestinationBalanceTransaction.Net == payment.TransferAmount)
                        {
                            contributionSaleViewModel.ReveueEarned = Math.Round(payment.TransferAmount / Convert.ToDecimal(payment.DestinationBalanceTransaction?.ExchangeRate), 2);
                            if (contributionSaleViewModel.ReveueEarned <= 0)
                                contributionSaleViewModel.ReveueEarned = payment.TransferAmount;
                        }
                        else
                        {
                            payment.DestinationBalanceTransaction.Net = payment.TransferAmount;
                        }
                    }
                    if (payment.TransferAmount > 0 && payment.DestinationBalanceTransaction != null)
                    {
                        payment.DestinationBalanceTransaction.Net = payment.TransferAmount;
                        contributionSaleViewModel.ReveuePayout = Convert.ToString(Convert.ToString(payment.TransferAmount) + " " + payment.DestinationBalanceTransaction.Currency.ToUpper());
                    }

                    contributionSaleViewModel.ExhangeRate = "N/A";
                    if (contributionSaleViewModel.Currency.ToUpper() != "USD")
                    {
                        contributionSaleViewModel.ExhangeRate = payment.DestinationBalanceTransaction != null  && payment.DestinationBalanceTransaction?.ExchangeRate != null ? payment.DestinationBalanceTransaction.ExchangeRate.ToString(): "N/A";
                    }
                    else
                    {
                        contributionSaleViewModel.ExhangeRate = payment.DestinationBalanceTransaction != null && payment.DestinationBalanceTransaction?.ExchangeRate != null ? payment.DestinationBalanceTransaction.ExchangeRate.ToString() : "N/A";
                    }
                    contributionSales.Add(contributionSaleViewModel);
                }
            }

            return contributionSales.OrderBy(x => DateTime.Parse(x.PaymentDate));
        }

        public decimal CalculateTotalCostForContributionMembership(PurchasePayment payment, ContributionMembership contributionMembership)
        {
            decimal cost = 0;
            var membershipCost = contributionMembership?.PaymentInfo?.MembershipInfo?.Costs?.FirstOrDefault(c => c.Key == payment.PaymentOption);
            if (membershipCost?.Value > 0)
            {
                cost = membershipCost.Value.Value;
            }
            return cost;
        }

        public decimal CalculateTotalCostForContributionCommunity(PurchasePayment payment, ContributionCommunity contributionMembership)
        {
            decimal cost = 0;
            var membershipCost = contributionMembership?.PaymentInfo?.MembershipInfo?.Costs?.FirstOrDefault(c => c.Key == payment.PaymentOption);
            if (membershipCost?.Value > 0)
            {
                cost = membershipCost.Value.Value;
            }
            return cost;
        }

        public async Task<decimal> GetContributionRevenueAsync(string contributionId)
        {
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(p => p.ContributionId == contributionId);

            if (!purchases.Any())
            {
                return 0;
            }

            var purchaseVms = _mapper.Map<List<PurchaseViewModel>>(purchases);
            var contribution = await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().GetOne(m => m.Id == contributionId);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));

            decimal revenue;
            if (purchaseVms?.FirstOrDefault().Payments?.FirstOrDefault().ExchangeRate is not null)
            {
                revenue = purchaseVms.SelectMany(p => p.Payments)
               .Where(pt => pt.PaymentStatus == PaymentStatus.Succeeded)
               .Sum(pt => (pt.ExchangeRate == 0) ? pt.TransferAmount : pt.TransferAmount / pt.ExchangeRate);
               
            }
            else
            {
                 revenue = purchaseVms.SelectMany(p => p.Payments)
                .Where(pt => pt.PaymentStatus == PaymentStatus.Succeeded)
                .Sum(pt => pt.TransferAmount);
            }

            return revenue;
        }
        public async Task<decimal> GetSingleClientRevenueAsync(string contributionId, string clientId)
        {
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(p => p.ContributionId == contributionId && p.ClientId == clientId);

            if (!purchases.Any())
            {
                return 0;
            }

            var purchaseVms = _mapper.Map<List<PurchaseViewModel>>(purchases);
            var contribution = await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().GetOne(m => m.Id == contributionId);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));

            var revenue = purchaseVms.SelectMany(p => p.Payments)
                .Where(pt => pt.PaymentStatus == PaymentStatus.Succeeded)
                .Sum(pt => pt.TransferAmount);

            return revenue;
        }

        public async Task<PurchaseIncomeViewModel> GetTotalIncomeAsync(string accountId)
        {
            var dashboardIncome = (await GetDashboardIncomeAsync(accountId)).ToList();

            if (!dashboardIncome.Any())
            {
                return new PurchaseIncomeViewModel()
                {
                    GrossIncomeAmount = 0m,
                    NetIncomeAmount = 0m,
                    EscrowIncomeAmount = 0m,
                    PendingIncomeAmount = 0m,
                    GrossIncomeAmountWithTaxIncluded = 0m,
                    NetIncomeAmountWithTaxIncluded = 0m,
                    EscrowIncomeAmountWithTaxIncluded = 0m,
                    PendingIncomeAmountWithTaxIncluded = 0m
                };
            }
            List<PurchaseIncomeViewModel> list = new List<PurchaseIncomeViewModel>();
            foreach (var type in dashboardIncome)
            {
                foreach (var item in type.PurchaseIncomeList)
                {
                    var purchaseincome = list.Where(x => x.Currency == item.Currency).FirstOrDefault();
                    if (purchaseincome != null)
                    {
                        purchaseincome.GrossIncomeAmount += item.GrossIncomeAmount;
                        purchaseincome.NetIncomeAmount += item.NetIncomeAmount;
                        purchaseincome.EscrowIncomeAmount += item.EscrowIncomeAmount;
                        purchaseincome.PendingIncomeAmount += item.PendingIncomeAmount;
                        purchaseincome.GrossIncomeAmountWithTaxIncluded += item.GrossIncomeAmountWithTaxIncluded;
                        purchaseincome.NetIncomeAmountWithTaxIncluded += item.NetIncomeAmountWithTaxIncluded;
                        purchaseincome.EscrowIncomeAmountWithTaxIncluded += item.EscrowIncomeAmountWithTaxIncluded;
                        purchaseincome.PendingIncomeAmountWithTaxIncluded += item.PendingIncomeAmountWithTaxIncluded;

                    }
                    else
                    {
                        var obj = new PurchaseIncomeViewModel()
                        {
                            Currency = item.Currency,
                            Symbol = item.Symbol, //Symbol according to Contribution's Currency
                            GrossIncomeAmount = item.GrossIncomeAmount,
                            NetIncomeAmount = item.NetIncomeAmount,
                            EscrowIncomeAmount = item.EscrowIncomeAmount,
                            PendingIncomeAmount = item.PendingIncomeAmount,
                            GrossIncomeAmountWithTaxIncluded = item.GrossIncomeAmountWithTaxIncluded,
                            NetIncomeAmountWithTaxIncluded = item.NetIncomeAmountWithTaxIncluded,
                            EscrowIncomeAmountWithTaxIncluded = item.EscrowIncomeAmountWithTaxIncluded,
                            PendingIncomeAmountWithTaxIncluded = item.PendingIncomeAmountWithTaxIncluded
                        };
                        list.Add(obj);
                    }



                }
            }
            var totalIncome = new PurchaseIncomeViewModel()
            {
                GrossIncomeAmount = dashboardIncome.Sum(i => i.GrossIncomeAmount),
                NetIncomeAmount = dashboardIncome.Sum(i => i.NetIncomeAmount),
                EscrowIncomeAmount = dashboardIncome.Sum(i => i.EscrowIncomeAmount),
                PendingIncomeAmount = dashboardIncome.Sum(i => i.PendingIncomeAmount),
                GrossIncomeAmountWithTaxIncluded = dashboardIncome.Sum(i => i.GrossIncomeAmountWithTaxIncluded),
                NetIncomeAmountWithTaxIncluded = dashboardIncome.Sum(i => i.NetIncomeAmountWithTaxIncluded),
                EscrowIncomeAmountWithTaxIncluded = dashboardIncome.Sum(i => i.EscrowIncomeAmountWithTaxIncluded),
                PendingIncomeAmountWithTaxIncluded = dashboardIncome.Sum(i => i.PendingIncomeAmountWithTaxIncluded),
                PurchaseIncomeList = list
            };
            return totalIncome;
        }

        public decimal CalculateTotalCostForContributionOneToOne(bool isPaidAsSessionPackage, ContributionOneToOne contribution)
        {
            if (isPaidAsSessionPackage && contribution.PaymentInfo.PackageCost.HasValue)
            {
                return contribution.PaymentInfo.PackageCost.Value
                    .SubtractPercent(contribution.PaymentInfo.PackageSessionDiscountPercentage);
            }
            
            if (contribution.PaymentInfo.Cost != null)
            {
                decimal cost = 0;
                if (contribution?.PaymentInfo?.Cost > 0)
                {
                    cost = contribution.PaymentInfo.Cost.Value;
                };

                if (cost > 0 && isPaidAsSessionPackage) //when package cost is not available use per session cost 
                {
                    return (cost * (decimal)contribution.PaymentInfo.PackageSessionNumbers)
                        .SubtractPercent(contribution.PaymentInfo.PackageSessionDiscountPercentage);
                }

                return cost; // paid as per session
            }

            var monthlySessionSubscriptionCost = contribution.PaymentInfo.MonthlySessionSubscriptionInfo;

            return monthlySessionSubscriptionCost?.MonthlyPrice != null?  monthlySessionSubscriptionCost.MonthlyPrice.Value *
                   (decimal)monthlySessionSubscriptionCost.Duration : 0;
        }

        public decimal CalculateTotalCostForContibutionCourse(bool isPaidAsEntireCourse, ContributionCourse contribution)
        {
            decimal cost = 0;
            if (contribution?.PaymentInfo?.Cost > 0)
            {
                cost = contribution.PaymentInfo.Cost.Value;
            };

            if (cost > 0 && isPaidAsEntireCourse)
            {
                return cost.SubtractPercent(contribution.PaymentInfo.PackageSessionDiscountPercentage);
            }

            return cost; // paid as a subscription
        }
    }
}
