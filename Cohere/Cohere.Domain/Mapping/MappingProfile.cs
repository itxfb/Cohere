using System.Linq;

using AutoMapper;
using Cohere.Domain.Mapping.Extensions;
using Cohere.Domain.Models;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.Community.Comment;
using Cohere.Domain.Models.Community.Comment.Request;
using Cohere.Domain.Models.Community.Like;
using Cohere.Domain.Models.Community.Like.Request;
using Cohere.Domain.Models.Community.Post;
using Cohere.Domain.Models.Community.Post.Request;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.ContributionViewModels.ForAdmin;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer.Tables;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Note;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Models.Pods;
using Cohere.Domain.Models.Testimonial;
using Cohere.Domain.Models.TimeZone;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Users;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Payment;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Membership;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.Enums.User;

using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Stripe;
using Account = Cohere.Entity.Entities.Account;
using Coupon = Cohere.Entity.Entities.Coupon;

namespace Cohere.Domain.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            #region Account

            CreateMap<AccountViewModel, Account>()
                .ForMember(
                    a => a.Email,
                    opt => opt.MapFrom(s => s.Email.ToLower()))
                .ForMember(
                    a => a.DecryptedPassword,
                    opt => opt.MapFrom(s => s.Password))
                .ForMember(
                    a => a.OAuthToken,
                    opt => opt.Ignore());

            CreateMap<Account, AccountViewModel>()
                .ForMember(
                    a => a.Password,
                    opt => opt.Ignore())
                .ForMember(
                    a => a.SecurityAnswers,
                    opt => opt.Ignore());

            #endregion

            #region User

            CreateMap<UserViewModel, User>()
                .ForMember(x => x.ConnectedStripeAccountId, opt => opt.Ignore())
                .ForMember(x => x.CustomerStripeAccountId, opt => opt.Ignore())
                .ForMember(x => x.PlaidId, opt => opt.Ignore())
                .ForMember(x => x.Contributions, opt => opt.Ignore())
                .ForMember(x => x.Purchases, opt => opt.Ignore())
                .ForMember(x => x.Pods, opt => opt.Ignore())
                .ForMember(x => x.PaidTierPurchases, opt => opt.Ignore());

            CreateMap<User, UserViewModel>()
                .ForMember(u => u.CustomerLabelPreference,
                    opt => opt.Condition(src => src.CustomerLabelPreference != default(CustomerLabelPreferences)))
                .ForMember(u => u.BusinessType,
                    opt => opt.Condition(src => src.BusinessType != default(BusinessTypes)))
                .ForSourceMember(u => u.Contributions,
                    opt => opt.DoNotValidate())
                .ForSourceMember(u => u.Purchases,
                    opt => opt.DoNotValidate())
                .ForSourceMember(u => u.Pods,
                    opt => opt.DoNotValidate())
                .ForSourceMember(u => u.PaidTierPurchases,
                    opt => opt.DoNotValidate());

            CreateMap<PreferenceViewModel, Preference>().ReverseMap();

            CreateMap<User, ParticipantViewModel>();

            CreateMap<User, ParticipantInfo>()
                .ForMember(x => x.ParticipantId, opt => opt.MapFrom(s => s.Id));

            CreateMap<User, CohealerInfoViewModel>().ForMember(d => d.BusinessType, opt =>
                opt.MapFrom(src =>
                    src.BusinessType == BusinessTypes.Other ? src.CustomBusinessType : src.BusinessType.ToString()));

            CreateMap<User, UserPreviewViewModel>();

            #endregion

            #region Contribution

            CreateMap<ContributionBase, ContributionBaseViewModel>()
                .IncludeAllDerived()
                .ForMember(cb => cb.DefaultCurrency, opt =>
                {
                    opt.NullSubstitute("usd");
                })
                .ForMember(cb => cb.DefaultSymbol, opt =>
                {
                    opt.NullSubstitute("$");
                })
                ;

            CreateMap<ContributionBaseViewModel, ContributionBase>()
                .IncludeAllDerived()
                .ForMember(cb => cb.DefaultCurrency, opt =>
                {
                    opt.MapFrom(src => src.DefaultCurrency.ToLowerInvariant());
                    opt.NullSubstitute("usd");
                })
                .ForMember(cb => cb.DefaultSymbol, opt =>
                {
                    opt.NullSubstitute("$");
                });

            CreateMap<ContributionCourse, ContributionCourseViewModel>()
                .ConstructUsingServiceLocator()
                .ReverseMap();

            CreateMap<ContributionOneToOne, ContributionOneToOneViewModel>()
                .ConstructUsingServiceLocator()
                .ReverseMap();

            CreateMap<ContributionMembership, ContributionMembershipViewModel>()
                .ConstructUsingServiceLocator()
                .ReverseMap();

            CreateMap<ContributionCommunity, ContributionCommunityViewModel>()
               .ConstructUsingServiceLocator()
               .ReverseMap();

            CreateMap<ContributionBase, ContributionAdminBriefViewModel>();

            CreateMap<ContributionBase, ContributionInCohealerInfoViewModel>();

            CreateMap<ContributionBaseViewModel, ContribTableViewModel>()
                .ForMember(ct => ct.StudentsNumber, opt => opt.MapFrom(c => c.GetBookedParticipantsIds().Count))
                .ForMember(ct => ct.ReasonDescription, opt =>
                {
                    opt.PreCondition(src => src.AdminReviewNotes.Any());
                    opt.MapFrom(src => src.AdminReviewNotes.OrderBy(rn => rn.DateUtc).Last().Description);
                });

            CreateMap<ContributionBase, AcademyContributionPreviewViewModel>()
                .ForMember(e => e.ServiceProviderName,
                    opt => opt.Ignore());

            CreateMap<AdminReviewNoteViewModel, AdminReviewNote>();
            CreateMap<StripeEvent, StripeEventViewModel>().ReverseMap();
            CreateMap<PaymentInfoViewModel, PaymentInfo>().ReverseMap();
            CreateMap<MembershipInfoViewModel, MembershipInfo>()
                .ForMember(e => e.PaymentOptionsMap, opt => opt.Ignore())
                .ReverseMap();
            CreateMap<SignoffInfo, SignoffInfoViewModel>().ReverseMap();
            CreateMap<EmailTemplates, EmailTemplatesViewModel>().ReverseMap();
            #endregion

            #region Purchase

            CreateMap<Purchase, PurchaseViewModel>()
                .ConstructUsingServiceLocator()
                .ReverseMap()
                .ConstructUsingServiceLocator();

            CreateMap<PackagePurchase, PackagePaymentDetailViewModel>();

            #endregion

            CreateMap<Agreement, AgreementViewModel>();

            #region Note

            CreateMap<Note, NoteViewModel>().ReverseMap();
            CreateMap<NoteBriefViewModel, Note>();

            #endregion

            #region PaidTier

            CreateMap<PaidTierOptionViewModel, PaidTierOption>()
                .ForMember(
                    pt => pt.PaidTierInfo,
                    opt => opt.Ignore()).ReverseMap();

            #endregion

            #region Pod

            CreateMap<Pod, PodViewModel>().ReverseMap();
            CreateMap<PodViewModel, Pod>();

            #endregion

            #region Testimonial

            CreateMap<Testimonial, TestimonialViewModel>().ReverseMap();
            CreateMap<TestimonialViewModel, Testimonial>();

            #endregion

            #region Community

            CreateMap<CreatePostRequest, Post>();
            CreateMap<UpdatePostRequest, Post>()
                .ReverseMap()
                .ForAllMembers(opts => opts.Condition(AutoMapperExtensions.ExceptDefaultValues));
            CreateMap<Post, PostDto>();

            CreateMap<CreateCommentRequest, Comment>();
            CreateMap<UpdateCommentRequest, Comment>()
                .ReverseMap()
                .ForAllMembers(opts => opts.Condition(AutoMapperExtensions.ExceptDefaultValues));

            CreateMap<Comment, CommentDto>();

            CreateMap<AddLikeRequest, Like>();
            CreateMap<Like, LikeDto>();

            CreateMap<User, CommunityUserDto>();
            CreateMap<User, CommunityPostUserDto>();

            #endregion

            #region Coupons

            CreateMap<CreateCouponRequest, Coupon>();
            CreateMap<CreateCouponRequest, CouponCreateOptions>()
                .ForMember(
                    pt => pt.AmountOff,
                    opt => opt.MapFrom(src => src.AmountOff > 0 ? src.AmountOff * 100 : null));
            CreateMap<Stripe.Coupon, CouponDto>();
            CreateMap<Coupon, CouponDto>()
                .ForMember(cb => cb.Currency, opt =>
                    {
                        opt.NullSubstitute("usd");
                    });

            #endregion

            #region ProfilePage

            CreateMap<ProfilePage, ProfilePageViewModel>().ReverseMap();

            CreateMap<CustomLinks, CustomLinksViewModel>().ReverseMap();

            #endregion

            var description =
                "When it's time for your session, please launch Cohere by going to the following URL to login: App.Cohere.Live.\n" +
                "If you need to contact the other session participant(s), this can also be done by going to App.Cohere.Live and then visiting \"Conversations\".\n" +
                "If you need to reschedule, this can done by going to App.Cohere.Live and then visiting the Sessions tab. If you are a Client, please go to \"My Journey\", click on the session, and then visit the \"Sessions\" tab to find the reschedule button. If you are a service provider, please open your Contribution and go to the \"Sessions\" tab.\n\n" +
                "PLEASE NOTE, THIS IS A NO-REPLY EMAIL ACCOUNT.";

            CreateMap<SessionTimeToSession, CalendarEvent>()
                .ConstructUsing(sessionTimeToSession =>
                    new CalendarEvent()
                    {
                        Uid = $"{sessionTimeToSession.SessionTime.Id.ToString()}@cohere.live",
                        Summary = $"{sessionTimeToSession.Session.Title} {sessionTimeToSession.ContributionName}",
                        Description = description,
                        DtStart = new CalDateTime(sessionTimeToSession.SessionTime.StartTime),
                        DtEnd = new CalDateTime(sessionTimeToSession.SessionTime.EndTime),
                        Sequence = 0,
                        Location = "https://app.cohere.live",
                    }).AfterMap((e, calendarEvent) =>
                {
                    calendarEvent.DtStart.HasTime = true;
                    calendarEvent.DtEnd.HasTime = true;
                });

            CreateMap<BookedTimeToAvailabilityTime, CalendarEvent>()
                .ConstructUsing(bookedTimeToAvailabilityTime =>
                    new CalendarEvent()
                    {
                        Uid = $"{bookedTimeToAvailabilityTime.BookedTime.Id.ToString()}@cohere.live",
                        Summary =
                            $"{bookedTimeToAvailabilityTime.ContributionName} {bookedTimeToAvailabilityTime.ClientName ?? string.Empty}"
                                .Trim(),
                        Description = description,
                        DtStart = new CalDateTime(bookedTimeToAvailabilityTime.BookedTime.StartTime),
                        DtEnd = new CalDateTime(bookedTimeToAvailabilityTime.BookedTime.EndTime),
                        Sequence = 0,
                        Location = "https://app.cohere.live",
                    }).AfterMap((e, calendarEvent) =>
                {
                    calendarEvent.DtStart.HasTime = true;
                    calendarEvent.DtEnd.HasTime = true;
                });

            #region TimeZone

            CreateMap<TimeZone, TimeZoneViewModel>();

            #endregion

            CreateMissingTypeMaps = true;

            #region Country

            CreateMap<Country, CountryViewModel>();

            #endregion

            CreateMissingTypeMaps = true;
        }
    }
}