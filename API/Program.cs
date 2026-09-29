using API.Data;
using API.Model;
//using BCrypt.Net;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.Xml;
using System.Text;
using static API.Dtos.AuthDto;
using static API.Dtos.PaystackDto;
using static API.Dtos.TransactionDto;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddHttpClient<PaystackService>(client =>
        {
            var secretKey = builder.Configuration["Paystack:SecretKey"]
                ?? throw new InvalidOperationException("Paystack secret key not configured.");

            client.BaseAddress = new Uri(builder.Configuration["Paystack:BaseUrl"]
                ?? "https://api.paystack.co");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);
        });



        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<EmailService>();
        //builder.Services.AddScoped<>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
        )
    };
});

        builder.Services.AddAuthorization();




        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add before builder.Build()
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }







        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();








        app.MapPost("/Create-User", async (RegisterDto register, AppDbContext context, EmailService emailService) =>
        {
            if (register == null)
                return Results.BadRequest("Missing credentials");

            if (string.IsNullOrWhiteSpace(register.Email) ||
                string.IsNullOrWhiteSpace(register.FirstName) ||
                string.IsNullOrWhiteSpace(register.LastName) ||
                string.IsNullOrWhiteSpace(register.PassWord))
                return Results.BadRequest("Missing credentials");

            if (await context.UserAuths.AnyAsync(u => u.Email == register.Email))
                return Results.BadRequest("Email already exists");

            var passwordHashed = BCrypt.Net.BCrypt.HashPassword(register.PassWord);

            var newUser = new UserAuth
            {
                //Id = Guid.NewGuid(),
                FirstName = register.FirstName,
                LastName = register.LastName,
                Email = register.Email,
                PassWord = passwordHashed
               
            };

            await context.UserAuths.AddAsync(newUser);


            var newWallet = new Wallet
            {
                UserAuthId = newUser.Id, // <-- CRITICAL: Uses the auto-generated GUID from newUser
                ToEmail = newUser.Email,
                Balance = 0,
                Reference = null
            };


            await context.Wallets.AddAsync(newWallet);
           
            // ✅ Save both together in one transaction
            await context.SaveChangesAsync();


            await emailService.SendEmailAsync(register.Email, "SignUp successful", "Congratulations on your appointment");

            return Results.Created($"/UserAuth/{newUser.Id}", new
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




        app.MapPost("/Login", async (LoginDto login, AppDbContext context, IJwtService jwtService) =>
        {
            // 1. Validate request body
            if (login == null || string.IsNullOrWhiteSpace(login.Email) || string.IsNullOrWhiteSpace(login.PassWord))
            {
                return Results.BadRequest("Email and password are required.");
            }

            // 2. Query user from database
            var user = await context.UserAuths.FirstOrDefaultAsync(u => u.Email == login.Email);

            // 3. Verify user exists and password matches (Unified 401 response for security)
            if (user == null || string.IsNullOrEmpty(user.PassWord) || !BCrypt.Net.BCrypt.Verify(login.PassWord, user.PassWord))
            {
                return Results.Unauthorized(); // Returns HTTP 401 Unauthorized
            }

            // 4. Generate JWT
            var token = jwtService.GenerateToken(user);

            // 5. Return success response
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






        //app.MapPost("/Login", async (LoginDto login, AppDbContext context, IJwtService jwtService) =>
        //{
        //    //check that a user is in the database
        //    var user = await context.UserAuths.FirstOrDefaultAsync(u => u.Email == login.Email);
        //    if (user == null || user.PassWord == null)
        //        return Results.BadRequest("Invalid user");

        //    if (!BCrypt.Net.BCrypt.Verify(login.PassWord, user.PassWord))
        //        return Results.BadRequest("Wrong email or Paswword");

        //    var token = jwtService.GenerateToken(user);
        //    return Results.Ok(new { token });



        //});







        //  / Initialize a payment
        app.MapPost("/payments/initialize", async (
    InitializePaymentRequest request,AppDbContext context, 
    [FromServices]PaystackService paystack) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || request.Amount <= 0)
        return Results.BadRequest("Email and a positive Amount are required.");

    var referenceExist = await context.Transactionss.FirstOrDefaultAsync(x => x.Reference == request.Reference);


    if (referenceExist != null)
        throw new InvalidOperationException("reference already exist");

    var record = new Transaction
    {
      ToEmail = request.Email,
      Reference = request.Reference,
      Amount = request.Amount,
      Status = "Pending",
    };
    context.Transactionss.Add(record);
    await context.SaveChangesAsync();


    var result = await paystack.InitializeAsync(request);

    return result.Status
        ? Results.Ok(result)
        : Results.BadRequest(result.Message);
});






        app.MapPost("/payments/verify/{reference}", async (
            string reference,
            AppDbContext context,
            PaystackService paystack) =>
        {
            if (string.IsNullOrWhiteSpace(reference))
                return Results.BadRequest("Reference is required.");

            // ✅ Guard against null result from Paystack
            var result = await paystack.VerifyAsync(reference);

            if (result == null)
                return Results.Problem("Paystack returned no response.");

            if (!result.Status)
                return Results.BadRequest(result.Message ?? "Verification failed.");

            // ✅ Guard against null transaction
            var transaction = await context.Transactionss
                .FirstOrDefaultAsync(x => x.Reference == reference);

            if (transaction == null)
                return Results.NotFound("Transaction not found.");

            // ✅ Guard against null email on transaction
            if (string.IsNullOrWhiteSpace(transaction.ToEmail))
                return Results.Problem("Transaction has no associated email.");

            // ✅ Guard against null wallet
            var wallet = await context.Wallets
                .FirstOrDefaultAsync(x => x.ToEmail == transaction.ToEmail);

            if (wallet == null)
                return Results.NotFound("Wallet not found.");

            // ✅ Prevent double-funding if already verified
            if (transaction.Status == "Success")
                return Results.BadRequest("Transaction already verified.");

            wallet.Balance += transaction.Amount;
          

            transaction.Status = "Success";
            wallet.UpdatedAt = DateTime.UtcNow;
          
            await context.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Payment verified and wallet funded.",
                newBalance = wallet.Balance
            });
        });





        // Paystack webhook receiver
        app.MapPost("/payments/webhook", async (HttpRequest req, ILogger<Program> logger) =>
        {
            // Validate the Paystack signature
            var paystackSignature = req.Headers["x-paystack-signature"].ToString();
            var secretKey = builder.Configuration["Paystack:SecretKey"]!;

            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();

            var expectedHash = ComputeHmacSha512(secretKey, body);

            if (!expectedHash.Equals(paystackSignature, StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();

            // Log and handle the event
            logger.LogInformation("Paystack webhook received: {Body}", body);

            // TODO: parse event type and act (e.g. update DB on "charge.success")

            return Results.Ok();
        });




        app.MapPost("/payments/send", async (
    [FromBody] SendMoneyDto dto,
    AppDbContext context,
    HttpContext httpContext) =>
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(dto.ToEmail) || dto.Amount <= 0)
                return Results.BadRequest("Recipient email and a positive amount are required.");

            // Get the logged-in user's email from claims
            var senderEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrWhiteSpace(senderEmail))
                return Results.Unauthorized();

            // Prevent sending money to yourself
            if (senderEmail.Equals(dto.ToEmail, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("You cannot send money to yourself.");

            // Get sender's wallet
            var senderWallet = await context.Wallets
                .FirstOrDefaultAsync(x => x.ToEmail == senderEmail);

            if (senderWallet == null)
                return Results.NotFound("Sender wallet not found.");

            // Check sufficient balance
            if (senderWallet.Balance < dto.Amount)
                return Results.BadRequest("Insufficient balance.");

            // Get recipient's wallet
            var recipientWallet = await context.Wallets
                .FirstOrDefaultAsync(x => x.ToEmail == dto.ToEmail);

            if (recipientWallet == null)
                return Results.NotFound("Recipient wallet not found.");

            // Check for duplicate reference
            var referenceExists = await context.Transactionss
                .AnyAsync(x => x.Reference == dto.Reference);

            if (referenceExists)
                return Results.BadRequest("Reference already exists.");

            // Deduct from sender
            senderWallet.Balance -= dto.Amount;

            // Credit recipient
            recipientWallet.Balance += dto.Amount;
 
            // Record the transaction
            var transaction = new Transaction
            {
                Reference = dto.Reference,
                ToEmail = dto.ToEmail,
                Amount = dto.Amount,
                Status = "Success",
            };

            await context.Transactionss.AddAsync(transaction);
            await context.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Transfer successful.",
                reference = dto.Reference,
                amountSent = dto.Amount,
                recipientEmail = dto.ToEmail,
                senderNewBalance = senderWallet.Balance
            });
        });








        app.Run();

        // HMAC-SHA512 helper for webhook verification
        static string ComputeHmacSha512(string secret, string data)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
            var dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
            using var hmac = new System.Security.Cryptography.HMACSHA512(keyBytes);
            return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
        }














        //app.MapPost("/login", async (LoginDto dto, AppDbContext context, JwtService jwtService) =>
        //{
        //    var user = await context.UserAuths
        //        .FirstOrDefaultAsync(u => u.Email == dto.Email);

        //    if (user == null)
        //        return Results.Unauthorized();

        //    if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PassWordHashed))
        //        return Results.Unauthorized();

        //    var token = jwtService.GenerateToken(user);

        //    return Results.Ok(new { token });
        //});

        


    app.Run();
    }
}