namespace API.Dtos
{
    public class TransactionDto
    {
        public record FundWalletDto(decimal Amount, string ToEmail ,string Status,string Reference);
        public record SendMoneyDto(string ToEmail, decimal Amount, string Status, string Reference);
        public record TansferResponseDto(string message, decimal newBalance);
    }
}





