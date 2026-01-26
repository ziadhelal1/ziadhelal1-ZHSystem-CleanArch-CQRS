using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using ZHSystem.Application.Common;
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.DTOs.Auth;
using ZHSystem.Application.Features.Auth.Commands;
using ZHSystem.Application.Features.Users.Commands;
using ZHSystem.Domain.Entities;


namespace ZHSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IApplicationDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly JwtSettings _jwt;
        private readonly IMediator _mediator;

        public AuthController(IApplicationDbContext db, 
            ITokenService tokenService, IPasswordHasher<User> passwordHasher, 
            IOptions<JwtSettings> jwt,IMediator mediator)
        {
            _db = db;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _jwt = jwt.Value;
            _mediator = mediator;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _mediator.Send(new RegisterCommand(dto));
            return Ok(result);
        }

        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _mediator.Send(new LoginCommand(dto));
            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
        {
            var result = await _mediator.Send(
                new RefreshTokenCommand(request.RefreshToken));
            return Ok(result);
        }
        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RefreshRequestDto dto)
        {
            await _mediator.Send(
                new RevokeRefreshTokenCommand(dto.RefreshToken));

            return NoContent();
        }
        [Authorize]

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            await _mediator.Send(new VerifyEmailCommand(token));
            return Ok("Email verified successfully");
        }
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] MailFeatureDto dto)
        {
            await _mediator.Send(
                new ForgotPasswordCommand(dto.Email));

            return Ok("If email exists, reset link sent");
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordDto dto)
        {
            await _mediator.Send(
                new ResetPasswordCommand(dto.Token, dto.NewPassword));

            return Ok("Password reset successfully");
        }
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerificationEmail(
            [FromBody] MailFeatureDto dto)
        {
            await _mediator.Send(
                new ResendVerificationEmailCommand(dto.Email)
            );

            return Ok("If the email exists and is not verified, a verification email has been sent.");
        }
        [HttpPost("auth/google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequestDto dto)
        {
            await _mediator.Send(new LogoutCommand(dto.RefreshToken));
            return NoContent();
        }
    }
}
