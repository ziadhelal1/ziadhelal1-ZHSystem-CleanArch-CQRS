using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZHSystem.Application.Common;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.Features.Auth.Commands;
using ZHSystem.Domain.Entities;
using ZHSystem.Infrastructure.Persistence;
using ZHSystem.Test.Common;

namespace ZHSystem.Test.Features.Users
{
    public class RefreshTokenCommandHandlerTests
    {
        private readonly Mock<ITokenService> _tokenServiceMock = new();

        private readonly JwtSettings _jwtSettings = new()
        {
            AccessTokenExpirationMinutes = 15
        };
    
        private RefreshTokenCommandHandler CreateHandler(ApplicationDbContext db)
        {
            var jwtOptions = Options.Create(_jwtSettings);

            return new RefreshTokenCommandHandler(
                db,
                _tokenServiceMock.Object,
                jwtOptions
            );
        }

        //  Valid refresh token
        [Fact]
        public async Task RefreshToken_Should_Issue_New_Tokens_When_Valid()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var user = new User
            {
                Id = 1,
                Email = "test@test.com",
                UserName = "testuser",
                EmailVerified = true,
                PasswordHash = "any-hash-value"
            };

            var oldRefreshToken = new RefreshToken
            {
                Token = "old-refresh-token",
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(1),
                IsRevoked = false
            };

            db.Users.Add(user);
            db.RefreshTokens.Add(oldRefreshToken);
            await db.SaveChangesAsync();

            _tokenServiceMock
                .Setup(x => x.GenerateAccessToken(user, It.IsAny<List<string>>()))
                .Returns("new-access-token");

            _tokenServiceMock
                .Setup(x => x.CreateRefreshToken(user.Id))
                .Returns(new RefreshToken
                {
                    Token = "new-refresh-token",
                    UserId = user.Id,
                    Expires = DateTime.UtcNow.AddDays(7)
                });

            var handler = CreateHandler(db);
            var command = new RefreshTokenCommand("old-refresh-token");

            // Act
            var result = await handler.Handle(command, default);

            // Assert - response
            result.AccessToken.Should().Be("new-access-token");
            result.RefreshToken.Should().Be("new-refresh-token");

            // Assert - database
            var tokens = await db.RefreshTokens.ToListAsync();
            tokens.Should().HaveCount(2);
            tokens.Single(t => t.Token == "old-refresh-token").IsRevoked.Should().BeTrue();
        }

        // 2️ Token not found
        [Fact]
        public async Task RefreshToken_Should_Throw_When_Token_Not_Found()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();
            var handler = CreateHandler(db);

            var command = new RefreshTokenCommand("invalid-token");

            // Act 
            Func<Task> act = async() => await handler.Handle(command, default);
            //Assert
            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid refresh token");

            // Act & Assert -- in one line --
            //await Assert.ThrowsAsync<BadRequestException>(
            //    () => handler.Handle(command, default));
        }

        // 3️ Expired token
        [Fact]
        public async Task RefreshToken_Should_Throw_When_Token_Expired()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var token = new RefreshToken
            {
                Token = "expired-token",
                UserId = 1,
                Expires = DateTime.UtcNow.AddMinutes(-1),
                IsRevoked = false
            };

            db.RefreshTokens.Add(token);
            await db.SaveChangesAsync();

            var handler = CreateHandler(db);
            var command = new RefreshTokenCommand("expired-token");

            // Act 
            Func<Task> act = async () => await handler.Handle(command, default);
            //Assert
            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid refresh token");


            // Act & Assert -- in one line --
            //await Assert.ThrowsAsync<BadRequestException>(
            //    () => handler.Handle(command, default));
        }

        // 4️  Revoked token
        [Fact]
        public async Task RefreshToken_Should_Throw_When_Token_Revoked()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var token = new RefreshToken
            {
                Token = "revoked-token",
                UserId = 1,
                Expires = DateTime.UtcNow.AddDays(1),
                IsRevoked = true
            };

            db.RefreshTokens.Add(token);
            await db.SaveChangesAsync();

            var handler = CreateHandler(db);
            var command = new RefreshTokenCommand("revoked-token");

            // Act 

            Func<Task> act = async () => await handler.Handle(command, default);

            //Assert
            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid refresh token");

            // Act & Assert -- in one line --
            //await Assert.ThrowsAsync<BadRequestException>(
            //    () => handler.Handle(command, default));
        }

        // 5️  User not found
        [Fact]
        public async Task RefreshToken_Should_Throw_When_User_Not_Found()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var token = new RefreshToken
            {
                Token = "valid-token",
                UserId = 1, // no user exists
                Expires = DateTime.UtcNow.AddDays(1),
                IsRevoked = false
            };

            db.RefreshTokens.Add(token);
            await db.SaveChangesAsync();

            var handler = CreateHandler(db);
            var command = new RefreshTokenCommand("valid-token");
            //Act
            Func<Task> act = async () => await handler.Handle(command, default);
            //Assert
            await act.Should().ThrowAsync<NotFoundException>().WithMessage("User Not Exist");
            // Act & Assert -- in one line --

            
            //await Assert.ThrowsAsync<NotFoundException>(
            //    () => handler.Handle(command, default));
        }
    }
}
