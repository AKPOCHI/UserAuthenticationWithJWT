
namespace API.Model
{
    public class UserAuth
    {
        public string Email { get; set; }
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailVerificationStatus { get; set; }
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }  // nullable is correct
        public string PassWord { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Wallet? Wallet { get; set; }
    
    // public string Provider { get; set; }
    //public string ProviderId { get; set; }
   }
}
