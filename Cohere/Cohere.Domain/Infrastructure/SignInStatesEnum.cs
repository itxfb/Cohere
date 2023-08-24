namespace Cohere.Domain.Infrastructure
{
    public enum SignInStatesEnum
    {
        Succeeded = 1,

        Failed = 2,

        RequiresTwoFactor = 3,

        RequiresPasswordChange = 4,

        RequiresPasswordReset = 5,

        LockedOut = 6,

        NotAllowed = 7
    }
}
