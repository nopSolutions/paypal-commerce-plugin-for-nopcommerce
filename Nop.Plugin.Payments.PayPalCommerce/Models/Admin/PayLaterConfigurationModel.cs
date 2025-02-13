using Nop.Web.Framework.Mvc.Models;

namespace Nop.Plugin.Payments.PayPalCommerce.Models.Admin
{
    /// <summary>
    /// Represents the Pay Later configuration model
    /// </summary>
    public class PayLaterConfigurationModel : BaseNopModel
    {
        public string ClientId { get; set; }

        public bool UseSandbox { get; set; }

        public string Locale { get; set; }

        public string Config { get; set; }
    }
}