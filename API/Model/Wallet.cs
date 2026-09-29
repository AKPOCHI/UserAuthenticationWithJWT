namespace API.Model
{
    public class Wallet
    {
        public Guid Id { get; set; }  = Guid.NewGuid();
        public Guid UserAuthId { get; set; }   
        public string? ToEmail { get; set; } 
        public decimal Balance { get; set; } = decimal.Zero;    
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;  
        public UserAuth? UserAuth { get; set; }
        public string? Reference { get; internal set; }
    }
}
