namespace API.Dtos
{
    public class AuthDto
    {
        public record RegisterDto(string FirstName, string LastName, string Email,string PassWord);
               
        
        public record LoginDto(string Email, string PassWord);
        public record ForGotPassWord(string Email);
        public record ResetPassWord(string Email, string Token,string NewPassWord);








       
       

       
    }
}
