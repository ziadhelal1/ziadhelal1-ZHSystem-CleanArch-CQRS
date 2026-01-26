using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZHSystem.Application.Common;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.DTOs.Auth;
using ZHSystem.Domain.Entities;
using Google.Apis.Auth;

namespace ZHSystem.Application.Features.Auth.Commands
{
    public record GoogleLoginCommand(string IdToken) : IRequest<RefreshResponseDto>;
    public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, RefreshResponseDto>
    {
        private readonly IApplicationDbContext _db;
        private readonly ITokenService _tokenService;

        public GoogleLoginCommandHandler(IApplicationDbContext db, ITokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        public async Task<RefreshResponseDto> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
            }
            catch
            {
                throw new UnauthorizedException("Invalid Google token");
            }

            // check if user exists
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email, cancellationToken);
            if (user == null)
            {
                // create new user if not exist and attach him with a role

                const int UserRoleId = 2;


                user = new User
                {
                    Email = payload.Email,
                    UserName = payload.Name,
                    EmailVerified = true,// already verified by Google
                    PasswordHash = Guid.NewGuid().ToString(), // random password
                    

                };
                var userRole = new UserRole { User = user, RoleId = UserRoleId };

                await _db.Users.AddAsync(user, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }

            // create JWT token like existing auth
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var accessToken = _tokenService.GenerateAccessToken(user, roles);
            var refreshToken = _tokenService.CreateRefreshToken(user.Id);

            await _db.RefreshTokens.AddAsync(refreshToken, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return new RefreshResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60) // Or To JWT settings
            };
        }
    }
}
