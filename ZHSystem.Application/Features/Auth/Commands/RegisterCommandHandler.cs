using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZHSystem.Application.Common;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.DTOs;
using ZHSystem.Application.DTOs.Auth;
using ZHSystem.Domain.Entities;
using static Google.Apis.Auth.OAuth2.Web.AuthorizationCodeWebApp;


namespace ZHSystem.Application.Features.Auth.Commands
{
    public record RegisterCommand(RegisterDto Dto) : IRequest<RegisterResponseDto>;
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResponseDto>
    {
        private readonly IApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ILogger<RegisterCommandHandler> _logger;
        public RegisterCommandHandler(
            IApplicationDbContext db,
            IPasswordHasher<User> passwordHasher,
            IMapper mapper
            ,IEmailService emailService ,ILogger<RegisterCommandHandler> logger)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _mapper = mapper;
            _emailService = emailService;
            _logger = logger;

        }
        public async Task<RegisterResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email, cancellationToken)) 
            {
                var errors = new Dictionary<string, string[]> { {
                    "Email" ,new[]{"This Email Already Registered."} } };
                throw new ValidationException(errors);

            }


            var user = _mapper.Map<User>(dto);
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            user.EmailVerified = false;
            var rawToken = Guid.NewGuid().ToString("N");

            user.EmailVerificationTokenHash =
                Convert.ToBase64String(
                    SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))
                );

            user.EmailVerificationExpires = DateTime.UtcNow.AddHours(24);
            const int UserRoleId = 2;
            var userRole = new UserRole { User = user, RoleId = UserRoleId };
            await _db.Users.AddAsync(user, cancellationToken);
            await _db.UserRoles.AddAsync(userRole, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            //send verification email to user
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.UserName, rawToken);
            }
            catch (Exception ex)
            {
                
                _logger.LogError(ex, "User registered but verification email failed to send for {Email}", user.Email);

               
                return new RegisterResponseDto
                {
                    
                    Message = "Registration successful, but we couldn't send the verification email. Please try resending it from your profile."
                };
            }

            return new RegisterResponseDto {  Message = "Registration successful. Please check your email." };




           
        }
    }
}
