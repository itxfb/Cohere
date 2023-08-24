using System;
using System.Collections.Generic;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities.Payment;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Cohere.Domain.Service
{
    public class StripeEventHandler
    {
        public static object lockedObject = new object();
        public const string ConnectWebhookEndpointSecret = nameof(ConnectWebhookEndpointSecret);
        public const string AccountWebhookEndpointSecret = nameof(AccountWebhookEndpointSecret);

        private readonly string _connectEndpointSecret;
        private readonly string _accountEndpointSecret;

        //For Connected Standard Account 
        public const string StripeConnectedAccountSecret = nameof(StripeConnectedAccountSecret);
        public const string StripeConnectedConnectSecret = nameof(StripeConnectedConnectSecret);
        private readonly string _connectedAccountEndpointSecret;
        private readonly string _connectedConnectEndpointSecret;

        private readonly IUnitOfWork _unitOfWork;
        private readonly ContributionPurchaseService _contributionPurchaseService;
        private readonly IInvoicePaymentFailedEventService _invoicePaymentFailedEventService;
        private readonly IInvoicePaidEventService _invoicePaidEventService;
        private readonly StripeAccountService _stripeAccountService;
        private readonly ILogger<StripeEventHandler> _logger;
        private readonly IStripeService _stripeService;

        private static ICollection<string> _skippedErrors = new List<string>
        {
            Constants.Contribution.Payment.StripeWebhookErrors.ContributionNotFound,
            Constants.Contribution.Payment.StripeWebhookErrors.UserNotFound
        };

        public StripeEventHandler(
            Func<string, string> secretsResolver,
            IUnitOfWork unitOfWork,
            ContributionPurchaseService contributionPurchaseService,
            StripeAccountService stripeAccountService,
            ILogger<StripeEventHandler> logger,
            IInvoicePaymentFailedEventService invoicePaymentFailedEventService,
            IInvoicePaidEventService invoicePaidEventService,
            IStripeService stripeService)
        {
            _connectEndpointSecret = secretsResolver.Invoke(ConnectWebhookEndpointSecret);
            _accountEndpointSecret = secretsResolver.Invoke(AccountWebhookEndpointSecret);
            _connectedAccountEndpointSecret = secretsResolver.Invoke(StripeConnectedAccountSecret);
            _connectedConnectEndpointSecret = secretsResolver.Invoke(StripeConnectedConnectSecret);
            _unitOfWork = unitOfWork;
            _contributionPurchaseService = contributionPurchaseService;
            _stripeAccountService = stripeAccountService;
            _logger = logger;
            _invoicePaymentFailedEventService = invoicePaymentFailedEventService;
            _invoicePaidEventService = invoicePaidEventService;
            _stripeService = stripeService;
        }

        public OperationResult HandleAccountEvent(string eventJson, string stripeSignatureHeader)
        {
            Event stripeEvent = null;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, _accountEndpointSecret);
                 //stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, "whsec_RY9ggOXirxRMG3TfboMDbiNXH1wFsq8e");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $" {ex.Message} in HandleConnectEvent at {DateTime.UtcNow}.");
                return OperationResult.Failure(ex.Message);
            }

            return ProcessStripeEvent(stripeEvent);
        }

        public OperationResult HandleStandardAccountEvent(string eventJson, string stripeSignatureHeader)
        {
            Event stripeEvent = null;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, _connectedAccountEndpointSecret);
                //stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, "whsec_2y6jni8TwO8LAmqIJJcV2C4WnAWUJJhA");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $" {ex.Message} in HandleConnectEvent at {DateTime.UtcNow}.");
                return OperationResult.Failure(ex.Message);
            }
            return ProcessStripeEvent(stripeEvent, forStandardAccount: true);
        }

        public OperationResult HandleConnectEvent(string eventJson, string stripeSignatureHeader)
        {
            Event stripeEvent = null;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, _connectEndpointSecret);
                 // stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, "whsec_2y6jni8TwO8LAmqIJJcV2C4WnAWUJJhA");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $" {ex.Message} in HandleConnectEvent at {DateTime.UtcNow}.");
                return OperationResult.Failure(ex.Message);
            }

            return ProcessStripeEvent(stripeEvent);
        }

        public OperationResult HandleStandardConnectEvent(string eventJson, string stripeSignatureHeader)
        {
            Event stripeEvent = null;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, _connectedConnectEndpointSecret);
                // stripeEvent = EventUtility.ConstructEvent(eventJson, stripeSignatureHeader, "whsec_2y6jni8TwO8LAmqIJJcV2C4WnAWUJJhA");
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $" {ex.Message} in HandleConnectEvent at {DateTime.UtcNow}.");
                return OperationResult.Failure(ex.Message);
            }
            return ProcessStripeEvent(stripeEvent, forStandardAccount: true);
        }

        private OperationResult ProcessStripeEvent(Event @event, bool forStandardAccount = false)
        {
            if (MachineLock.IsLocked(@event.Id))
            {
                //Means that there is Thread that is processing the same Event at the moment
                _logger.Log(LogLevel.Error, $"Event with ID: '{@event.Id}' is getting processed at the moment");
                return OperationResult.Failure($"Event with ID: '{@event.Id}' is getting processed at the moment");
            }

            try
            {
                //TODO: use named SemaphoreSlim and make it async.
                //TODO: (unfortunately Linux named SemaphoreSlim is process-wide and not allow us to have more then one instance)
                using (MachineLock.Create(@event.Id, TimeSpan.FromMilliseconds(1)))
                {
                    //lock(lockedObject) 
                    {
                    StripeEvent stripeEvent = null;

                    try
                    {
                        stripeEvent = _unitOfWork.GetRepositoryAsync<StripeEvent>()
                            .GetOne(x => x.StripeEventId == @event.Id).GetAwaiter().GetResult();

                        if (stripeEvent != null && stripeEvent.IsProcessed)
                        {
                            return OperationResult.Success($"Event with ID: '{@event.Id}' has been already processed");
                        }

                        OperationResult result;
                        switch (@event.Type)
                        {
                            case Events.CheckoutSessionCompleted:
                                result = _contributionPurchaseService.HandleCheckoutSessionCompletedEvent(@event, forStandardAccount);
                                break;
                            case Events.InvoicePaid:
                                result = _invoicePaidEventService.HandleInvoicePaidEvent(@event, forStandardAccount); //By Uzair
                                break;
                            case Events.InvoicePaymentFailed:
                                result = _invoicePaymentFailedEventService.HandleInvoiceFailedStripeEvent(@event);
                                break;
                            case Events.PaymentIntentCreated:
                            case Events.PaymentIntentProcessing:
                            case Events.PaymentIntentSucceeded:
                            case Events.PaymentIntentPaymentFailed:
                            case Events.PaymentIntentCanceled:
                                    {

                                result = _contributionPurchaseService.HandlePaymentIntentStripeEvent(@event, forStandardAccount); //By Uzair
                                break;
                                    }
                            case Events.AccountUpdated: //TODO: does accountUpdated event occurred for customerAccount
                                result = _stripeAccountService.HandleAccountUpdatedStripeEvent(@event);
                                break;
                            case Events.CustomerSubscriptionDeleted:
                                result = _stripeService.RevokeContributionAccessOnSubscriptionCancel(@event).GetAwaiter().GetResult();
                                break;
                            default:
                            result = OperationResult.Failure($"Event type '{@event.Type}' is not supported");
                            break;
                        }

                        //TODO: Added temporary for ignoring of handling of events generated by another test environments
                        if (!result.Succeeded && _skippedErrors.Contains(result.Message))
                        {
                            return OperationResult.Success(result.Message);
                        }


                        stripeEvent ??= new StripeEvent { StripeEventId = @event.Id };
                        stripeEvent.IsProcessed = result.Succeeded;

                        if (stripeEvent.Id != null)
                        {
                            _unitOfWork.GetRepositoryAsync<StripeEvent>().Update(stripeEvent.Id, stripeEvent)
                                .GetAwaiter().GetResult();
                        }
                        else
                        {
                            stripeEvent = _unitOfWork.GetRepositoryAsync<StripeEvent>().Insert(stripeEvent).GetAwaiter()
                                .GetResult();
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            $"{nameof(StripeEventHandler)} Method {nameof(ProcessStripeEvent)} Exception occurred. Message:{ex.Message}, /r/n Stack Trace: {ex.StackTrace}, /r/n InnerException {ex.InnerException}");

                        if (stripeEvent?.Id != null)
                        {
                            stripeEvent.IsProcessed = false;
                            stripeEvent.LastErrorMessage =
                                $"Exception message:{ex.Message} STACK TRACE: {ex.StackTrace}";
                            _unitOfWork.GetRepositoryAsync<StripeEvent>().Update(stripeEvent.Id, stripeEvent)
                                .GetAwaiter().GetResult();
                        }
                        else
                        {
                            stripeEvent = new StripeEvent
                            {
                                StripeEventId = @event.Id,
                                IsProcessed = false,
                                LastErrorMessage = $"Exception message:{ex.Message} STACK TRACE: {ex.StackTrace}"
                            };
                            _unitOfWork.GetRepositoryAsync<StripeEvent>().Insert(stripeEvent).GetAwaiter().GetResult();
                        }

                        return OperationResult.Failure(
                            $"An error occured during processing of the Event with ID: '{@event.Id}'");
                    }
                }
            }
            }
            catch (MachineLockTimeoutException)
            {
                //Ignoring
            }

            return OperationResult.Failure($"Event with ID: '{@event.Id}' is getting processed at the moment");
        }
    }
}