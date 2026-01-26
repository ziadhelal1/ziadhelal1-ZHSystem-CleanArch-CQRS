using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    public class RevokeTokenCommandHandlerTests
    {
        

        private Mock<ICurrentUserService> MockCurrentUser(string? userId)
        {
            var mock = new Mock<ICurrentUserService>();
            mock.Setup(x => x.UserId).Returns(userId);
            return mock;
        }



        //1. happy path: valid token, user authenticated
        [Fact]
        public async Task Should_Revoke_Token_When_Valid()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var token = new RefreshToken
            {
                Token = "valid-token",
                UserId = 1,
                IsRevoked = false,
                Expires = DateTime.UtcNow.AddDays(1)
            };

            await db.RefreshTokens.AddAsync(token);
            await db.SaveChangesAsync();

            var currentUser = MockCurrentUser("1");
            var handler = new RevokeRefreshTokenCommandHandler(db, currentUser.Object);
            var command = new RevokeRefreshTokenCommand("valid-token");

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            var tokenInDb = await db.RefreshTokens.SingleAsync();
            tokenInDb.IsRevoked.Should().BeTrue();
        }

        //2. user not authenticated
        [Fact]
        public async Task Should_Throw_Unauthorized_When_User_Not_Authenticated()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var currentUser = MockCurrentUser(null);

            var handler = new RevokeRefreshTokenCommandHandler(db, currentUser.Object);
            var command = new RevokeRefreshTokenCommand("token");

            // Act
            Func<Task> act = () => handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should()
                .ThrowAsync<UnauthorizedException>()
                .WithMessage("User not authenticated");
        }

       // 3. token does not exist
        [Fact]
        public async Task Should_Throw_NotFound_When_Token_Does_Not_Exist()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var currentUser = MockCurrentUser("1");

            var handler = new RevokeRefreshTokenCommandHandler(db, currentUser.Object);
            var command = new RevokeRefreshTokenCommand("not-exist");

            // Act
            Func<Task> act = () => handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should()
                .ThrowAsync<NotFoundException>()
                .WithMessage("Refresh token not found");
        }



        // 4. token already revoked
        [Fact]
        public async Task Should_Not_Throw_When_Token_Already_Revoked()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var token = new RefreshToken
            {
                Token = "revoked-token",
                UserId = 1,
                IsRevoked = true,
                Expires = DateTime.UtcNow.AddDays(1)
            };

            await db.RefreshTokens.AddAsync(token);
            await db.SaveChangesAsync();

            var currentUser = MockCurrentUser("1");
            var handler = new RevokeRefreshTokenCommandHandler(db, currentUser.Object);
            var command = new RevokeRefreshTokenCommand("revoked-token");

            // Act
            Func<Task> act = () => handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
