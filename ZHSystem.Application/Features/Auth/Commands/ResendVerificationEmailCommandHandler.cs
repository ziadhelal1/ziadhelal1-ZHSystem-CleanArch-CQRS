using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZHSystem.Application.Common;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ZHSystem.Application.Features.Auth.Commands
{

    public record ResendVerificationEmailCommand(string Email) : IRequest;
    public class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand>
    {
        private readonly IApplicationDbContext _db;
        private readonly IEmailService _emailService;
        

        public ResendVerificationEmailCommandHandler(
            IApplicationDbContext db,
            IEmailService emailService, IConfiguration configuration)
        {
            _db = db;
            _emailService = emailService;
           
        }

        public async Task Handle(
            ResendVerificationEmailCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            
            if (user == null ||user.EmailVerified)
                return ;

            

            //  rate limit => 5 minutes
            
            if (user.LastSecurityEmailSentAt.HasValue)
            {
                var nextAllowedSendTime = user.LastSecurityEmailSentAt.Value.AddMinutes(5);
                if (nextAllowedSendTime > DateTime.UtcNow)
                {
                    
                    var remainingSeconds = (int)(nextAllowedSendTime - DateTime.UtcNow).TotalSeconds;
                    throw new RateLimitException($"Please wait {remainingSeconds} seconds before requesting another email.");
                }
            }
            var rawToken = Guid.NewGuid().ToString("N");

            user.EmailVerificationTokenHash =
                Convert.ToBase64String(
                    SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))
                );

            user.EmailVerificationExpires = DateTime.UtcNow.AddHours(24);
            user.LastSecurityEmailSentAt = DateTime.UtcNow;

            await _emailService.SendVerificationEmailAsync(user.Email, user.UserName, rawToken);
        }
    }
}
