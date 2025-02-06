namespace Nop.Plugin.Payments.PayPalCommerce.Models.Public
{
    /// <summary>
    /// Represents the order completed model
    /// </summary>
    public class OrderCompletedModel : OrderModel
    {
        public string Warning { get; set; }
    }
}