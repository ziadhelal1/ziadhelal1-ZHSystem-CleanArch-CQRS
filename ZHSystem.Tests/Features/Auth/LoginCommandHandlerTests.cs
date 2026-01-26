using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ZHSystem.Application.Common;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.DTOs.Auth;
using ZHSystem.Application.Features.Auth.Commands;
using ZHSystem.Application.UnitTests.Common;
using ZHSystem.Domain.Entities;
using ZHSystem.Infrastructure.Persistence;
using ZHSystem.Test.Common;




namespace ZHSystem.Test.Features.Auth;
public class LoginCommandHandlerTests
{
    private readonly Mock<IPasswordHasher<User>> _passwordHasherMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly IOptions<JwtSettings> _jwtOptions;

    public LoginCommandHandlerTests()
    {
        _jwtOptions = Options.Create(new JwtSettings
        {
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        });
    }


    [Fact]
    public async Task Login_Should_Return_Tokens_When_Credentials_Are_Valid()
    {
        // Arrange
        var db = ApplictionDbContextTestFactory.CreateDbContext(); 

        var user = new User
        {
            
            Email = "test@test.com",
            UserName = "testuser",
            EmailVerified = true,
            PasswordHash = "hashed-password",
            UserRoles =
            {
                new UserRole { Role = new Role { Name = "User" } }
            }
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        _passwordHasherMock
            .Setup(x => x.VerifyHashedPassword(user, "hashed-password", "Password123!"))
            .Returns(PasswordVerificationResult.Success);

        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(x => x.CreateRefreshToken(user.Id))
            .Returns(new RefreshToken { Token = "refresh-token", UserId = user.Id });

        var handler = new LoginCommandHandler(db, _passwordHasherMock.Object, _tokenServiceMock.Object, _jwtOptions);

        var command = new LoginCommand(new LoginDto
        {
            Email = "test@test.com",
            Password = "Password123!"
        });

        // Act
        var result = await handler.Handle(command, default);

        // Assert - Result
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");

        // Assert - Database
        db.RefreshTokens.Count().Should().Be(1);

        // Assert - Dependencies
        _passwordHasherMock.Verify(x => x.VerifyHashedPassword(user, "hashed-password", "Password123!"), Times.Once);
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()), Times.Once);
        _tokenServiceMock.Verify(x => x.CreateRefreshToken(user.Id), Times.Once);
    }

    [Fact]
    public async Task Login_Should_Throw_When_Password_Is_Wrong()
    {
        // Arrange
        var db = ApplictionDbContextTestFactory.CreateDbContext();

        var user = new User
        {
          
            Email = "test@test.com",
            UserName = "testuser",
            EmailVerified = true,
            PasswordHash = "hashed-password"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        _passwordHasherMock
            .Setup(x => x.VerifyHashedPassword(user, "hashed-password", "WrongPass"))
            .Returns(PasswordVerificationResult.Failed);

        var handler = new LoginCommandHandler(db, _passwordHasherMock.Object, _tokenServiceMock.Object, _jwtOptions);

        var command = new LoginCommand(new LoginDto
        {
            Email = "test@test.com",
            Password = "WrongPass"
        });

        // Act
        Func<Task> act = async () => await handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Login_Should_Throw_When_Email_Is_Not_Verified()
    {
        // Arrange
        var db = ApplictionDbContextTestFactory.CreateDbContext();

        var user = new User
        {
           
            Email = "test@test.com",
            UserName = "testuser",
            EmailVerified = false,
            PasswordHash = "hashed-password"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(db, _passwordHasherMock.Object, _tokenServiceMock.Object, _jwtOptions);

        var command = new LoginCommand(new LoginDto
        {
            Email = "test@test.com",
            Password = "Password123!"
        });

        // Act
        Func<Task> act = async () => await handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Email not verified");
    }
}
