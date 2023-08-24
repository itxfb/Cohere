using Cohere.Domain.Models.ContributionViewModels.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Cohere.Api.IntegrationTests.Controllers
{
    [DoNotParallelize]
    [TestClass]
    public class ContributionControllerTest : IntegrationTestBase
    {
        [TestMethod]
        public async Task OneToOneContribution_CreatedContributionShouldHaveInReviewStatus()
        {
            SetCoachAuthToken();

            var createdContribution = await ContributionActions.CreateOneToOneContribtuion(Client);

            Assert.IsTrue(!string.IsNullOrEmpty(createdContribution?.Id));
            Assert.IsTrue(createdContribution.Status == "InReview");
        } 

        [TestMethod]
        public async Task OneToOneContribution_ApprovedShouldHaveApprovedStatus()
        {
            SetCoachAuthToken();

            var createdContribution = await ContributionActions.CreateOneToOneContribtuion(Client);

            Assert.IsTrue(!string.IsNullOrEmpty(createdContribution?.Id));
            Assert.IsTrue(createdContribution.Status == "InReview");

            SetAdminAuthToken();

            await ContributionActions.ApproveContribution(createdContribution.Id, Client);

            SetCoachAuthToken();

            var updatedContribution = await ContributionActions.GetCoachContribtuion<ContributionOneToOneViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(updatedContribution.Status == "Approved");
        }

        [TestMethod]
        public async Task LiveCourseContribution__CreatedContributionShouldHaveInReviewStatus()
        {
            SetCoachAuthToken();

            var createdContribution = await ContributionActions.CreateLiveCourseContribtuion(Client);

            Assert.IsTrue(!string.IsNullOrEmpty(createdContribution?.Id));
            Assert.IsTrue(createdContribution.Status == "InReview");
        }

        [TestMethod]
        public async Task LiveCourseContribution_ApprovedShouldHaveApprovedStatus()
        {
            SetCoachAuthToken();

            var createdContribution = await ContributionActions.CreateLiveCourseContribtuion(Client);

            Assert.IsTrue(!string.IsNullOrEmpty(createdContribution?.Id));
            Assert.IsTrue(createdContribution.Status == "InReview");

            SetAdminAuthToken();

            await ContributionActions.ApproveContribution(createdContribution.Id, Client);

            SetCoachAuthToken();

            var updatedContribution = await ContributionActions.GetCoachContribtuion<ContributionCourseViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(updatedContribution.Status == "Approved");
        }
    }
}
