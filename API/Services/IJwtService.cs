using API.Model;

namespace API.Services
{
    public interface IJwtService
    {
       public string GenerateToken(UserAuth userLogin);
    }
}
