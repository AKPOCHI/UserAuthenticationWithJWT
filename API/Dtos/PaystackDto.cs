namespace API.Dtos;
//namespace PaystackApi;

    public class PaystackDto
    {


    public record InitializePaymentRequest(
        string Email,
        decimal Amount, // in Naira — converted to kobo internally
        string? Reference = null,
        string? CallbackUrl = null,
        Dictionary<string, string>? Metadata = null
    );

    public record InitializePaymentResponse(
        bool Status,
        string Message,
        string? AuthorizationUrl,
        string? AccessCode,
        string? Reference
    );

    public record VerifyPaymentResponse(
        bool Status,
        string Message,
        string? PaymentStatus,  // "success", "failed", "abandoned"
        decimal? Amount,        // in Naira
        string? Reference,
        string? Email
    );

    public record PaystackApiResponse<T>
    {
        public bool Status { get; init; }
        public string Message { get; init; } = string.Empty;
        public T? Data { get; init; }
    }
}

