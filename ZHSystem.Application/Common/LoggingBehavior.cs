using MediatR;
using Microsoft.Extensions.Logging;
using ZHSystem.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZHSystem.Application.Common
{
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
     where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<TRequest> _logger;
        private readonly ICurrentUserService _currentUserService;

        public LoggingBehavior(ILogger<TRequest> logger, ICurrentUserService currentUserService) 
        { 
            _logger = logger;
            _currentUserService = currentUserService;
        }
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var timer = Stopwatch.StartNew();
        
            var userId = _currentUserService.UserId ?? "Anonymous";

            try
            {
              
                _logger.LogInformation("MediatR Request: {Name} | User: {UserId} {@Request}",
                    requestName, userId, request);

                var response = await next();

                timer.Stop();
               
                _logger.LogInformation("MediatR Handled: {Name} | User: {UserId} | Duration: {Duration}ms",
                    requestName, userId, timer.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                timer.Stop();
               
                _logger.LogError(ex, "MediatR Failure: {Name} | User: {UserId} | Duration: {Duration}ms",
                    requestName, userId, timer.ElapsedMilliseconds);
                throw;
            }
        }

       
    }
}
