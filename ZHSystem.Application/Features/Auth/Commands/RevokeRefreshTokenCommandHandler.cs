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

namespace ZHSystem.Application.Features.Auth.Commands
{
    public record RevokeRefreshTokenCommand(string RefreshToken)
    : IRequest;
    public class RevokeRefreshTokenCommandHandler: IRequestHandler<RevokeRefreshTokenCommand>
    {
        private readonly IApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public RevokeRefreshTokenCommandHandler(
            IApplicationDbContext db,
            ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        public async Task Handle(
            RevokeRefreshTokenCommand request,
            CancellationToken cancellationToken)
        {
            if (_currentUser.UserId == null)
                throw new UnauthorizedException("User not authenticated");

            var token = await _db.RefreshTokens.FirstOrDefaultAsync
                (t => t.Token == request.RefreshToken && t.UserId.ToString() == _currentUser.UserId, cancellationToken);
      

            if (token == null)
                throw new NotFoundException("Refresh token not found");

            if (token.IsRevoked)
                return; // mirror idempotent behavior

            token.IsRevoked = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
