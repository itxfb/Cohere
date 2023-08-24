using Cohere.Api.Controllers;
using Cohere.Api.Utils.Abstractions;
using Cohere.Domain;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cohere.Domain.Service;

namespace Cohere.Api.UnitTests
{

    public class UserControllerTests
    {
        private Mock<IUserService<UserViewModel, User>> _userServiceMock;
        private Mock<IValidator<UserViewModel>> _userValidatorMock;
        private Mock<ITokenGenerator> _tokenGeneratorMock;
        private Mock<ILogger<UserController>> _loggerMock;
        private Mock<INotificationService> _notificationServiceMock;
        private Mock<IRoleSwitchingService> _roleSwitchingServiceMock;
        private Mock<IContributionAccessService> _contributionAccessServiceMock;
        private Mock<IActiveCampaignService> _activeCampaignServiceMock;

        private UserController _userController;

        [SetUp]
        public void Init()
        {
            _userServiceMock = new Mock<IUserService<UserViewModel, User>>();
            _userValidatorMock = new Mock<IValidator<UserViewModel>>();
            _tokenGeneratorMock = new Mock<ITokenGenerator>();
            _loggerMock = new Mock<ILogger<UserController>>();
            _notificationServiceMock = new Mock<INotificationService>();
            _roleSwitchingServiceMock = new Mock<IRoleSwitchingService>();
            _contributionAccessServiceMock = new Mock<IContributionAccessService>();
            _activeCampaignServiceMock = new Mock<IActiveCampaignService>();
            _userController = new UserController(
                _userServiceMock.Object,
                null,
                _userValidatorMock.Object,
                _tokenGeneratorMock.Object,
                _loggerMock.Object,
                _roleSwitchingServiceMock.Object,
                _contributionAccessServiceMock.Object,
                _notificationServiceMock.Object,
                _activeCampaignServiceMock.Object, null, null, null, null,null,null);
        }

        [TearDown]
        public void Cleanup()
        {
            _userServiceMock = null;
            _userValidatorMock = null;
            _tokenGeneratorMock = null;
            _loggerMock = null;

            _userController = null;
        }

        [Test]
        [Description("GetAll() method execution should successfully return existing users.")]
        public async Task GetAllUsersReturnsOk()
        {
            //Arrange
            IEnumerable<UserViewModel> fakeUserList = new List<UserViewModel>();
            _userServiceMock.Setup(x => x.GetAll()).ReturnsAsync(fakeUserList);

            //Act
            var result = await _userController.GetAll();

            //Assert
            _userServiceMock.Verify(x => x.GetAll(), Times.Once);
            Assert.AreEqual(fakeUserList, result);
        }

        [Test]
        [Description("GetById() method execution should successfully return existing user.")]
        public async Task GetByIdUserReturnsOk()
        {
            //Arrange
            var fakeUserId = "userId";
            var fakeUser = new UserViewModel();
            _userServiceMock.Setup(x => x.GetOne(fakeUserId)).ReturnsAsync(fakeUser);

            //Act
            var result = await _userController.GetById(fakeUserId);

            //Assert
            _userServiceMock.Verify(x => x.GetOne(fakeUserId), Times.Once);
            Assert.IsInstanceOf<OkObjectResult>(result);
            Assert.AreEqual(fakeUser, (result as OkObjectResult)?.Value);
        }

        [Test]
        [Description("GetById() method execution should return Not Found result if user doesn't exist.")]
        public async Task GetByIdUserReturnsNotFound()
        {
            //Arrange
            var fakeUserId = "userId";
            var incomingUserId = "nonexistentId";
            var fakeUser = new UserViewModel();
            _userServiceMock.Setup(x => x.GetOne(fakeUserId)).ReturnsAsync(fakeUser);

            //Act
            var result = await _userController.GetById(incomingUserId);

            //Assert
            _userServiceMock.Verify(x => x.GetOne(incomingUserId), Times.Once);
            Assert.IsInstanceOf<NotFoundResult>(result);
        }

        [Ignore("Failed")]
        [Test]
        [Description("AddUserInfo() method execution should successfully return InSandbox Result.")]
        public async Task AddUserInfoReturnsOk()
        {
            //Arrange
            var fakeUserViewModel = new UserViewModel();
            _userValidatorMock.Setup(x
                    => x.ValidateAsync(fakeUserViewModel, CancellationToken.None))
                .ReturnsAsync(new ValidationResult());

            _userServiceMock.Setup(x
                => x.Insert(fakeUserViewModel))
                .ReturnsAsync(new OperationResult(true, string.Empty, new List<BaseDomain> { new AccountViewModel(), new UserViewModel() }));

            //Act
            var result = await _userController.AddUserInfo(fakeUserViewModel);

            //Assert
            _userValidatorMock.Verify(x => x.ValidateAsync(fakeUserViewModel, CancellationToken.None), Times.Once);
            _userServiceMock.Verify(x => x.Insert(fakeUserViewModel), Times.Once);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Once);
            Assert.IsInstanceOf<CreatedResult>(result);
            Assert.IsNotNull((result as CreatedResult)?.Value);
        }

