//using System.Security.Cryptography;
//using API.Dtos;
//using System.Text;
//using System.Text.Json;
//using static API.Dtos.PaystackDto;

//namespace API.Services
//{
//    public class PayStackService(HttpClient http, IConfiguration config) : IPayStackService
//    {
//        private static readonly JsonSerialiZerOptions _json = new() {propertyNameCaseInsensitive = true };




//        public async Task<PayStackInitResponse?> InitializeTransaction(PayStackInitRequest req)
//        {
//            var response = await http.PostAsJsonAsync("transaction/initialize", req);
//            response.EnsureSuccessStatusCode();
//            return await response.Content.ReadFromJsonAsync<PayStackInitResponse> (_json);

//        }





//        public async Task<PayStackVerifyResponse?> VerifyResponse(string reference)
//        {
//            var response = await http.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}");
//            response.EnsureSuccessStatusCode();
//            return await response.Content.ReadFromJsonAsync<PayStackVerifyResponse> (_json);
//        }



//        public bool ValidateWebhookSignature(string payLoad, string payStackSignature)
//        {
//            var secretKey = config["paystack: SecretKey"]
//                ?? throw new InvalidOperationException("Paystack key not configured");
//                var keyBytes = Encoding.UTF8.GetBytes(secretKey); 
//            var msgBytes = Encoding.UTF8.GetBytes(payLoad);
//            using var hmac = new HMACSHA512(keyBytes);
//            var hash = hmac.ComputeHash(msgBytes);
//            var computed = Convert.ToHexString(hash).ToLowerInvariant();

//            return CryptographicOperations.FixedTimeEquals(
//                Encoding.UTF8.GetBytes(computed),
//                Encoding.UTF8.GetBytes(payStackSignature.ToLowerInvariant())
//                );

//        }



//    }
//}
