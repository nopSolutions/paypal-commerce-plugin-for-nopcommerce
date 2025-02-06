using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nop.Data.Mapping;
using Nop.Plugin.Payments.PayPalCommerce.Domain;

namespace Nop.Plugin.Payments.PayPalCommerce.Data
{
    /// <summary>
    /// Represents the payment token mapping configuration
    /// </summary>
    public class PayPalTokenMap : NopEntityTypeConfiguration<PayPalToken>
    {
        #region Methods

        /// <summary>
        /// Configures the entity
        /// </summary>
        public override void Configure(EntityTypeBuilder<PayPalToken> builder)
        {
            builder.ToTable(nameof(PayPalToken));
            builder.HasKey(token => token.Id);
            builder.Property(token => token.CustomerId).IsRequired();
            builder.Property(token => token.VaultId).HasMaxLength(100);
            builder.Property(token => token.VaultCustomerId).HasMaxLength(100);
            builder.Property(token => token.TransactionId).HasMaxLength(100);
            builder.Property(token => token.Title).HasMaxLength(200);
            builder.Property(token => token.Expiration).HasMaxLength(100);
            builder.Property(token => token.ClientId).HasMaxLength(200).IsRequired();
        }

        #endregion
    }
}