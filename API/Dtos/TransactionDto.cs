namespace API.Dtos
{
    public class TransactionDto
    {
        public record FundWalletDto(decimal amount);
        public record SendMoneyDto(string reciepientEmail, decimal amount);
        public record TansferResponseDto(string message, decimal newBalance);
    }
}





