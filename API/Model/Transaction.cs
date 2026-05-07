namespace API.Model
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public string FromWalletId { get; set; }
        public string ToWalletId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }  
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  
        
    }
}
