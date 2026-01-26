using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.Features.Auth.Commands;
using ZHSystem.Domain.Entities;
using ZHSystem.Infrastructure.Persistence;
using ZHSystem.Test.Common;

namespace ZHSystem.Test.Features.Auth
{
    public  class ForgotPasswordCommandHandlerTests
    {
        private readonly Mock<IEmailService> _emailServiceMock = new();
        private readonly Mock<IConfiguration> _configurationMock = new();

        

        [Fact]
        public async Task ForgotPassword_Should_CreateToken_And_SendEmail_When_UserExists()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var user = new User
            {
                Id = 1,
                Email = "user@test.com",
                UserName = "testuser",
                PasswordHash = "hashed",
            };
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            _configurationMock.Setup(c => c["ClientSettings:BaseUrl"])
                .Returns("https://localhost:7277");

            var handler = new ForgotPasswordCommandHandler(db, _emailServiceMock.Object, _configurationMock.Object);

            // Act
            await handler.Handle(new ForgotPasswordCommand("user@test.com"), default);

            // Assert - Token created
            var updatedUser = await db.Users.SingleAsync();
            updatedUser.PasswordResetTokenHash.Should().NotBeNullOrEmpty();
            updatedUser.PasswordResetExpires.Should().BeAfter(DateTime.UtcNow.AddMinutes(29));
            updatedUser.LastSecurityEmailSentAt.Should().BeAfter(DateTime.UtcNow.AddSeconds(-1));

            // Assert - Email sent
            _emailServiceMock.Verify(x => x.SendAsync(
                "user@test.com",
                "Reset your password",
                It.Is<string>(s => s.Contains("Reset Password"))
            ), Times.Once);
        }

        [Fact]
        public async Task ForgotPassword_Should_Return_When_UserNotFound()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var handler = new ForgotPasswordCommandHandler(db, _emailServiceMock.Object, _configurationMock.Object);

            // Act
            var act = async () => await handler.Handle(new ForgotPasswordCommand("notfound@test.com"), default);

            // Assert - does not throw
            await act.Should().NotThrowAsync();
            _emailServiceMock.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPassword_Should_Throw_RateLimitException_When_CalledTooSoon()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var user = new User
            {
                Id = 1,
                Email = "user@test.com",
                UserName = "testuser",
                PasswordHash = "hashed",
                LastSecurityEmailSentAt = DateTime.UtcNow
            };
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            var handler = new ForgotPasswordCommandHandler(db, _emailServiceMock.Object, _configurationMock.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(new ForgotPasswordCommand("user@test.com"), default);

            // Assert
            await act.Should().ThrowAsync<RateLimitException>()
                .WithMessage("*Please wait*");
        }

        [Fact]
        public async Task ForgotPassword_Should_Throw_When_EmailServiceFails()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var user = new User
            {
                Id = 1,
                Email = "user@test.com",
                UserName = "testuser",
                PasswordHash = "hashed"
            };
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            _configurationMock.Setup(c => c["ClientSettings:BaseUrl"]).Returns("https://localhost:7277");

            _emailServiceMock
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP error"));

            var handler = new ForgotPasswordCommandHandler(db, _emailServiceMock.Object, _configurationMock.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(new ForgotPasswordCommand("user@test.com"), default);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("SMTP error");
        }
    }
}

