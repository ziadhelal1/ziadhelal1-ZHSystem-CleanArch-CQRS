using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZHSystem.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(string destination, string subject, string htmlBody);

        Task SendVerificationEmailAsync(string email, string name, string token);
    }
}
