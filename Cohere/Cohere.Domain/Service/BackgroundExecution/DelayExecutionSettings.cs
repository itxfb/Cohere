namespace Cohere.Domain.Service.BackgroundExecution
{
    public class DelayExecutionSettings : IDelayExecutionSettings
    {
        public int SendCoachInstructionsGuideDelayMinutes { get; set; }

        public int SendCoachOneToOneInstructionsGuideDelayMinutes { get; set; }
    }

    public interface IDelayExecutionSettings
    {
        int SendCoachInstructionsGuideDelayMinutes { get; }

        int SendCoachOneToOneInstructionsGuideDelayMinutes { get; }
    }
}
