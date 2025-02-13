using Nop.Web.Framework.Mvc.Models;

namespace Nop.Plugin.Payments.PayPalCommerce.Models.Admin
{
    /// <summary>
    /// Represents the authentication model
    /// </summary>
    public class AuthenticationModel : BaseNopModel
    {
        #region Properties

        public int StoreId { get; set; }

        public string SharedId { get; set; }

        public string AuthCode { get; set; }

        #endregion
    }
}