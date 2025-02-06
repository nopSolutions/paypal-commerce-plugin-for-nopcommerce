using System.Collections.Generic;
using Nop.Plugin.Payments.PayPalCommerce.Domain;

namespace Nop.Plugin.Payments.PayPalCommerce.Models.Public
{
    /// <summary>
    /// Represents the Google Pay model
    /// </summary>
    public class GooglePayModel : OrderModel
    {
        #region Properties

        public ButtonPlacement Placement { get; set; }

        public string Country { get; set; }

        public string CurrencyCode { get; set; }

        public bool ShippingIsRequired { get; set; }

        public List<(string Type, string Price, string Status, string Label)> Items { get; set; } = new List<(string, string, string, string)>();

        #endregion
    }
}