using System.Collections.Generic;
using Nop.Plugin.Payments.PayPalCommerce.Domain;

namespace Nop.Plugin.Payments.PayPalCommerce.Models.Public
{
    /// <summary>
    /// Represents the Apple Pay model
    /// </summary>
    public class ApplePayModel : OrderModel
    {
        #region Properties

        public ButtonPlacement Placement { get; set; }

        public string CurrencyCode { get; set; }

        public Contact BillingAddress { get; set; } = new Contact();

        public Contact ShippingAddress { get; set; } = new Contact();

        public List<(string Id, string Label, string Description, string Price)> ShippingOptions { get; set; } = new List<(string, string, string, string)>();

        public List<(string Type, string Price, string Status, string Label)> Items { get; set; } = new List<(string, string, string, string)>();

        #endregion
    }
}