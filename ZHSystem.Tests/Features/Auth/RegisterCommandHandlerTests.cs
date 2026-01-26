using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

namespace ZHSystem.Test.Features.Auth;
public class RegisterCommandHandlerTests
{
    private readonly Mock<IPasswordHasher<User>> _passwordHasherMock = new();
    
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<RegisterCommandHandler>> _loggerMock = new(); 

    private readonly  IMapper _mapper;

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
            _mapper,
            _emailServiceMock.Object,
            _loggerMock.Object

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
            x => x.SendVerificationEmailAsync(
                "test@test.com",
                "testuser",
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
            _mapper,
            _emailServiceMock.Object,
            _loggerMock.Object

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

        _emailServiceMock
           .Setup(x => x.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .ThrowsAsync(new Exception("SMTP error"));


        _passwordHasherMock
            .Setup(x => x.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed-password");


        var handler = new RegisterCommandHandler(
            db,
            _passwordHasherMock.Object,
            _mapper,
            _emailServiceMock.Object
            , _loggerMock.Object

        );

        var command = new RegisterCommand(new RegisterDto
        {
            Email = "test2@test.com",
            UserName = "user2",
            Password = "Password123!"
        });

        

        //Act
        var result = await handler.Handle(command, CancellationToken.None);
        // Act
        //Func<Task> act = async () => await handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("couldn't send the verification email");



        //Verify that the user was still created despite the email failure
        var userInDb = await db.Users.AnyAsync(u => u.Email == "test2@test.com");
        userInDb.Should().BeTrue();

        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }




}
