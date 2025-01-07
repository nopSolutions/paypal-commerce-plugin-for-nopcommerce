﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentMigrator;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.PayPalCommerce.Data
{
    [SkipMigrationOnInstall]
    [NopMigration("2024-06-06 00:00:01", "Payments.PayPalCommerce 4.70.10. Advanced cards")]
    public class AdvancedCardsMigration : MigrationBase
    {
        #region Fields

        private readonly IMigrationManager _migrationManager;

        #endregion

        #region Ctor

        public AdvancedCardsMigration(IMigrationManager migrationManager)
        {
            _migrationManager = migrationManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            if (!DataSettingsManager.IsDatabaseInstalled())
                return;

            if (!Schema.Table(nameof(PayPalToken)).Exists())
                _migrationManager.BuildTable<PayPalToken>(Create);

            var languageService = EngineContext.Current.Resolve<ILanguageService>();
            var localizationService = EngineContext.Current.Resolve<ILocalizationService>();
            var settingService = EngineContext.Current.Resolve<ISettingService>();
            var settings = settingService.LoadSettingAsync<PayPalCommerceSettings>().Result;

            var languages = languageService.GetAllLanguagesAsync(true).Result;
            var languageId = languages
                .FirstOrDefault(lang => lang.UniqueSeoCode == new CultureInfo(NopCommonDefaults.DefaultLanguageCulture).TwoLetterISOLanguageName)
                ?.Id;

            localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Cart"] = "Shopping cart",
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Product"] = "Product",
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.PaymentMethod"] = "Checkout",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Discount"] = "Discount",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Shipping"] = "Shipping",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Subtotal"] = "Subtotal",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Tax"] = "Tax",
                ["Plugins.Payments.PayPalCommerce.Card.Button"] = "Pay now with Card",
                ["Plugins.Payments.PayPalCommerce.Card.New"] = "Pay by new card",
                ["Plugins.Payments.PayPalCommerce.Card.Prefix"] = "Pay by",
                ["Plugins.Payments.PayPalCommerce.Card.Save"] = "Save your card",
                ["Plugins.Payments.PayPalCommerce.Configuration"] = "Configuration",
                ["Plugins.Payments.PayPalCommerce.Fields.ClientId.Required"] = "Client ID is required",
                ["Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired"] = "Use 3D Secure",
                ["Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired.Hint"] = "3D Secure enables you to authenticate card holders through card issuers. It reduces the likelihood of fraud when you use supported cards and improves transaction performance. A successful 3D Secure authentication can shift liability for chargebacks due to fraud from you to the card issuer.",
                ["Plugins.Payments.PayPalCommerce.Fields.MerchantId"] = "Merchant ID",
                ["Plugins.Payments.PayPalCommerce.Fields.MerchantId.Hint"] = "PayPal account ID of the merchant.",
                ["Plugins.Payments.PayPalCommerce.Fields.MerchantId.Required"] = "Merchant ID is required",
                ["Plugins.Payments.PayPalCommerce.Fields.SecretKey.Required"] = "Secret is required",
                ["Plugins.Payments.PayPalCommerce.Fields.SetCredentialsManually"] = "Specify API credentials manually",
                ["Plugins.Payments.PayPalCommerce.Fields.SetCredentialsManually.Hint"] = "Determine whether to manually set the credentials (for example, there is already the REST API application created, or if you want to use the sandbox mode).",
                ["Plugins.Payments.PayPalCommerce.Fields.SkipOrderConfirmPage"] = "Skip 'Confirm Order' page",
                ["Plugins.Payments.PayPalCommerce.Fields.SkipOrderConfirmPage.Hint"] = "Determine whether to skip the 'Confirm Order' step during checkout so that after approving the payment on PayPal site, customers will redirected directly to the 'Order Completed' page.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseAlternativePayments"] = "Use Alternative Payments Methods",
                ["Plugins.Payments.PayPalCommerce.Fields.UseAlternativePayments.Hint"] = "With alternative payment methods, customers across the globe can pay with their bank accounts, wallets, and other local payment methods.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseApplePay"] = "Use Apple Pay",
                ["Plugins.Payments.PayPalCommerce.Fields.UseApplePay.Hint"] = "Apple Pay is a mobile payment and digital wallet service provided by Apple Inc.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseApplePay.Warning"] = "Don't forget to enable 'Serve unknown types of static files' on the <a href=\"{0}\" target=\"_blank\">App settings page</a>, so that the domain association file is processed correctly.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseCardFields"] = "Use Custom Card Fields",
                ["Plugins.Payments.PayPalCommerce.Fields.UseCardFields.Hint"] = "Advanced Credit and Debit Card Payments (Custom Card Fields) are a PCI compliant solution to accept debit and credit card payments.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseGooglePay"] = "Use Google Pay",
                ["Plugins.Payments.PayPalCommerce.Fields.UseGooglePay.Hint"] = "Google Pay is a mobile payment and digital wallet service provided by Alphabet Inc.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking"] = "Use shipment tracking",
                ["Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking.Hint"] = "Determine whether to use the package tracking. It allows to automatically sync orders and shipment status with PayPal.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseVault"] = "Use Vault",
                ["Plugins.Payments.PayPalCommerce.Fields.UseVault.Hint"] = "Determine whether to use PayPal Vault. It allows to store buyers payment information and use it in subsequent transactions.",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Discount"] = "Discount",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Shipping"] = "Shipping",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Subtotal"] = "Subtotal",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Tax"] = "Tax",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Total"] = "Total",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Button.Sandbox"] = "Sign up for PayPal (sandbox)",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Process.Account.Success"] = "PayPal account is created",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Process.Email.Success"] = "Email address is confirmed",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Process.Payments.Success"] = "Billing information is set",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Sandbox"] = "After you finish testing the plugin in the PayPal sandbox, move it into the production environment so you can process live transactions. To take the plugin live: 1. Revoke access to the sandbox account, 2. Disable 'Use sandbox' setting, 3. Sign up for the live PayPal account.",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Title"] = "Connect PayPal account",
                ["Plugins.Payments.PayPalCommerce.Order.Adjustment.Name"] = "Adjustment item",
                ["Plugins.Payments.PayPalCommerce.Order.Adjustment.Description"] = "Used to adjust the order total amount when applying complex discounts or/and calculations",
                ["Plugins.Payments.PayPalCommerce.Order.Error"] = "Failed to get order details",
                ["Plugins.Payments.PayPalCommerce.Order.Id"] = "PayPal order ID",
                ["Plugins.Payments.PayPalCommerce.Order.Placement"] = "PayPal component placement",
                ["Plugins.Payments.PayPalCommerce.PaymentTokens"] = "Payment methods",
                ["Plugins.Payments.PayPalCommerce.PaymentTokens.Default"] = "Default",
                ["Plugins.Payments.PayPalCommerce.PaymentTokens.Expiration"] = "Expires",
                ["Plugins.Payments.PayPalCommerce.PaymentTokens.None"] = "No payment methods saved yet",
                ["Plugins.Payments.PayPalCommerce.PaymentTokens.MarkDefault"] = "Make default",
                ["Plugins.Payments.PayPalCommerce.PaymentTokens.Title"] = "Method",
                ["Plugins.Payments.PayPalCommerce.PayLater"] = "Pay Later",
                ["Plugins.Payments.PayPalCommerce.Shipment.Carrier"] = "Carrier",
                ["Plugins.Payments.PayPalCommerce.Shipment.Carrier.Hint"] = "Specify the carrier for the shipment (e.g. UPS or FEDEX_UK, see allowed values on PayPal site).",
            }, languageId).Wait();

            if (!settingService.SettingExistsAsync(settings, settings => settings.MerchantId).Result)
                settings.MerchantId = null;

            if (!settingService.SettingExistsAsync(settings, settings => settings.UseCardFields).Result)
                settings.UseCardFields = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.CustomerAuthenticationRequired).Result)
                settings.CustomerAuthenticationRequired = true;

            if (!settingService.SettingExistsAsync(settings, settings => settings.UseApplePay).Result)
                settings.UseApplePay = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.UseGooglePay).Result)
                settings.UseGooglePay = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.UseAlternativePayments).Result)
                settings.UseAlternativePayments = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.UseVault).Result)
                settings.UseVault = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.SkipOrderConfirmPage).Result)
                settings.SkipOrderConfirmPage = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.UseShipmentTracking).Result)
                settings.UseShipmentTracking = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.DisplayButtonsOnPaymentMethod).Result)
                settings.DisplayButtonsOnPaymentMethod = true;

            if (!settingService.SettingExistsAsync(settings, settings => settings.HideCheckoutButton).Result)
                settings.HideCheckoutButton = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.ImmediatePaymentRequired).Result)
                settings.ImmediatePaymentRequired = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.OrderValidityInterval).Result)
                settings.OrderValidityInterval = 300;

            if (!settingService.SettingExistsAsync(settings, settings => settings.ConfiguratorSupported).Result)
                settings.ConfiguratorSupported = false;

            if (!settingService.SettingExistsAsync(settings, settings => settings.PayLaterConfig).Result)
                settings.PayLaterConfig = null;

            if (!settingService.SettingExistsAsync(settings, settings => settings.MerchantIdRequired).Result)
                settings.MerchantIdRequired = false;

            try
            {
                if (settings.SetCredentialsManually)
                    settings.MerchantIdRequired = true;
                else if (!string.IsNullOrEmpty(settings.MerchantGuid))
                {
                    var serviceManager = EngineContext.Current.Resolve<PayPalCommerceServiceManager>();
                    if (serviceManager.IsActiveAsync(settings).Result.Active)
                    {
                        var httpClient = EngineContext.Current.Resolve<OnboardingHttpClient>();

                        settings.MerchantIdRequired = true;
                        settings.MerchantId = httpClient.GetMerchantAsync(settings.MerchantGuid).Result?.MerchantId;
                        settings.MerchantIdRequired = string.IsNullOrEmpty(settings.MerchantId);
                    }
                }
            }
            catch { }

            settingService.SaveSettingAsync(settings).Wait();
        }

        /// <summary>
        /// Collects the DOWN migration expressions
        /// </summary>
        public override void Down()
        {
        }

        #endregion
    }
}