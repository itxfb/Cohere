namespace Cohere.Api.IntegrationTests
{
    public static class ApiRoutes
    {
        public const string Base = "";

        public static class Contribution
        {
            public static string ContributionBaseUrl => $"{Base}/Contribution";

            public static class Client
            {
                public static string GetClientContribById(string contributionId) => $"{ContributionBaseUrl}/GetClientContribById/{contributionId}";

                public static string GetClientSlots(string contributionId) => $"{ContributionBaseUrl}/{contributionId}/GetClientSlots";
            }

            public static class Admin
            {
                public static string ChagneStatus(string contributionId) => $"{ContributionBaseUrl}/ChangeStatus/{contributionId}";
            }

            public static class Coach {
                public static string Create => ContributionBaseUrl;

                public static string Update(string contributionId) => $"{ContributionBaseUrl}/{contributionId}";

                public static string GetCohealerContribById(string contributionId) => $"{ContributionBaseUrl}/GetCohealerContribById/{contributionId}";
            }
        }

        public static class Purchase
        {
            private static string PurchaseBaseUrl => $"{Base}/api/Purchase";
            public static string PurchaseLiveCourse => $"{PurchaseBaseUrl}/course";
            public static string PurchaseOneToOneSession => $"{PurchaseBaseUrl}/one-to-one";
            public static string PurchaseOntToOnePackage => $"{PurchaseBaseUrl}/one-to-one/package";
        }

        public static class Auth
        {
            public static string GetAccountInfo() => $"{Base}/Auth/GetAccountInfo";
        }

        public static class Account
        {
            public static string GetById(string userId) => $"{Base}/Account/{userId}";
            public static string Create() => $"{Base}/Account";
        }

        public static class User
        {
            public static string Create() => $"{Base}/User";
        }
    }
}