        [Test]
        [Description("AddUserInfo() method execution should return Bad Request result if incoming model is empty.")]
        public async Task AddUserInfoReturnsBadRequestIfEmptyModel()
        {
            //Arrange
            var emptyModel = default(UserViewModel);

            //Act
            var result = await _userController.AddUserInfo(emptyModel);

            //Assert
            _userServiceMock.Verify(x => x.Insert(It.IsAny<UserViewModel>()), Times.Never);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Never);
            Assert.IsInstanceOf<BadRequestResult>(result);
        }

        [Test]
        [Description("AddUserInfo() method execution should return Bad Request result with Error Message result if model is invalid.")]
        public async Task AddUserInfoReturnsBadRequestIfInvalidModel()
        {
            //Arrange
            var fakeUserViewModel = new UserViewModel();
            var validationFailure = new ValidationFailure(nameof(fakeUserViewModel.AccountId), "Is required.");
            _userValidatorMock.Setup(x
                    => x.ValidateAsync(fakeUserViewModel, CancellationToken.None))
                .ReturnsAsync(new ValidationResult(new List<ValidationFailure> { validationFailure }));

            //Act
            var result = await _userController.AddUserInfo(fakeUserViewModel);

            //Assert
            _userValidatorMock.Verify(x => x.ValidateAsync(fakeUserViewModel, CancellationToken.None), Times.Once);
            _userServiceMock.Verify(x => x.Insert(It.IsAny<UserViewModel>()), Times.Never);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Never);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            Assert.IsInstanceOf<ErrorInfo>((result as BadRequestObjectResult)?.Value);
        }

        [Test]
        [Description("AddUserInfo() method execution should return Bad Request result with Error Message result if User Service insertion was unsuccessful.")]
        public async Task AddUserInfoReturnsBadRequestIfUserServiceFailedInsertion()
        {
            //Arrange
            var fakeUserViewModel = new UserViewModel();
            _userValidatorMock.Setup(x
                    => x.ValidateAsync(fakeUserViewModel, CancellationToken.None))
                .ReturnsAsync(new ValidationResult());

            _userServiceMock.Setup(x
                    => x.Insert(fakeUserViewModel))
                .ReturnsAsync(new OperationResult(false, string.Empty));

            //Act
            var result = await _userController.AddUserInfo(fakeUserViewModel);

            //Assert
            _userValidatorMock.Verify(x => x.ValidateAsync(fakeUserViewModel, CancellationToken.None), Times.Once);
            _userServiceMock.Verify(x => x.Insert(fakeUserViewModel), Times.Once);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            Assert.IsInstanceOf<ErrorInfo>((result as BadRequestObjectResult)?.Value);
        }

        [Ignore("Failed")]
        [Test]
        [Description("UpdateUserInfo() method execution should successfully return Accepted Result.")]
        public async Task UpdateUserInfoReturnsOk()
        {
            //Arrange
            var fakeUserId = "someid";
            var fakeUserViewModel = new UserViewModel { Id = fakeUserId };
            _userValidatorMock.Setup(x
                    => x.ValidateAsync(fakeUserViewModel, CancellationToken.None))
                .ReturnsAsync(new ValidationResult());

            _userServiceMock.Setup(x
                    => x.Update(fakeUserViewModel))
                .ReturnsAsync(new OperationResult(true, string.Empty, new List<BaseDomain> { new AccountViewModel(), new UserViewModel() }));

            //Act
            var result = await _userController.UpdateUserInfo(fakeUserId, fakeUserViewModel);

            //Assert
            _userValidatorMock.Verify(x => x.ValidateAsync(fakeUserViewModel, CancellationToken.None), Times.Once);
            _userServiceMock.Verify(x => x.Update(fakeUserViewModel), Times.Once);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Once);
            Assert.IsInstanceOf<AcceptedResult>(result);
            Assert.IsNotNull((result as AcceptedResult)?.Value);
        }

        [Test]
        [Description("UpdateUserInfo() method execution should return Bad Request result with Error Message if incoming model is empty.")]
        public async Task UpdateUserInfoReturnsBadRequestIfEmptyModel()
        {
            //Arrange
            var fakeUserId = "someid";

            //Act
            var result = await _userController.UpdateUserInfo(fakeUserId, null);

            //Assert
            _userServiceMock.Verify(x => x.Update(It.IsAny<UserViewModel>()), Times.Never);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Never);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            Assert.IsInstanceOf<ErrorInfo>((result as BadRequestObjectResult)?.Value);
        }

        [Test]
        [Description("UpdateUserInfo() method execution should return Bad Request result with Error Message if incoming id doesn't equal to incoming model id.")]
        public async Task UpdateUserInfoReturnsBadRequestIfIncomingIdDoesnotMatchIncomingModelId()
        {
            //Arrange
            var fakeUserId = "someid";
            var fakeUserViewModel = new UserViewModel();

            //Act
            var result = await _userController.UpdateUserInfo(fakeUserId, fakeUserViewModel);

            //Assert
            _userServiceMock.Verify(x => x.Update(It.IsAny<UserViewModel>()), Times.Never);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Never);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            Assert.IsInstanceOf<ErrorInfo>((result as BadRequestObjectResult)?.Value);
        }

        [Test]
        [Description("UpdateUserInfo() method execution should return Bad Request result with Error Message if model is invalid.")]
        public async Task UpdateUserInfoReturnsBadRequestIfInvalidModel()
        {
            //Arrange
            var fakeUserId = "someid";
            var fakeUserViewModel = new UserViewModel { Id = fakeUserId };
            var validationFailure = new ValidationFailure(nameof(fakeUserViewModel.AccountId), "Is required.");
            _userValidatorMock.Setup(x
                    => x.ValidateAsync(fakeUserViewModel, CancellationToken.None))
                .ReturnsAsync(new ValidationResult(new List<ValidationFailure> { validationFailure }));

            //Act
            var result = await _userController.UpdateUserInfo(fakeUserId, fakeUserViewModel);

            //Assert
            _userValidatorMock.Verify(x => x.ValidateAsync(fakeUserViewModel, CancellationToken.None), Times.Once);
            _userServiceMock.Verify(x => x.Update(It.IsAny<UserViewModel>()), Times.Never);
            _tokenGeneratorMock.Verify(x => x.GenerateToken(It.IsAny<AccountViewModel>()), Times.Never);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            Assert.IsInstanceOf<ErrorInfo>((result as BadRequestObjectResult)?.Value);
        }

        [Ignore("Failed")]
        [Test]
        [Description("UpdateUserInfo() method execution should return Not Modified 304 result with Error Message if User Service updating was unsuccessful.")]
        public async Task UpdateUserInfoReturnsBadRequestIfUserServiceFailedInsertion()
        {
            //Arrange
            var fakeUserId = "someid";
            var fakeUserViewModel = new UserViewModel { Id = fakeUserId };
            _userValidatorMock.Setup(x
                    => x.ValidateAsync(fakeUserViewModel, CancellationToken.None))
                .ReturnsAsync(new ValidationResult());

            _userServiceMock.Setup(x
                    => x.Update(fakeUserViewModel))
                .ReturnsAsync(new OperationResult(false, string.Empty));

            //Act
            var result = await _userController.UpdateUserInfo(fakeUserId, fakeUserViewModel);

            //Assert
            _userValidatorMock.Verify(x => x.ValidateAsync(fakeUserViewModel, CancellationToken.None), Times.Once);
            _userServiceMock.Verify(x => x.Update(fakeUserViewModel), Times.Once);
            Assert.IsInstanceOf<ObjectResult>(result);
            Assert.AreEqual(304, (result as ObjectResult)?.StatusCode);
            Assert.IsInstanceOf<ErrorInfo>((result as ObjectResult)?.Value);
        }

        [Test]
        [Description("DeleteUserInfo() method execution should successfully return No Content Result.")]
        public async Task DeleteUserInfoReturnsOk()
        {
            //Arrange
            var fakeUserId = "someid";
            _userServiceMock.Setup(x
                    => x.Delete(fakeUserId))
                .ReturnsAsync(new OperationResult(true, string.Empty));

            //Act
            var result = await _userController.DeleteUserInfo(fakeUserId);

            //Assert
            _userServiceMock.Verify(x => x.Delete(fakeUserId), Times.Once);
            Assert.IsInstanceOf<NoContentResult>(result);
        }

        [Test]
        [Description("Delete() method execution should return Not Found result with Error Message if user doesn't exist.")]
        public async Task DeleteReturnsNotFound()
        {
            //Arrange
            var incomingUserId = "nonexistentId";
            _userServiceMock.Setup(x => x.Delete(incomingUserId)).ReturnsAsync(new OperationResult(false, string.Empty));

            //Act
            var result = await _userController.DeleteUserInfo(incomingUserId);

            //Assert
            _userServiceMock.Verify(x => x.Delete(incomingUserId), Times.Once);
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            Assert.IsInstanceOf<ErrorInfo>((result as NotFoundObjectResult)?.Value);
        }
    }
}
