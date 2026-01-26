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
    public record LogoutCommand(string RefreshToken) : IRequest;
    public class LogoutCommandHandler: IRequestHandler<LogoutCommand>
    {

        private readonly IApplicationDbContext _db;
        private readonly ICurrentUserService _currentUser;

        public LogoutCommandHandler(
            IApplicationDbContext db,
            ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId;

            
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(r =>
                    r.Token == request.RefreshToken &&
                    r.UserId.ToString() == userId &&
                    !r.IsRevoked,
                    cancellationToken);

            if (token == null)
                throw new BadRequestException("Invalid refresh token");

            token.IsRevoked = true;

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
