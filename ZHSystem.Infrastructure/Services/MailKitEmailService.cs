using MailKit.Net.Smtp; 
using MailKit.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using MimeKit;
using ZHSystem.Application.Common.Exceptions;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Infrastructure.Persistence.Models;
using Microsoft.Extensions.Configuration;

namespace ZHSystem.Infrastructure.Services
{
    
    public class MailKitEmailService : IEmailService
    {
        private readonly SmtpSettings _smtp;
        private readonly IConfiguration _configuration;
        public MailKitEmailService(IOptions<SmtpSettings> smtp)
        {
            _smtp = smtp.Value;
        }





        public async Task SendAsync(string destination, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _smtp.DisplayName ?? "ZHSystem System",
                    _smtp.From));

                message.To.Add(MailboxAddress.Parse(destination));
                message.Subject = subject;
                message.Body = new TextPart("html") { Text = htmlBody };
                using var client = new SmtpClient();

                await client.ConnectAsync(
                    _smtp.Host,
                    _smtp.Port,
                    SecureSocketOptions.StartTls);

                await client.AuthenticateAsync(
                    _smtp.User,
                    _smtp.Password);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch(Exception ex)
            {
                throw new EmailSendException($"Failed to send email to {destination}",ex);
            }
           
        }
        public async Task SendVerificationEmailAsync(string email, string name, string token)
        {
            var baseUrl = _configuration["ClientSettings:BaseUrl"];
            var verifyUrl = $"{baseUrl}/auth/verify-email?token={token}";

            var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; border: 1px solid #eee; padding: 20px;'>
                <h2>Welcome to ZHSystem, {name}!</h2>
                <p>Please verify your email by clicking the link below:</p>
                <div style='margin-top: 20px;'>
                    <a href='{verifyUrl}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Verify Email</a>
                </div>
                <p style='margin-top: 20px; font-size: 0.8em; color: #777;'>If the button doesn't work, copy and paste this link: <br/> {verifyUrl}</p>
            </div>";

            await SendAsync(email, "Verify your email", htmlBody);
        }
    }
}
