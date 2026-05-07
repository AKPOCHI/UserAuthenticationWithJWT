namespace API.Model
{
    public class Wallet
    {
        public Guid Id { get; set; }  
        public Guid UserAuthId { get; set; }    
        public decimal Balance { get; set; }    
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;  
        public UserAuth? UserAuth { get; set; } 

    }
}
