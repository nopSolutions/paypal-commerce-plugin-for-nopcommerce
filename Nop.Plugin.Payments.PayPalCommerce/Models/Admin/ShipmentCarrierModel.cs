using Nop.Web.Framework.Mvc.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayPalCommerce.Models.Admin
{
    /// <summary>
    /// Represents the shipment carrier model
    /// </summary>
    public class ShipmentCarrierModel : BaseNopEntityModel
    {
        #region Properties

        [NopResourceDisplayName("Plugins.Payments.PayPalCommerce.Shipment.Carrier")]
        public string PayPalCommerceShipmentCarrier { get; set; }

        public string TrackingNumber { get; set; }

        #endregion
    }
}