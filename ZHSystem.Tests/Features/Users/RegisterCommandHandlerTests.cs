using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.DTOs.Auth;
using ZHSystem.Application.Features.Auth.Commands;
using ZHSystem.Application.UnitTests.Common;
using ZHSystem.Domain.Entities;
using ZHSystem.Infrastructure.Persistence;
using ZHSystem.Test.Common;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IPasswordHasher<User>> _passwordHasherMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly IMapper _mapper;

    public RegisterCommandHandlerTests()
    {
        _mapper = AutoMapperTestFactory.CreateMapper();
    }

   
    [Fact]
    public async Task Register_Should_Create_User_And_Send_Verification_Email()
    {
        // Arrange
        var db = ApplictionDbContextTestFactory.CreateDbContext(); 


        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed-password");

        

        var handler = new RegisterCommandHandler(
            db,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _mapper,
            _emailServiceMock.Object,
            _configurationMock.Object
        );

        var command = new RegisterCommand(new RegisterDto
        {
            Email = "test@test.com",
            UserName = "testuser",
            Password = "Password123!"
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Result
        result.Message.Should().Contain("Registration successful");

        // Assert - Database
        var user = await db.Users.SingleAsync();

        user.Email.Should().Be("test@test.com");
        user.UserName.Should().Be("testuser");
        user.PasswordHash.Should().Be("hashed-password");
        user.EmailVerified.Should().BeFalse();

        // Assert - Dependencies
        _passwordHasherMock.Verify(
            x => x.HashPassword(It.IsAny<User>(), "Password123!"),
            Times.Once);

        _emailServiceMock.Verify(
            x => x.SendAsync(
                "test@test.com",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }
    [Fact]
    public async Task Register_Should_Throw_When_Email_Already_Exists()
    {
        // Arrange
        var db = ApplictionDbContextTestFactory.CreateDbContext();
        var existingUser = new User
        {
            Email = "existing@test.com",
            UserName = "exist",
            PasswordHash = "any-hash-value" 
        };
        await db.Users.AddAsync(existingUser);
        await db.SaveChangesAsync();

        var handler = new RegisterCommandHandler(
            db,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _mapper,
            _emailServiceMock.Object,
            _configurationMock.Object
        );

        var command = new RegisterCommand(new RegisterDto
        {
            Email = "existing@test.com",
            UserName = "newuser",
            Password = "Password123!"
        });

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, default));
    }
    [Fact]
    public async Task Register_Should_Handle_EmailService_Exception_Gracefully()
    {
        // Arrange
        var db = ApplictionDbContextTestFactory.CreateDbContext();
        var handler = new RegisterCommandHandler(
            db,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _mapper,
            _emailServiceMock.Object,
            _configurationMock.Object
        );

        var command = new RegisterCommand(new RegisterDto
        {
            Email = "test2@test.com",
            UserName = "user2",
            Password = "Password123!"
        });

        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed-password");

        _emailServiceMock
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP error"));

        // Act
        Func<Task> act = async () => await handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("SMTP error"); // Optional: depends on how you want to handle email exceptions
    }

   
    

}
