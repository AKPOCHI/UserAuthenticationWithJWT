namespace API.Model
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public string? FromEmail { get; set; }
        public string ToEmail { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }  
        public string Reference { get; set; }   
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  
        
    }
}
