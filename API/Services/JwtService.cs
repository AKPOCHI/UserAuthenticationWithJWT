using API.Model;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace API.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;
        public JwtService(IConfiguration config)
        {
            _config = config;
        }
        //jwt service setup
       public string GenerateToken(UserAuth userAuth)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier,userAuth.Id.ToString()),
                new Claim(ClaimTypes.Email,userAuth.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires:DateTime.UtcNow.AddDays(7),
                signingCredentials: creds

                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        string IJwtService.GenerateToken(UserAuth userAuth)
        {
            return GenerateToken(userAuth);
        }
    }
}
