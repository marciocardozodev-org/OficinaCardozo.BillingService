using System;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public class MercadoPagoOptions
    {
        public string AccessToken { get; set; } = string.Empty;
        public bool IsSandbox { get; set; } = true;
        public string TestEmail { get; set; } = "test@example.com";
        public string TestCardToken { get; set; } = string.Empty;
    }
}
