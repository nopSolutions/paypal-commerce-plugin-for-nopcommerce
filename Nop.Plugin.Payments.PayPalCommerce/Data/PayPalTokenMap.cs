using Nop.Data.Mapping;
using Nop.Plugin.Payments.PayPalCommerce.Domain;

namespace Nop.Plugin.Payments.PayPalCommerce.Data
{
    /// <summary>
    /// Represents the payment token mapping configuration
    /// </summary>
    public class PayPalTokenMap : NopEntityTypeConfiguration<PayPalToken>
    {
        #region Ctor

        public PayPalTokenMap()
        {
            ToTable(nameof(PayPalToken));
            HasKey(token => token.Id);
            Property(token => token.CustomerId).IsRequired();
            Property(token => token.VaultId).HasMaxLength(100);
            Property(token => token.VaultCustomerId).HasMaxLength(100);
            Property(token => token.TransactionId).HasMaxLength(100);
            Property(token => token.Title).HasMaxLength(200);
            Property(token => token.Expiration).HasMaxLength(100);
            Property(token => token.ClientId).HasMaxLength(200).IsRequired();
        }

        #endregion
    }
}