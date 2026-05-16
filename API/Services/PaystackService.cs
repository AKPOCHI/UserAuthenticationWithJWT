using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static API.Dtos.PaystackDto;


namespace API.Services
{
   

    public class PaystackService(HttpClient httpClient, IConfiguration config)
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task<InitializePaymentResponse> InitializeAsync(InitializePaymentRequest request)
        {
            var body = new
            {
                email = request.Email,
                amount = (long)(request.Amount * 100), // Naira → kobo
                reference = request.Reference ?? GenerateReference(),
                callback_url = request.CallbackUrl,
                metadata = request.Metadata
            };


            var response = await PostAsync<PaystackApiResponse<InitializeData>>(
                "/transaction/initialize", body);


            return new InitializePaymentResponse(
                Status: response.Status,
                Message: response.Message,
                AuthorizationUrl: response.Data?.AuthorizationUrl,
                AccessCode: response.Data?.AccessCode,
                Reference: response.Data?.Reference
            );
        }

        public async Task<VerifyPaymentResponse> VerifyAsync(string reference)
        {
            var response = await GetAsync<PaystackApiResponse<VerifyData>>(
                $"/transaction/verify/{reference}");

            return new VerifyPaymentResponse(
                Status: response.Status,
                Message: response.Message,
                PaymentStatus: response.Data?.Status,
                Amount: response.Data?.Amount is long amt ? amt / 100m : null, // kobo → Naira
                Reference: response.Data?.Reference,
                Email: response.Data?.Customer?.Email
            );
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private async Task<T> PostAsync<T>(string path, object body)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await httpClient.PostAsync(path, content);
            return await DeserializeAsync<T>(res);
        }

        private async Task<T> GetAsync<T>(string path)
        {
            var res = await httpClient.GetAsync(path);
            return await DeserializeAsync<T>(res);
        }

        private static async Task<T> DeserializeAsync<T>(HttpResponseMessage res)
        {
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidOperationException("Empty response from Paystack");
        }

        private static string GenerateReference() =>
            $"ref_{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // Internal DTOs — only used for deserialization
        private record InitializeData(
            [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
            [property: JsonPropertyName("access_code")] string AccessCode,
            string Reference
        );

        private record VerifyData(
            string Status,
            long Amount,
            string Reference,
            VerifyCustomer? Customer
        );

        private record VerifyCustomer(string Email);
    }


}

