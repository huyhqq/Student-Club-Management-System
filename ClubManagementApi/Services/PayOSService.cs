using Net.payOS.Types;
using Net.payOS;

namespace ClubManagementApi.Services
{
    public class PayOSService
    {
        private readonly PayOS _payOS;

        public PayOSService(IConfiguration configuration)
        {
            string clientId = configuration["PayOS:ClientId"];
            string apiKey = configuration["PayOS:ApiKey"];
            string checksumKey = configuration["PayOS:ChecksumKey"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(checksumKey))
            {
                throw new ArgumentException("PayOS configuration is missing or invalid");
            }

            _payOS = new PayOS(clientId, apiKey, checksumKey);
        }

        public async Task<CreatePaymentResult> CreatePaymentLink(int orderCode, decimal amount, string description,
            List<ItemData> items, string cancelUrl, string returnUrl)
        {
            PaymentData paymentData = new PaymentData(
                orderCode,
                (int)(amount),
                description,
                items,
                cancelUrl,
                returnUrl
            );

            return await _payOS.createPaymentLink(paymentData);
        }

        public async Task<PaymentLinkInformation> GetPaymentLinkInformation(long orderCode)
        {
            try
            {
                PaymentLinkInformation paymentLinkInfo = await _payOS.getPaymentLinkInformation(orderCode);
                return paymentLinkInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get payment link information for orderCode {orderCode}: {ex.Message}", ex);
            }
        }
    }
}
