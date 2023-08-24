using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.SimpleEmail;
using Amazon.SQS;
using AutoMapper;
using Cohere.Api.Auth;
using Cohere.Api.Filters;
using Cohere.Api.Settings;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Abstractions;
using Cohere.Domain.Mapping;
using Cohere.Domain.Models;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.Affiliate;
using Cohere.Domain.Models.Content;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Note;
using Cohere.Domain.Models.PartnerCoach;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Models.TimeZone;
using Cohere.Domain.Models.User;
using Cohere.Domain.Models.Video;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Service.Abstractions.Community;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Service.BackgroundExecution;
using Cohere.Domain.Service.FCM;
using Cohere.Domain.Service.Generic;
using Cohere.Domain.Service.Implementation;
using Cohere.Domain.Service.Implementation.Community;
using Cohere.Domain.Service.Nylas;
using Cohere.Domain.Service.Workers;
using Cohere.Domain.Utils;
using Cohere.Domain.Utils.Validators;
using Cohere.Domain.Utils.Validators.Account;
using Cohere.Domain.Utils.Validators.Affiliate;
using Cohere.Domain.Utils.Validators.Content;
using Cohere.Domain.Utils.Validators.Contribution;
using Cohere.Domain.Utils.Validators.EmailMessages;
using Cohere.Domain.Utils.Validators.Note;
using Cohere.Domain.Utils.Validators.PartnerCoach;
using Cohere.Domain.Utils.Validators.Payment;
using Cohere.Domain.Utils.Validators.TimeZone;
using Cohere.Domain.Utils.Validators.User;
using Cohere.Domain.Utils.Validators.Video;
using Cohere.Entity.Entities;
using Cohere.Entity.Infrastructure;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using FluentValidation;
using Hangfire;
using Hangfire.Mongo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ResourceLibrary;
using RestSharp;
using Serilog;
using Stripe;
using Stripe.Checkout;
using Account = Cohere.Entity.Entities.Account;
using CouponService = Cohere.Domain.Service.Implementation.CouponService;
using S3Settings = Cohere.Entity.Infrastructure.Options.S3Settings;
using StoragePathTemplatesSettings = Cohere.Entity.Infrastructure.Options.StoragePathTemplatesSettings;

namespace Cohere.Api
{
    public class Startup
    {
        public static IConfiguration Configuration { get; set; }

        public IWebHostEnvironment HostingEnvironment { get; private set; }

        private static readonly Dictionary<string, object> _namedDependencies = new Dictionary<string, object>();

        const long MaxFileLimit = 5_368_709_120; //5 Gb
        public static string hangFirePrefix = "Hangfire";

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            HostingEnvironment = env;
        }

