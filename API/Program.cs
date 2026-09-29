using API.Data;
using API.Model;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using static API.Dtos.AuthDto;
using static API.Dtos.PaystackDto;
using static API.Dtos.TransactionDto;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // =========================================================
        // SERVICES
        // =========================================================

        // Paystack HttpClient
        builder.Services.AddHttpClient<PaystackService>(client =>
        {
            var secretKey = builder.Configuration["Paystack:SecretKey"]
                ?? throw new InvalidOperationException(
                    "Paystack secret key not configured.");

            client.BaseAddress = new Uri(
                builder.Configuration["Paystack:BaseUrl"]
                ?? "https://api.paystack.co");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);
        });

        // Database
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("DefaultConnection")));

        // Application services
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<EmailService>();


        // =========================================================
        // JWT AUTHENTICATION
        // =========================================================

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                JwtBearerDefaults.AuthenticationScheme;

            options.DefaultChallengeScheme =
                JwtBearerDefaults.AuthenticationScheme;
        })
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
                    Encoding.UTF8.GetBytes(
                        builder.Configuration["Jwt:Key"]!))
            };
        });

        // Authorization
        builder.Services.AddAuthorization();


        // =========================================================
        // SWAGGER / OPENAPI
        // =========================================================

        builder.Services.AddOpenApi();

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options =>
        {
            // Defines the Bearer authentication scheme
            options.AddSecurityDefinition("Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description =
                        "Enter your JWT token"
                });

            // Applies Bearer authentication to Swagger operations
            options.AddSecurityRequirement(document =>
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(
                        "Bearer",
                        document)] = new List<string>()
                });
        });


        // =========================================================
        // POSTGRESQL TIMESTAMP COMPATIBILITY
        // =========================================================

        AppContext.SetSwitch(
            "Npgsql.EnableLegacyTimestampBehavior",
            true);


        var app = builder.Build();


        // =========================================================
        // HTTP REQUEST PIPELINE
        // =========================================================

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        // Authentication MUST come before Authorization
        app.UseAuthentication();
        app.UseAuthorization();


        // =========================================================
        // CREATE USER
        // =========================================================

        app.MapPost("/Create-User",
            async (
                RegisterDto register,
                AppDbContext context,
                EmailService emailService) =>
            {
                if (register == null)
                    return Results.BadRequest(
                        "Missing credentials");

                if (string.IsNullOrWhiteSpace(register.Email) ||
                    string.IsNullOrWhiteSpace(register.FirstName) ||
                    string.IsNullOrWhiteSpace(register.LastName) ||
                    string.IsNullOrWhiteSpace(register.PassWord))
                {
                    return Results.BadRequest(
                        "Missing credentials");
                }

                if (await context.UserAuths
                    .AnyAsync(u => u.Email == register.Email))
                {
                    return Results.BadRequest(
                        "Email already exists");
                }

                var passwordHashed =
                    BCrypt.Net.BCrypt.HashPassword(
                        register.PassWord);

                var newUser = new UserAuth
                {
                    FirstName = register.FirstName,
                    LastName = register.LastName,
                    Email = register.Email,
                    PassWord = passwordHashed
                };

                await context.UserAuths.AddAsync(newUser);


                // Create wallet for the new user
                var newWallet = new Wallet
                {
                    UserAuthId = newUser.Id,
                    ToEmail = newUser.Email,
                    Balance = 0,
                    Reference = null
                };

                await context.Wallets.AddAsync(newWallet);

                // Save user + wallet
                await context.SaveChangesAsync();


                await emailService.SendEmailAsync(
                    register.Email,
                    "SignUp successful",
                    "Congratulations on your appointment");


                return Results.Created(
                    $"/UserAuth/{newUser.Id}",
                    new
                    {
                        newUser.Id,
                        newUser.FirstName,
                        newUser.LastName,
                        newUser.Email,
                        newUser.EmailVerificationStatus,

                        WalletId = newWallet.Id,

                        newWallet.Balance
                    });
            });


        // =========================================================
        // LOGIN
        // =========================================================

        app.MapPost("/Login",
            async (
                LoginDto login,
                AppDbContext context,
                IJwtService jwtService) =>
            {
                // Validate request
                if (login == null ||
                    string.IsNullOrWhiteSpace(login.Email) ||
                    string.IsNullOrWhiteSpace(login.PassWord))
                {
                    return Results.BadRequest(
                        "Email and password are required.");
                }


                // Find user
                var user = await context.UserAuths
                    .FirstOrDefaultAsync(
                        u => u.Email == login.Email);


                // Verify credentials
                if (user == null ||
                    string.IsNullOrEmpty(user.PassWord) ||
                    !BCrypt.Net.BCrypt.Verify(
                        login.PassWord,
                        user.PassWord))
                {
                    return Results.Unauthorized();
                }


                // Generate JWT
                var token =
                    jwtService.GenerateToken(user);


                // Return token
                return Results.Ok(new
                {
                    token,

                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName
                    }
                });
            });


        // =========================================================
        // INITIALIZE PAYMENT
        // =========================================================

        app.MapPost("/payments/initialize",
     async (
         InitializePaymentRequest request,
         AppDbContext context,
         [FromServices] PaystackService paystack) =>
     {
         if (string.IsNullOrWhiteSpace(request.Email) ||
             request.Amount <= 0)
         {
             return Results.BadRequest(
                 "Email and a positive Amount are required.");
         }

         // Check duplicate reference
         var referenceExists =
             await context.Transactionss
                 .AnyAsync(x =>
                     x.Reference == request.Reference);

         if (referenceExists)
         {
             return Results.BadRequest(
                 "Reference already exists.");
         }

         // Create transaction
         var record = new Transaction
         {
             ToEmail = request.Email,
             Reference = request.Reference,
             Amount = request.Amount,
             Status = "Pending"
         };

         context.Transactionss.Add(record);

         await context.SaveChangesAsync();

         // Initialize Paystack payment
         var result =
             await paystack.InitializeAsync(request);

         return result.Status
             ? Results.Ok(result)
             : Results.BadRequest(result.Message);
     });

        // =========================================================
        // VERIFY PAYMENT
        // =========================================================

        app.MapPost("/payments/verify/{reference}",
            async (
                string reference,
                AppDbContext context,
                PaystackService paystack) =>
            {
                if (string.IsNullOrWhiteSpace(reference))
                    return Results.BadRequest(
                        "Reference is required.");


                // Verify with Paystack
                var result =
                    await paystack.VerifyAsync(reference);


                if (result == null)
                {
                    return Results.Problem(
                        "Paystack returned no response.");
                }


                if (!result.Status)
                {
                    return Results.BadRequest(
                        result.Message ??
                        "Verification failed.");
                }


                // Find transaction
                var transaction =
                    await context.Transactionss
                        .FirstOrDefaultAsync(
                            x => x.Reference == reference);


                if (transaction == null)
                    return Results.NotFound(
                        "Transaction not found.");


                if (string.IsNullOrWhiteSpace(
                    transaction.ToEmail))
                {
                    return Results.Problem(
                        "Transaction has no associated email.");
                }


                // Find wallet
                var wallet =
                    await context.Wallets
                        .FirstOrDefaultAsync(
                            x => x.ToEmail == transaction.ToEmail);


                if (wallet == null)
                    return Results.NotFound(
                        "Wallet not found.");


                // Prevent duplicate funding
                if (transaction.Status == "Success")
                {
                    return Results.BadRequest(
                        "Transaction already verified.");
                }


                // Fund wallet
                wallet.Balance += transaction.Amount;

                transaction.Status = "Success";

                wallet.UpdatedAt = DateTime.UtcNow;


                await context.SaveChangesAsync();


                return Results.Ok(new
                {
                    message =
                        "Payment verified and wallet funded.",

                    newBalance = wallet.Balance
                });
            });


        // =========================================================
        // PAYSTACK WEBHOOK
        // =========================================================

        app.MapPost("/payments/webhook",
            async (
                HttpRequest req,
                ILogger<Program> logger) =>
            {
                // Get Paystack signature
                var paystackSignature =
                    req.Headers["x-paystack-signature"]
                        .ToString();

                var secretKey =
                    builder.Configuration[
                        "Paystack:SecretKey"]!;


                // Read request body
                using var reader =
                    new StreamReader(req.Body);

                var body =
                    await reader.ReadToEndAsync();


                // Generate expected signature
                var expectedHash =
                    ComputeHmacSha512(
                        secretKey,
                        body);


                // Validate signature
                if (!expectedHash.Equals(
                        paystackSignature,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Unauthorized();
                }


                logger.LogInformation(
                    "Paystack webhook received: {Body}",
                    body);


                // TODO:
                // Parse event type and process event
                // e.g. charge.success


                return Results.Ok();
            });


        // =========================================================
        // SEND MONEY
        // =========================================================

        app.MapPost("/payments/send",
            async (
                [FromBody] SendMoneyDto dto,
                AppDbContext context,
                HttpContext httpContext) =>
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(dto.ToEmail) ||
                    dto.Amount <= 0)
                {
                    return Results.BadRequest(
                        "Recipient email and a positive amount are required.");
                }


                // =====================================================
                // GET LOGGED-IN USER FROM JWT
                // =====================================================

                var senderEmail =
                    httpContext.User
                        .FindFirst(ClaimTypes.Email)
                        ?.Value;


                if (string.IsNullOrWhiteSpace(senderEmail))
                    return Results.Unauthorized();


                // Prevent sending money to yourself
                if (senderEmail.Equals(
                        dto.ToEmail,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(
                        "You cannot send money to yourself.");
                }


                // Find sender wallet
                var senderWallet =
                    await context.Wallets
                        .FirstOrDefaultAsync(
                            x => x.ToEmail == senderEmail);


                if (senderWallet == null)
                {
                    return Results.NotFound(
                        "Sender wallet not found.");
                }


                // Check balance
                if (senderWallet.Balance < dto.Amount)
                {
                    return Results.BadRequest(
                        "Insufficient balance.");
                }


                // Find recipient wallet
                var recipientWallet =
                    await context.Wallets
                        .FirstOrDefaultAsync(
                            x => x.ToEmail == dto.ToEmail);


                if (recipientWallet == null)
                {
                    return Results.NotFound(
                        "Recipient wallet not found.");
                }


                // Check duplicate reference
                var referenceExists =
                    await context.Transactionss
                        .AnyAsync(
                            x => x.Reference == dto.Reference);


                if (referenceExists)
                {
                    return Results.BadRequest(
                        "Reference already exists.");
                }


                // Deduct from sender
                senderWallet.Balance -= dto.Amount;


                // Credit recipient
                recipientWallet.Balance += dto.Amount;


                // Record transaction
                var transaction = new Transaction
                {
                    Reference = dto.Reference,
                    ToEmail = dto.ToEmail,
                    Amount = dto.Amount,
                    Status = "Success"
                };

                await context.Transactionss.AddAsync(
                    transaction);


                await context.SaveChangesAsync();


                return Results.Ok(new
                {
                    message = "Transfer successful.",

                    reference = dto.Reference,

                    amountSent = dto.Amount,

                    recipientEmail = dto.ToEmail,

                    senderNewBalance =
                        senderWallet.Balance
                });
            })
            .RequireAuthorization();


        // =========================================================
        // RUN APPLICATION
        // =========================================================

        app.Run();


        // =========================================================
        // PAYSTACK HMAC SHA512
        // =========================================================

        static string ComputeHmacSha512(
            string secret,
            string data)
        {
            var keyBytes =
                Encoding.UTF8.GetBytes(secret);

            var dataBytes =
                Encoding.UTF8.GetBytes(data);

            using var hmac =
                new System.Security.Cryptography.HMACSHA512(
                    keyBytes);

            return Convert.ToHexString(
                hmac.ComputeHash(dataBytes))
                .ToLower();
        }
    }
}