using System.IO;
using Cohere.Domain.Service;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using NUnit.Framework;

namespace Cohere.Api.UnitTests
{
    class SlotsGenerationTests
    {
        [Test]
        [Description("Get Scheduled Slots should not throw exception")]
        public void GetScheduledSlotsWithDaylightSavingTimeChangingShouldNotThrowException()
        {
            var json = File.ReadAllText("./Data/SchedulingCriteria.json");
            var schedulingCriteria = Newtonsoft.Json.JsonConvert.DeserializeObject<OneToOneSessionDataUi>(json);

            Assert.DoesNotThrow(() =>
            {
                foreach (var coachTimeZoneId in DateTimeHelper.TimeZoneFriendlyNames.Keys)
                {
                    SlotsGenerator.GetScheduledSlots(schedulingCriteria, coachTimeZoneId, 0);
                }
            });
        }
    }
}
