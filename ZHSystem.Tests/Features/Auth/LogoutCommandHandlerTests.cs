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
    public class LogoutCommandHandlerTests
    {
        private readonly Mock<ICurrentUserService> _currentUserMock = new();

       
        
        //1. happy path 
        
        [Fact]
        public async Task Logout_Should_Revoke_RefreshToken_When_Valid()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var user = new User
            {
                Id = 1,
                Email = "test@test.com",
                UserName = "test",
                PasswordHash = "hash"
            };

            var refreshToken = new RefreshToken
            {
                Token = "valid-refresh-token",
                UserId = 1,
                IsRevoked = false,
                Expires = DateTime.UtcNow.AddDays(1)
            };

            await db.Users.AddAsync(user);
            await db.RefreshTokens.AddAsync(refreshToken);
            await db.SaveChangesAsync();

            _currentUserMock
                .Setup(x => x.UserId)
                .Returns("1");

            var handler = new LogoutCommandHandler(db, _currentUserMock.Object);

            var command = new LogoutCommand("valid-refresh-token");

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            var tokenInDb = await db.RefreshTokens.SingleAsync();
            tokenInDb.IsRevoked.Should().BeTrue();
        }

        
        // 2. token not found 
        
        [Fact]
        public async Task Logout_Should_Throw_When_Token_Not_Found()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            _currentUserMock
                .Setup(x => x.UserId)
                .Returns("1");

            var handler = new LogoutCommandHandler(db, _currentUserMock.Object);

            var command = new LogoutCommand("not-exist-token");

            // Act
            Func<Task> act = () => handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should()
                .ThrowAsync<BadRequestException>()
                .WithMessage("Invalid refresh token");
        }

        //3.  already revoked
        [Fact]
        public async Task Logout_Should_Throw_When_Token_Already_Revoked()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var refreshToken = new RefreshToken
            {
                Token = "revoked-token",
                UserId = 1,
                IsRevoked = true,
                Expires = DateTime.UtcNow.AddDays(1)
            };

            await db.RefreshTokens.AddAsync(refreshToken);
            await db.SaveChangesAsync();

            _currentUserMock
                .Setup(x => x.UserId)
                .Returns("1");

            var handler = new LogoutCommandHandler(db, _currentUserMock.Object);

            var command = new LogoutCommand("revoked-token");

            // Act
            Func<Task> act = () => handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should()
                .ThrowAsync<BadRequestException>()
                .WithMessage("Invalid refresh token");
        }
    }
}