        // added when upgraded.net core 3.1 to.net 6		
        // Todo: remove after fixing VideoRetrievingService && DownloadFilesFromZoomService
        [Obsolete]
        public void ConfigureServices(IServiceCollection services)
        {
            Log.Information("Startup::ConfigureServices");

            try
            {
                ConfigureLocalization(services);

                services.AddControllers(
                    opt =>
                    {
                        //Custom filters can be added here
                        //opt.Filters.Add(typeof(CustomFilterAttribute));
                        //opt.Filters.Add(new ProducesAttribute("application/json"));
                    });

                #region "API versioning"

                // API versioning service
                services.AddApiVersioning(
                    o =>
                    {
                        //o.Conventions.Controller<UserController>().HasApiVersion(1, 0);
                        o.AssumeDefaultVersionWhenUnspecified = true;
                        o.ReportApiVersions = true;
                        o.DefaultApiVersion = new ApiVersion(1, 0);
                        o.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });

                // format code as "'v'major[.minor][-status]"
                services.AddVersionedApiExplorer(
                    options =>
                    {
                        options.GroupNameFormat = "'v'VVV";

                        //versioning by url segment
                        options.SubstituteApiVersionInUrl = true;
                    });
                #endregion

                // Register Mongo serialization maps
                MongoMapRegistrator.RegisterMaps();

                ConfigureOptions(services);
                ConfigureAuthentication(services);

                #region Authorization
                services.AddAuthorization(
                    options =>
                    {
                        options.AddPolicy(
                            "IsOwnerOrAdmin",
                            policy => policy.Requirements.Add(new IsOwnerOrAdminAuthorizationRequirement()));
                        options.AddPolicy(
                            "IsPaidTierPolicy",
                            policy => policy.Requirements.Add(new IsPaidTierAuthorizationRequirement()));
                        options.AddPolicy(
                            "IsScalePaidTierPolicy",
                            policy => policy.Requirements.Add(new IsScalePaidTierAuthorizationRequirement()));
                    });

                services.AddTransient<IAuthorizationHandler, IsOwnerOrAdminAuthorizationHandler>();
                services.AddTransient<IAuthorizationHandler, IsPaidTierAuthorizationHandler>();
                services.AddTransient<IAuthorizationHandler, IsScalePaidTierAuthorizationHandler>();
                #endregion

                #region "CORS"

                // include support for CORS
                // More often than not, we will want to specify that our API accepts requests coming from other origins (other domains). When issuing AJAX requests, browsers make preflights to check if a server accepts requests from the domain hosting the web app. If the response for these preflights don't contain at least the Access-Control-Allow-Origin header specifying that accepts requests from the original domain, browsers won't proceed with the real requests (to improve security).
                services.AddCors(options =>
                {
                    options.AddPolicy(
                        "CorsPolicy-public",
                        builder => builder
                            .AllowAnyOrigin() //WithOrigins and define a specific origin to be allowed (e.g. https://mydomain.com)
                            .AllowAnyMethod()
                            .AllowAnyHeader().Build());
                });
                #endregion

                // mvc service
                services.AddMvc(option =>
                {
                    option.EnableEndpointRouting = false;
                    option.AllowEmptyInputInBodyModelBinding = true;
                });
                services.AddMemoryCache();

                // utils
                ConfigureValidators(services);
                services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddSingleton<ITokenGenerator, TokenGenerator>();

                services.AddSingleton<IUnitOfWork, MongoUnitOfWork>();
                services.AddSingleton<IFCMService, FCMService>();
                services.AddSingleton<IProfilePageService, ProfilePageService>();

                services.AddSingleton<ISynchronizePurchaseUpdateService, SynchronizePurchaseUpdateService>();
                services.AddSingleton<IContributionBookingService, ContributionBookingService>();

                // AWS reelated injections
                services.AddSingleton<IAmazonSimpleEmailService, AmazonSimpleEmailServiceClient>(sp =>
                {
                    var creds = sp.GetService<AWSOptions>().Credentials.GetCredentials();
                    var sesOpts = sp.GetService<IOptions<SesSettings>>();
                    var region = RegionEndpoint.GetBySystemName(sesOpts.Value.RegionName);
                    return new AmazonSimpleEmailServiceClient(creds.AccessKey, creds.SecretKey, region);
                });

                services.AddSingleton<IAmazonSQS>(sp =>
                {
                    var creds = sp.GetService<AWSOptions>().Credentials.GetCredentials();
                    var sqsSettings = sp.GetRequiredService<IOptions<SqsSettings>>().Value;
                    return new AmazonSQSClient(creds.AccessKey, creds.SecretKey, RegionEndpoint.GetBySystemName(sqsSettings.RegionName));
                });

                services.AddSingleton<IAmazonS3>(sp =>
                {
                    var creds = sp.GetService<AWSOptions>().Credentials.GetCredentials();
                    var s3Settings = sp.GetRequiredService<IOptions<S3Settings>>().Value;
                    return new AmazonS3Client(creds.AccessKey, creds.SecretKey, RegionEndpoint.GetBySystemName(s3Settings.RegionName));
                });

                //services injections
                services.AddTransient(typeof(IServiceAsync<,>), typeof(GenericServiceAsync<,>));
                services.AddTransient(typeof(IUserService<,>), typeof(UserService<,>));
                services.AddTransient(typeof(IAccountService<,>), typeof(AccountService<,>));

                services.AddScoped<IContributionService, ContributionService>();
                services.AddScoped<IZoomService, ZoomService>();
                services.AddScoped<IAuthService, AuthService>();
                services.AddScoped<IAccountManager, AccountManager>();
                services.AddTransient<IFileStorageManager, FileStorageManager>(s =>
                {
                    var s3Settings = s.GetService<IOptions<S3Settings>>().Value;
                    return new FileStorageManager(
                        s.GetService<IAmazonS3>(),
                        s.GetService<IStringLocalizer<SharedResource>>(),
                        s3Settings.PublicBucketName,
                        s3Settings.NonPublicBucketName);
                });
                services.AddScoped<IContentService, ContentService>();

                services.AddSingleton<IEmailService, EmailService>(sp =>
                    new EmailService(
                        sp.GetService<IOptions<SesSettings>>().Value.SourceAddress,
                        sp.GetService<ILogger<EmailService>>(),
                        sp.GetService<IAmazonSimpleEmailService>()));
                services.AddScoped<IServiceAsync<AccountViewModel, Account>, AccountService<AccountViewModel, Account>>();
                services.AddScoped<IChatService, ChatService>();
                services.AddSingleton<ICalendarSyncService, CalendarSyncService>();
                services.AddScoped<IChatManager, ChatManager>(s =>
                {
                    var encryptionsettings = s.GetService<IOptions<SecretsSettings>>().Value;
                    var twilioSettings = s.GetService<IOptions<TwilioSettings>>().Value;

                    return new ChatManager(
                        s.GetService<ILogger<ChatManager>>(),
                        twilioSettings.TwilioAccountSid,
                        twilioSettings.TwilioApiSid,
                        encryptionsettings.TwilioApiSecret,
                        encryptionsettings.TwilioAccountAuthToken,
                        twilioSettings.ChatServiceSid,
                        twilioSettings.ChatUserRoleSid,
                        twilioSettings.ChatTokenLifetimeSec);
                });
                services.AddTransient<IRecordingService, RecordingService>();

                services.AddScoped<IVideoService, VideoService>(s =>
                {
                    SecretsSettings encryptionsettings = s.GetService<IOptions<SecretsSettings>>().Value;
                    TwilioSettings twilioSettings = s.GetService<IOptions<TwilioSettings>>().Value;
                    SqsSettings sqsSettings = s.GetService<IOptions<SqsSettings>>().Value;
                    AwsSettings awsSettings = s.GetService<IOptions<AwsSettings>>().Value;
                    var s3Settings = s.GetService<IOptions<S3Settings>>().Value;

                    return new VideoService(
                        s.GetService<IUnitOfWork>(),
                        s.GetService<IMapper>(),
                        s.GetService<IContributionService>(),
                        s.GetService<IContributionRootService>(),
                        s.GetService<ILogger<VideoService>>(),
                        s.GetService<IAccountManager>(),
                        s.GetService<IAmazonS3>(),
                        s3Settings.NonPublicBucketName,
                        s.GetService<IAmazonSQS>(),
                        twilioSettings.TwilioAccountSid,
                        twilioSettings.TwilioApiSid,
                        encryptionsettings.TwilioApiSecret,
                        encryptionsettings.TwilioAccountAuthToken,
                        twilioSettings.VideoTokenLifetimeSec,
                        twilioSettings.VideoWebHookUrl,
                        twilioSettings.ContributionWebHookUrl,
                        sqsSettings.VideoRetrievalQueueUrl,
                        awsSettings.DistributionName,
                         s.GetService<ICommonService>());
                });

                services.AddTransient<StripeAccountService>();
                services.AddScoped<IStripeService, StripeService>();
                services.AddScoped<ContributionPurchaseService>();
                services.AddScoped<IPayoutService, StripePayoutService>();
                services.AddScoped<IStripeEventService, StripeEventService>();
                services.AddScoped<IPaymentSystemFeeService, StripePaymentSystemFeeService>();
                services.AddScoped<IPricingCalculationService, PricingCalculationService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<ICommonService, CommonService>();//Common service
                services.AddScoped<
                    IPaidTiersService<PaidTierOptionViewModel, PaidTierOption>,
                    PaidTiersService<PaidTierOptionViewModel, PaidTierOption>>();
                services.AddScoped<StripeEventHandler>();
                services.AddScoped<ICohealerIncomeService, CohealerIncomeService>();
                services.AddScoped<INoteService, NoteService>();
                services.AddScoped<ValidateTwilioRequestAttribute>();
                services.AddSingleton<IContributionRootService, ContributionRootService>();
                services.AddTransient<IAffiliateService, AffiliateService>();
                services.AddTransient<IAffiliateCommissionService, AffiliateCommissionService>();
                services.AddTransient<IPaidTierPurchaseService, PaidTierPurchaseService>();
                services.AddTransient<IInvoicePaidEventService, InvoicePaidEventService>();
                services.AddTransient<IInvoicePaymentFailedEventService, InvoicePaymentFailedEventEventService>();
                services.AddTransient<IAcademyService, AcademyService>();
                services.AddTransient<IAdminService, AdminService>();
                services.AddTransient<IContributionStatusService, ContributionStatusService>();
                services.AddTransient<IRoleSwitchingService, RoleSwitchingService>();
                services.AddTransient<IContributionAccessService, ContributionAccessService>();
                services.AddTransient<IPodService, PodService>();
                services.AddTransient<ITestimonialService, TestimonialService>();
                services.AddTransient<IAccountUpdateService, AccountUpdateService>();

                services.AddTransient<IPostService, PostService>();
                services.AddTransient<ICommentService, CommentService>();
                services.AddTransient<ILikeService, LikeService>();
                services.AddTransient<IBookIfSingleSessionTimeJob, BookIfSingleSessionTimeJob>();
                services.AddTransient<ISharedRecordingService, SharedRecordingService>();
                //AC
                services.AddTransient<IRestClient, RestClient>();
                services.AddTransient<IActiveCampaignClient, ActiveCampaignClient>();
                services.AddTransient<IActiveCampaignService, ActiveCampaignService>();
                services.AddHostedService(sp =>
                {
                    var sqsSettings = sp.GetService<IOptions<SqsSettings>>().Value;

                    return new ActiveCampaignHandler(
                        sp.GetService<ILogger<ActiveCampaignHandler>>(),
                        sp.GetService<IAmazonSQS>(),
                        sqsSettings.ActiveCampaignQueueUrl,
                        sp.GetService<IActiveCampaignService>(),
                        new GenericServiceAsync<AccountViewModel, Account>(sp.GetService<IUnitOfWork>(), sp.GetService<IMapper>()));
                });

                services.AddTransient<ICouponService, CouponService>();

                ConfigureNamedDependencies(services, HostingEnvironment);
                ConfigureServiceTypeResolvers(services);
                ConfigureStripe(services);
                ConfigurePlaid(services);
                ConfigureNylas(services);
                ConfigureSwagger(services);

                ConfigureAutoMapperServiceLocator(services);
                services.AddSingleton(provider => new MapperConfiguration(cfg =>
                {
                    cfg.ConstructServicesUsing(provider.GetService);
                    cfg.AddProfile(new MappingProfile());
                }).CreateMapper());

                ConfigureHangfire(services);

                services.AddHostedService(sp =>
                {
                    var s3Settings = sp.GetService<IOptions<S3Settings>>().Value;
                    var sqsSettings = sp.GetService<IOptions<SqsSettings>>().Value;
                    var encryptionsettings = sp.GetService<IOptions<SecretsSettings>>().Value;
                    var twilio = sp.GetService<IOptions<TwilioSettings>>().Value;

                    return new VideoRetrievingService(
                        sp.GetService<ILogger<VideoRetrievingService>>(),
                        sp.GetService<IAmazonSQS>(),
                        sqsSettings.VideoRetrievalQueueUrl,
                        sqsSettings.VideoCompletedQueueUrl,
                        sp.GetService<IAmazonS3>(),
                        s3Settings.NonPublicBucketName,
                        twilio.TwilioAccountSid,
                        encryptionsettings.TwilioAccountAuthToken);
                });

                services.AddHostedService<VideoCompletedService>();
                services.AddHostedService<DownloadFilesFromZoomService>();
                services.AddHostedService<SendHourSessionReminders>();
                services.AddHostedService<SessionReminderJob>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "error during configuring services");
                throw;
            }
        }

        // This method gets called by the runtime
        // This method can be used to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Register encryption key
            var options = app.ApplicationServices.GetService<IOptionsMonitor<SecretsSettings>>();
            Log.Debug($"Keys grabbed. PasswordEncryptionKey :{options.CurrentValue.PasswordEncryptionKey}");

            EntityHelper.EncryptionKey = options.CurrentValue.PasswordEncryptionKey;

            var jobCreator = app.ApplicationServices.GetService<IJobRecurringCreator>();
            var backgroundJobSettings = Configuration.GetSection("BackgroundJob").Get<BackgroundJobSettings>();
            jobCreator.CreateMinutelyRecurringJob<IUnreadChatJob>();
            jobCreator.CreateDailyRecurringJob<IPlanExpireAlertJob>(10, 0);

            Log.Information("Startup::Configure");

            try
            {
                if (env.EnvironmentName == "Development")
                {
                    app.UseDeveloperExceptionPage();
                    app.UseHangfireDashboard();
                }
                else
                {
                    app.UseMiddleware<ExceptionHandler>();
                }

                //app.UseHttpsRedirection(); // should be enabled if nginx/apache doesn't handles http to https
                app.UseRouting();
                app.UseCors("CorsPolicy-public");  //apply to every request
                app.UseRequestLocalization();
                app.UseAuthentication(); //needs to be up in the pipeline, before UseMvc/UseEndpoints
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());

                // Swagger API documentation
                app.UseSwagger();

                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cohere API V1");
                    c.SwaggerEndpoint("/swagger/v2/swagger.json", "Cohere API V2");
                    c.DisplayOperationId();
                    c.DisplayRequestDuration();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in configure services: {ex.Message}");
            }
        }

        private static void ConfigureLocalization(IServiceCollection services)
        {
            services.AddLocalization(o => { o.ResourcesPath = "Resources"; });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo("en-US")
                };

                options.DefaultRequestCulture = new RequestCulture("en-US");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });
        }

        private static void ConfigureOptions(IServiceCollection services)
        {
            services.AddDefaultAWSOptions(RetrieveAwsOptions());
            services.Configure<SecretsSettings>(Configuration.GetSection("Keys"));
            services.Configure<MongoSecretsSettings>(Configuration.GetSection("Keys"));
            services.Configure<JwtSettings>(Configuration.GetSection("Jwt"));
            services.Configure<MongoSettings>(Configuration.GetSection(nameof(MongoSettings)));
            services.Configure<AccountManagementSettings>(Configuration.GetSection("AccountManagement"));
            services.Configure<SesSettings>(Configuration.GetSection("AWS:SES"));
            services.Configure<S3Settings>(Configuration.GetSection("AWS:S3"));
            services.Configure<SqsSettings>(Configuration.GetSection("AWS:SQS"));
            services.Configure<TwilioSettings>(Configuration.GetSection("Twilio"));
            services.Configure<UrlPathsSettings>(Configuration.GetSection("UrlPaths"));
            services.Configure<ClientUrlsSettings>(Configuration.GetSection("ClientUrls"));
            services.Configure<StoragePathTemplatesSettings>(Configuration.GetSection("StoragePathTemplates"));
            services.Configure<DelayExecutionSettings>(Configuration.GetSection("DelayExecutionSettings"));
            services.Configure<PaymentFeeSettings>(Configuration.GetSection("Payment:Fee"));
            services.Configure<LoggingSettings>(Configuration.GetSection("LoggingSettings"));
            services.Configure<ZoomSettings>(Configuration.GetSection("Zoom"));
            services.Configure<AwsSettings>(Configuration.GetSection("AWS"));

            services.Configure<PaymentSettings>(Configuration.GetSection("Payment"));
            services.Configure<AffiliateSettings>(Configuration.GetSection("Affiliate"));
            services.Configure<FirebaseSettings>(Configuration.GetSection("Firebase"));

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = MaxFileLimit;
            });
            services.Configure<ActiveCampaignSettings>(Configuration.GetSection("ActiveCampaign"));

            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = MaxFileLimit;
            });
        }

        private static AWSOptions RetrieveAwsOptions()
        {
            var awsOpt = Configuration.GetAWSOptions();
            var credentialProfileStoreChain = new CredentialProfileStoreChain();
            var succeeded = credentialProfileStoreChain.TryGetAWSCredentials(awsOpt.Profile, out var credentials);
            if (succeeded)
            {
                awsOpt.Credentials = credentials;
                Console.WriteLine($"AWS credentials: {credentials}");
                return awsOpt;
            }
            else
            {
                var errorMessage = "Unable to find credentials for profile name specified in settings on machine";
                Console.WriteLine(errorMessage);
                throw new AmazonClientException(errorMessage);
            }
        }

        private static void ConfigureAuthentication(IServiceCollection services)
        {
            var publicRsa = RSA.Create();
            publicRsa.FromXmlString(Configuration.GetSection("Keys:JwtRsaPublicKeyXml").Value);
            var signingKey = new RsaSecurityKey(publicRsa);

            // JWT API authentication service
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(config =>
                {
                    config.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey
                    };
                });
        }

        private static void ConfigureValidators(IServiceCollection services)
        {
            services.AddSingleton(typeof(IValidator<EmailTemplatesViewModel>), typeof(EmailTemplatesValidator));
            services.AddSingleton(typeof(IValidator<ProfilePageViewModel>), typeof(ProfilePageValidator));
            services.AddSingleton(typeof(IValidator<UserViewModel>), typeof(UserValidator));
            services.AddSingleton(typeof(IValidator<AccountViewModel>), typeof(AccountValidator));
            services.AddSingleton(typeof(IValidator<LoginViewModel>), typeof(LoginValidator));
            services.AddSingleton(typeof(IValidator<ChangePasswordViewModel>), typeof(ChangePassworValidator));
            services.AddSingleton(typeof(IValidator<TokenVerificationViewModel>), typeof(TokenVerificationModelValidator));
            services.AddSingleton(typeof(IValidator<RestorePasswordViewModel>), typeof(RestorePasswordModelValidator));
            services.AddSingleton(typeof(IValidator<LocationViewModel>), typeof(LocationValidator));
            services.AddSingleton(typeof(IValidator<TimeZoneViewModel>), typeof(TimeZoneValidator));
            services.AddSingleton(typeof(IValidator<CountryViewModel>), typeof(CountryValidator));
            services.AddSingleton(typeof(IValidator<RestoreBySecurityAnswersViewModel>), typeof(RestorePasswordBySecurityAnswersModelValidator));
            services.AddSingleton<IValidator<ContributionCourseViewModel>, ContributionCourseValidator>();
            services.AddSingleton<IValidator<ContributionOneToOneViewModel>, ContributionOneToOneValidator>();
            services.AddSingleton<IValidator<ContributionMembershipViewModel>, ContributionMembershipValidator>();
            services.AddSingleton<IValidator<ContributionCommunityViewModel>, ContributionCommunityValidator>();
            services.AddSingleton<IValidator<AdminReviewNoteViewModel>, ReviewNoteValidator>();
            services.AddSingleton<IValidator<BookOneToOneTimeViewModel>, BookOneToOneTimeValidator>();
            services.AddSingleton<IValidator<BookOneToOneTimeViewModel>, BookOneToOneTimeValidator>();
            services.AddSingleton<IValidator<BookSessionTimeViewModel>, BookSessionTimeValidator>();
            services.AddSingleton<IValidator<ShareContributionEmailViewModel>, ShareContributionEmailModelValidator>();
            services.AddSingleton<IValidator<PaymentIntentCreateViewModel>, PaymentIntentCreateValidator>();
            services.AddSingleton<IValidator<ProductSubscriptionViewModel>, ProductSubscriptionValidator>();
            services.AddSingleton<IValidator<GetPlanSubscriptionViewModel>, GetPlanSubscriptionValidator>();
            services.AddSingleton<IValidator<CreateProductViewModel>, CreateProductValidator>();
            services.AddSingleton<IValidator<CreateProductPlanViewModel>, CreateProductPlanValidator>();
            services.AddSingleton<IValidator<PaymentIntentUpdateViewModel>, PaymentIntentUpdateValidator>();
            services.AddSingleton<IValidator<UpdatePaymentMethodViewModel>, UpdatePaymentMethodValidator>();
            services.AddSingleton<IValidator<UpdatePaymentMethodViewModel>, UpdatePaymentMethodValidator>();
            services.AddSingleton<IValidator<PurchaseCourseContributionViewModel>, PurchaseCourseContributionValidator>();
            services.AddSingleton<IValidator<GetVideoTokenViewModel>, GetVideoTokenModelValidator>();
            services.AddSingleton<IValidator<DeleteRoomInfoViewModel>, DeleteRoomValidator>();
            services.AddSingleton<IValidator<TwilioVideoWebHookModel>, TwilioWebHookModelValidator>();
            services.AddSingleton<IValidator<TwilioCompositionWebHookModel>, TwilioCompositionWebHookModelValidator>();
            services.AddSingleton<IValidator<GetPaidViewModel>, GetPaidValidator>();
            services.AddSingleton<IValidator<SetAsCompletedViewModel>, SetAsCompletedValidator>();
            services.AddSingleton<IValidator<SetClassAsCompletedViewModel>, SetClassCompletedValidator>();
            services.AddSingleton<IValidator<GetAttachmentViewModel>, GetAttachmentValidator>();
            services.AddSingleton<IValidator<AttachmentWithKeyViewModel>, AttachmentWithKeyValidator>();
            services.AddSingleton<IValidator<AttachmentBaseViewModel>, AttachmentBaseValidator>();
            services.AddSingleton<IValidator<PurchaseOneToOnePackageViewModel>, PurchaseOneToOnePackageValidator>();
            services.AddSingleton<IValidator<PurchaseOneToOneMonthlySessionSubscriptionViewModel>, PurchaseOneToOneMonthlySessionSubscriptionValidator>();
            services
                .AddSingleton<IValidator<PurchaseMembershipContributionViewModel>,
                    PurchaseMembershipContributionValidator>();
            services
                .AddSingleton<IValidator<PurchaseCommunityContributionViewModel>,
                    PurchaseCommunityContributionValidator>();
            services.AddSingleton<IValidator<NoteBriefViewModel>, CreateNoteValidator>();
            services.AddSingleton<IValidator<InvitePartnerCoachViewModel>, InviteParnerCoachValidator>();
            services.AddSingleton<IUnfinishedContributionValidator, UnfinishedContributionValidator>();
            services.AddSingleton<IValidator<InviteEmailsRequestModel>, InviteEmailRequestModelValidator>();
        }

        private static void ConfigureSwagger(IServiceCollection services)
        {
            // Swagger API documentation
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Cohere API", Version = "v1" });
                c.SwaggerDoc("v2", new OpenApiInfo { Title = "Cohere API", Version = "v2" });

                // In Test project find attached swagger.auth.pdf file with instructions how to run Swagger authentication
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Authorization header using the Bearer scheme",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Id = "Bearer", //The name of the previously defined security scheme.
                                Type = ReferenceType.SecurityScheme
                            }
                        }, new List<string>()
                    }
                });

                //c.DocumentFilter<api.infrastructure.filters.SwaggerSecurityRequirementsDocumentFilter>();
            });
        }

        private static void ConfigureNamedDependencies(IServiceCollection services, IWebHostEnvironment env)
        {
            var serviceProvider = services.BuildServiceProvider();
            var accountManagementSettings = serviceProvider.GetService<IOptions<AccountManagementSettings>>().Value;
            var clientUrlsSettings = serviceProvider.GetService<IOptions<ClientUrlsSettings>>().Value;
            var urlsSettings = serviceProvider.GetService<IOptions<UrlPathsSettings>>().Value;
            var sesSettings = serviceProvider.GetService<IOptions<SesSettings>>().Value;

            _namedDependencies.Add(
                NotificationService.SessionNotificationSourceAddress,
                sesSettings.SessionNotificationSourceAddress);

            _namedDependencies.Add(
                AccountManager.PasswordRestorationTokenLifetimeDays,
                accountManagementSettings.PasswordRestorationTokenLifetimeDays);

            _namedDependencies.Add(
                AccountService<AccountViewModel, Account>.VerificationTokenLifetimeDays,
                accountManagementSettings.VerificationTokenLifetimeDays);

            _namedDependencies.Add(
                NotificationService.ContributionLinkTemplate,
                clientUrlsSettings.WebAppUrl + urlsSettings.ContributionDetailsUrlPath);
            _namedDependencies.Add(NotificationService.LoginLink, clientUrlsSettings.WebAppUrl);
            _namedDependencies.Add(
                NotificationService.EmailVerificationLink,
                clientUrlsSettings.WebAppUrl + urlsSettings.EmailVerificationRedirectUrlPath);
            _namedDependencies.Add(
                NotificationService.PasswordRestorationRedirectUrl,
                clientUrlsSettings.WebAppUrl + urlsSettings.PasswordRestorationRedirectUrlPath);
            _namedDependencies.Add(
                NotificationService.UnsubscribeEmailsLink,
                clientUrlsSettings.WebAppUrl + urlsSettings.UnsubscribeEmailsUrlPath);
            _namedDependencies.Add(
                NotificationService.SignUpPath,
                clientUrlsSettings.WebAppUrl + clientUrlsSettings.SignUpPath);

            _namedDependencies.Add(
                NotificationService.AffiliateLinkTemplate,
                clientUrlsSettings.WebAppUrl + clientUrlsSettings.AffiliateLinkTemplate); // TODO: refactor this with IOptions post actions

            _namedDependencies.Add(
                StripeService.SessionBillingUrl,
                clientUrlsSettings.WebAppUrl + clientUrlsSettings.SessionBillinglUrl);

            _namedDependencies.Add(
                StripeService.CoachSessionBillingUrl,
                clientUrlsSettings.WebAppUrl + clientUrlsSettings.CoachSessionBillingUrl);

            _namedDependencies.Add(
                StripeService.ContributionViewUrl,
                clientUrlsSettings.WebAppUrl + clientUrlsSettings.ContributionView);

            _namedDependencies.Add(
                ContributionPurchaseService.PaymentSessionLifetimeSeconds,
                int.Parse(Configuration.GetSection("Payment:PaymentSessionLifetimeSeconds").Value, CultureInfo.InvariantCulture));

            _namedDependencies.Add(
                PaymentCancellationJob.RetryPolicyNumber,
                int.Parse(Configuration.GetSection("Payment:PaymentCancellationJobRetryPolicyNumber").Value, CultureInfo.InvariantCulture));
            _namedDependencies.Add(
                SubscriptionCancellationJob.RetryPolicyNumber,
                int.Parse(Configuration.GetSection("Payment:SubscriptionCancellationJobRetryPolicyNumber").Value, CultureInfo.InvariantCulture));

            _namedDependencies.Add(
                StripeEventHandler.AccountWebhookEndpointSecret,
                Configuration.GetSection($"Keys:{StripeEventHandler.AccountWebhookEndpointSecret}").Value);
            _namedDependencies.Add(
                StripeEventHandler.ConnectWebhookEndpointSecret,
                Configuration.GetSection($"Keys:{StripeEventHandler.ConnectWebhookEndpointSecret}").Value);

            //For stripe standard Account
            _namedDependencies.Add(
               StripeEventHandler.StripeConnectedAccountSecret,
               Configuration.GetSection($"keys:{StripeEventHandler.StripeConnectedAccountSecret}").Value);
            _namedDependencies.Add(
                StripeEventHandler.StripeConnectedConnectSecret,
                Configuration.GetSection($"keys:{StripeEventHandler.StripeConnectedConnectSecret}").Value);


            var webAppUrl = Configuration.GetSection("ClientUrls:WebAppUrl").Value;

            _namedDependencies.Add(
                StripeAccountService.AccountLinkFailureUrl,
                $"{webAppUrl}/{Configuration.GetSection("UrlPaths:AccountLink:Failure").Value}");
            _namedDependencies.Add(
                StripeAccountService.AccountLinkSuccessUrl,
                $"{webAppUrl}/{Configuration.GetSection("UrlPaths:AccountLink:Success").Value}");
        }

        private static void ConfigureServiceTypeResolvers(IServiceCollection services)
        {
            services.AddSingleton(GetDependencyTypeResolverByName<int>());
            services.AddSingleton(GetDependencyTypeResolverByName<string>());
            services.AddSingleton(GetDependencyTypeResolverByName<decimal>());
            services.AddSingleton(GetDependencyTypeResolverByName<double>());
        }

        private static void ConfigureAutoMapperServiceLocator(IServiceCollection services)
        {
            services.AddTransient<ContributionCourseViewModel>();
            services.AddTransient<ContributionOneToOneViewModel>();
            services.AddTransient<ContributionMembershipViewModel>();
            services.AddTransient<ContributionCommunityViewModel>();
            services.AddTransient<PurchaseViewModel>();
        }

        private static Func<string, T> GetDependencyTypeResolverByName<T>()
        {
            return dependencyName =>
            {
                if (_namedDependencies.TryGetValue(dependencyName, out var dependency))
                {
                    return (T)dependency;
                }

                throw new InvalidOperationException($"Cannot resolve dependency with the name: '{dependencyName}'");
            };
        }

        private static void ConfigureStripe(IServiceCollection services)
        {
            StripeConfiguration.ApiKey = Configuration.GetSection("Keys:StripeSecretKey").Value;

            _namedDependencies.Add(
                nameof(IStripeService.StripePublishableKey),
                Configuration.GetSection("Keys:StripePublishableKey").Value);

            services.AddScoped<CustomerService>();
            services.AddScoped<AccountService>();
            services.AddScoped<ExternalAccountService>();
            services.AddTransient<PaymentIntentService>();
            services.AddScoped<PaymentMethodService>();
            services.AddScoped<AccountLinkService>();
            services.AddScoped<EventService>();
            services.AddScoped<TransferService>();
            services.AddTransient<SubscriptionService>();
            services.AddTransient<BalanceTransactionService>();
            services.AddScoped<PlanService>();
            services.AddScoped<TokenService>();
            services.AddScoped<ProductService>();
            services.AddTransient<PriceService>();
            services.AddScoped<PlanService>();
            services.AddTransient<InvoiceService>();
            services.AddTransient<InvoiceItemService>();
            services.AddScoped<SessionService>();
            services.AddScoped<SetupIntentService>();
            services.AddScoped<SubscriptionScheduleService>();
            services.AddScoped<PayoutService>();
            services.AddScoped<BalanceService>();
            services.AddScoped<ApplicationFeeService>();
            services.AddScoped<NotificationService>();
        }

        private void ConfigurePlaid(IServiceCollection services)
        {
            var plaidUrl = Configuration.GetSection("PlaidSettings:PlaidUrl").Value;
            var plaidExchangePublicTokenUrlPath = Configuration.GetSection("PlaidSettings:PlaidExchangePublicTokenUrlPath").Value;
            var plaidFetchStripeTokenUrlPath = Configuration.GetSection("PlaidSettings:PlaidFetchStripeTokenUrlPath").Value;

            _namedDependencies.Add(PlaidService.PlaidExchangePublicTokenUrl, plaidUrl + plaidExchangePublicTokenUrlPath);
            _namedDependencies.Add(PlaidService.PlaidFetchStripeTokenUrl, plaidUrl + plaidFetchStripeTokenUrlPath);

            _namedDependencies.Add(PlaidService.PlaidPublicKey, Configuration.GetSection("Keys:PlaidPublicKey").Value);
            _namedDependencies.Add(PlaidService.PlaidClientId, Configuration.GetSection("Keys:PlaidClientId").Value);
            _namedDependencies.Add(PlaidService.PlaidSecret, Configuration.GetSection("Keys:PlaidSecret").Value);

            services.AddScoped<PlaidService>();
        }

        private static void ConfigureNylas(IServiceCollection services)
        {
            var nylasSettings = Configuration.GetSection(nameof(NylasSettings)).Get<NylasSettings>();

            _namedDependencies.Add(NylasService.ClientId, nylasSettings.ClientId);
            _namedDependencies.Add(NylasService.ClientSecret, nylasSettings.ClientSecret);
            _namedDependencies.Add(NylasService.AuthorisationUrlPath, nylasSettings.InitialUrl + nylasSettings.AuthorisationUrlPath);
            _namedDependencies.Add(NylasService.TokenUrlPath, nylasSettings.InitialUrl + nylasSettings.TokenUrlPath);
            _namedDependencies.Add(NylasService.FreeBusyUrlPath, nylasSettings.InitialUrl + nylasSettings.FreeBusyUrlPath);
            _namedDependencies.Add(NylasService.RedirectUri, nylasSettings.RedirectUri);
            _namedDependencies.Add(NylasService.Scopes, nylasSettings.Scopes);
            _namedDependencies.Add(NylasService.ResponseType, nylasSettings.ResponseType);
            _namedDependencies.Add(NylasService.GrantType, nylasSettings.GrantType);

            services.AddSingleton<NylasService>();
        }

        private void ConfigureHangfire(IServiceCollection services)
        {
            var env = HostingEnvironment.EnvironmentName;
            if (env == "Development")
            {
                hangFirePrefix = "Hangfire_Local";
            }
            else
            {
                hangFirePrefix = "Hangfire_V1";
            }
            services.AddHangfire((sp, configuration) =>
            {
                var mongoSecretsSettings = sp.GetService<IOptions<MongoSecretsSettings>>().Value;
                var dbSettings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;

                configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseMongoStorage($"{mongoSecretsSettings.MongoConnectionString}/{dbSettings.DatabaseName}", new MongoStorageOptions
                    {
                        Prefix = hangFirePrefix,
                        MigrationOptions = new MongoMigrationOptions(MongoMigrationStrategy.Migrate),
                        InvisibilityTimeout = TimeSpan.FromMinutes(5),
                    });
            });

            services.AddHangfireServer();

            services.AddTransient<IPaymentCancellationJob, PaymentCancellationJob>();
            services.AddTransient<ISubscriptionCancellationJob, SubscriptionCancellationJob>();
            services.AddTransient<IMoveIncomeFromEscrowJob, MoveIncomeFromEscrowJob>();
            services.AddTransient<IMoveRevenueFromEscrowJob, MoveRevenueFromEscrowJob>();
            services.AddTransient<IJobScheduler, JobScheduler>();
            services.AddTransient<ISchedulePostJob, SchedulePostJob>();
            services.AddTransient<IContentAvailableJob, ContentAvailableJob>();

            // services.AddTransient<ISendEmailCoachInstructionGuideJob, SendEmailCoachInstructionGuideJob>();
            // services.AddTransient<ISendEmailCoachOneToOneInstructionGuideJob, SendEmailCoachOneToOneInstructionGuideJob>();
            services.AddSingleton<IJobRecurringCreator, JobRecurringCreator>();
            services.AddSingleton<IUnreadChatJob, UnreadChatJob>();
            //services.AddSingleton<IBookIfSingleSessionTimeJob, BookIfSingleSessionTimeJob>();
            services.AddTransient<IPlanExpireAlertJob, PlanExpireAlertJob>();
        }
    }
}
