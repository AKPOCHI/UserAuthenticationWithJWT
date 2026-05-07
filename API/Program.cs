using API.Data;
using API.Dtos;
using API.Model;
//using BCrypt.Net;
using API.Services;
using Microsoft.EntityFrameworkCore;
using static API.Dtos.AuthDto;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<EmailService>();
        //builder.Services.AddScoped<>();




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







        app.MapPost("/UserAuth", async (RegisterDto register, AppDbContext context, EmailService emailService) =>
        {
            // Null check first before anything else
            if (register == null)
                return Results.BadRequest("Missing credentials");

            if (string.IsNullOrWhiteSpace(register.Email) ||
                string.IsNullOrWhiteSpace(register.FirstName) ||
                string.IsNullOrWhiteSpace(register.LastName) ||
                string.IsNullOrWhiteSpace(register.PassWord))
                return Results.BadRequest("Missing credentials");

            // Check email is not already taken
            if (await context.UserAuths.AnyAsync(u => u.Email == register.Email)) // fix: was context.Email
                return Results.BadRequest("Email already exists");



            // Hash the password
            var passwordHashed = BCrypt.Net.BCrypt.HashPassword(register.PassWord);

            // Map DTO to entity manually — EF cannot add a DTO directly
            var newUser = new UserAuth
            {
                Id = Guid.NewGuid(),
                FirstName = register.FirstName,
                LastName = register.LastName,
                Email = register.Email,
                EmailVerificationStatus = "Unverified",
                ResetToken = null,
                ResetTokenExpiry = null,
                PassWord = passwordHashed  // save the hash, not the raw password
            };

            await context.UserAuths.AddAsync(newUser);
            await context.SaveChangesAsync();

            await emailService.SendEmailAsync(register.Email, "SignUp successful", "Congratulations on your appointment");

            return Results.Created($"/UserAuth/{newUser.Id}", new
            {
                newUser.Id,
                newUser.FirstName,
                newUser.LastName,
                newUser.Email,
                newUser.EmailVerificationStatus
            });

           
        });









        app.MapPost("/Login", async (LoginDto login, AppDbContext context, JwtService jwtService) =>
        {
            //check that a user is in the database
            var user = await context.UserAuths.FirstOrDefaultAsync(u => u.Email == login.Email);
            if (user == null || user.PassWord == null)
                return Results.BadRequest("Invalid user");

            if (!BCrypt.Net.BCrypt.Verify(login.PassWord, user.PassWord))
                return Results.BadRequest("Wrong email or Paswword");

            var token = jwtService.GenerateToken(user);
            return Results.Ok(new { token });



        });

        

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