using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Features.Auth.Commands;
using ZHSystem.Domain.Entities;
using ZHSystem.Test.Common;

namespace ZHSystem.Test.Features.Auth
{
    public class VerifyEmailCommandHandlerTests
    {
        private static string HashToken(string rawToken)
        {
            return Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))
            );
        }

        [Fact]
        public async Task VerifyEmail_Should_Verify_User_When_Token_Is_Valid()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var rawToken = "valid-token";
            var user = new User
            {
                Id = 1,
                Email = "user@test.com",
                UserName = "testuser",
                EmailVerified = false,
                EmailVerificationTokenHash = HashToken(rawToken),
                EmailVerificationExpires = DateTime.UtcNow.AddHours(1),
                PasswordHash = "hashed-password"
            };

            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            var handler = new VerifyEmailCommandHandler(db);

            var command = new VerifyEmailCommand(rawToken);

            // Act
            await handler.Handle(command, default);

            // Assert
            var updatedUser = await db.Users.SingleAsync();
            updatedUser.EmailVerified.Should().BeTrue();
            updatedUser.EmailVerificationTokenHash.Should().BeNull();
            updatedUser.EmailVerificationExpires.Should().BeNull();
        }

        [Fact]
        public async Task VerifyEmail_Should_Throw_When_Token_Is_Invalid()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var user = new User
            {
                Id = 1,
                Email = "user@test.com",
                UserName = "testuser",
                EmailVerified = false,
                EmailVerificationTokenHash = HashToken("correct-token"),
                EmailVerificationExpires = DateTime.UtcNow.AddHours(1),
                PasswordHash = "hashed-password"
            };

            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            var handler = new VerifyEmailCommandHandler(db);

            // Act
            Func<Task> act = async () =>
                await handler.Handle(new VerifyEmailCommand("wrong-token"), default);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid or expired token.");
        }

        [Fact]
        public async Task VerifyEmail_Should_Throw_When_Token_Is_Expired()
        {
            // Arrange
            var db = ApplictionDbContextTestFactory.CreateDbContext();

            var rawToken = "expired-token";
            var user = new User
            {
                Id = 1,
                Email = "user@test.com",
                UserName = "testuser",
                EmailVerified = false,
                EmailVerificationTokenHash = HashToken(rawToken),
                EmailVerificationExpires = DateTime.UtcNow.AddMinutes(-1),
                PasswordHash = "hashed-password"
            };

            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();

            var handler = new VerifyEmailCommandHandler(db);

            // Act
            Func<Task> act = async () =>
                await handler.Handle(new VerifyEmailCommand(rawToken), default);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid or expired token.");
        }
    }
}
