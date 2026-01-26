
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZHSystem.Api.Middleware;
using ZHSystem.Application;           
using ZHSystem.Application.Common.Interfaces;
using ZHSystem.Application.Validators;
using ZHSystem.Domain.Entities;
using ZHSystem.Infrastructure;      
using ZHSystem.Infrastructure.Extensions;
using ZHSystem.Infrastructure.Persistence;
using ZHSystem.Infrastructure.Persistence.Seed;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;





namespace ZHSystem.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();


        try
        {


            var builder = WebApplication.CreateBuilder(args);


            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter =
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        var partitionKey =
                            httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous";

                        return RateLimitPartition.GetTokenBucketLimiter(
                            partitionKey,
                            _ => new TokenBucketRateLimiterOptions
                            {
                                TokenLimit = 5,                 // Bucket capacity
                                TokensPerPeriod = 5,            // Tokens refilled
                                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                                AutoReplenishment = true,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0                    // No waiting
                            });
                    });

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsync(
                        "Too many requests. Please try again later.",
                        cancellationToken);
                };
            });


            builder.Host.UseSerilog((ctx, lc) => lc
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console()
                .WriteTo.Seq("http://localhost:5341")
                .WriteTo.File("Logs/audit-.log", rollingInterval: RollingInterval.Day));

            builder.Services.AddControllers();

            // Register FluentValidation auto-validation
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddFluentValidationClientsideAdapters();

            // Register all validators from Application assembly
            // 

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZHSystem API", Version = "v1" });


                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter ONLY your JWT token", 
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer", 
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer" 
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
         // These two lines replace 50+ lines of manual registration
        builder.Services.AddApplication();                                   //  MediatR, AutoMapper, Validators, etc.
        builder.Services.AddInfrastructure(builder.Configuration);             // DbContext, Repositories, EmailService, etc.


            // Optional – JWT (you’ll need it soon)
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
                        ),
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.Name
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Log.Error(" Auth Failed: {Message}", context.Exception.Message);
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            
                            Log.Warning(" {Error}, {Description}", context.Error, context.ErrorDescription);
                            return Task.CompletedTask;
                        },
                       
                    };
                    
                });


            builder.Services.AddAuthorization();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

          
       
            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
                await initializer.SeedAsync();
            }

            #region Old Swagger
            // Pipeline
            //if (app.Environment.IsDevelopment())
            //{
            //    app.UseSwagger();
            //    app.UseSwaggerUI();
            //} 
            #endregion
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = "swagger";    
            });
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();   
            app.UseRateLimiter();
            app.UseAuthorization();
            app.UseMiddleware<AuditLoggingMiddleware>();    

            app.MapControllers();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}