using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Payments.PayPalCommerce
{
    /// <summary>
    /// Represents the PayPal Commerce payment method
    /// </summary>
    public class PayPalCommercePaymentMethod : BasePlugin, IAdminMenuPlugin, IPaymentMethod, IWidgetPlugin
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWorkContext _workContext;
        private readonly PaymentSettings _paymentSettings;
        private readonly PayPalCommerceServiceManager _serviceManager;
        private readonly PayPalCommerceSettings _settings;
        private readonly WidgetSettings _widgetSettings;

        #endregion

        #region Ctor

        public PayPalCommercePaymentMethod(IActionContextAccessor actionContextAccessor,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreService storeService,
            IUrlHelperFactory urlHelperFactory,
            IWorkContext workContext,
            PaymentSettings paymentSettings,
            PayPalCommerceServiceManager serviceManager,
            PayPalCommerceSettings settings,
            WidgetSettings widgetSettings)
        {
            _actionContextAccessor = actionContextAccessor;
            _localizationService = localizationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeService = storeService;
            _urlHelperFactory = urlHelperFactory;
            _workContext = workContext;
            _paymentSettings = paymentSettings;
            _serviceManager = serviceManager;
            _settings = settings;
            _widgetSettings = widgetSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>The process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>The capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            //capture previously authorized payment
            var (capture, error) = _serviceManager.CaptureAuthorization(_settings, capturePaymentRequest.Order.AuthorizationTransactionId);
            if (!string.IsNullOrEmpty(error))
                return new CapturePaymentResult { Errors = new[] { error } };

            //request succeeded
            return new CapturePaymentResult
            {
                CaptureTransactionId = capture.Id,
                CaptureTransactionResult = capture.Status,
                NewPaymentStatus = PaymentStatus.Paid
            };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>The result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            //void previously authorized payment
            var (_, error) = _serviceManager.Void(_settings, voidPaymentRequest.Order.AuthorizationTransactionId);
            if (!string.IsNullOrEmpty(error))
                return new VoidPaymentResult { Errors = new[] { error } };

            //request succeeded
            return new VoidPaymentResult { NewPaymentStatus = PaymentStatus.Voided };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>The result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            //refund previously captured payment
            var amount = refundPaymentRequest.AmountToRefund != refundPaymentRequest.Order.OrderTotal
                ? (decimal?)refundPaymentRequest.AmountToRefund
                : null;

            var (_, error) = _serviceManager.Refund(_settings, refundPaymentRequest.Order, amount);
            if (!string.IsNullOrEmpty(error))
                return new RefundPaymentResult { Errors = new[] { error } };

            //request succeeded
            return new RefundPaymentResult { NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>The process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>The result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return !PayPalCommerceServiceManager.IsConnected(_settings);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>The additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return decimal.Zero;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>The result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            return false;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>The list of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>The payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(PayPalCommerceDefaults.Route.Configuration);
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return PayPalCommerceDefaults.PAYMENT_INFO_VIEW_COMPONENT_NAME;
        }

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>The widget zones</returns>
        public IList<string> GetWidgetZones()
        {
            return new List<string>
            {
                PublicWidgetZones.ProductDetailsAddInfo,
                PublicWidgetZones.OrderSummaryContentBefore,
                PublicWidgetZones.HeaderLinksBefore,
                PublicWidgetZones.Footer,
                PublicWidgetZones.OrderSummaryTotals,
                AdminWidgetZones.OrderShipmentDetailsButtons,
                AdminWidgetZones.OrderShipmentAddButtons,
                AdminWidgetZones.PaymentMethodListTop
            };
        }

        /// <summary>
        /// Gets a name of a view component for displaying widget
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <returns>View component name</returns>
        public string GetWidgetViewComponentName(string widgetZone)
        {
            if (widgetZone is null)
                throw new ArgumentNullException(nameof(widgetZone));

            if (widgetZone.Equals(PublicWidgetZones.ProductDetailsAddInfo) || widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore))
                return PayPalCommerceDefaults.BUTTONS_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(PublicWidgetZones.HeaderLinksBefore) || widgetZone.Equals(PublicWidgetZones.Footer))
                return PayPalCommerceDefaults.LOGO_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(PublicWidgetZones.OrderSummaryTotals))
                return PayPalCommerceDefaults.MESSAGES_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(AdminWidgetZones.OrderShipmentDetailsButtons) || widgetZone.Equals(AdminWidgetZones.OrderShipmentAddButtons))
                return PayPalCommerceDefaults.SHIPMENT_CARRIER_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(AdminWidgetZones.PaymentMethodListTop))
                return PayPalCommerceDefaults.PAYMENT_METHOD_VIEW_COMPONENT_NAME;

            return null;
        }

        /// <summary>
        /// Manage sitemap. You can use "SystemName" of menu items to manage existing sitemap or add a new menu item.
        /// </summary>
        /// <param name="rootNode">Root node of the sitemap.</param>
        public void ManageSiteMap(SiteMapNode rootNode)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return;

            var configurationItem = rootNode.ChildNodes.FirstOrDefault(node => node.SystemName.Equals("Configuration"));
            if (configurationItem is null)
                return;

            var nextItem = configurationItem.ChildNodes.FirstOrDefault(node => node.SystemName.Equals("nopCommerce Web API plugin"))
                ?? configurationItem.ChildNodes.FirstOrDefault(node => node.SystemName.Equals("Local plugins"));
            if (nextItem is null)
                return;

            var index = configurationItem.ChildNodes.IndexOf(nextItem);
            if (index < 0)
                return;

            configurationItem.ChildNodes.Insert(index + 1, new SiteMapNode
            {
                Visible = true,
                SystemName = PluginDescriptor.SystemName,
                Title = _localizationService.GetLocalizedFriendlyName(this, _workContext.WorkingLanguage.Id),
                IconClass = "fa fa-dot-circle-o",
                ChildNodes = new List<SiteMapNode>
                {
                    new SiteMapNode
                    {
                        Visible = true,
                        SystemName = $"{PayPalCommerceDefaults.SystemName} Configuration",
                        Title = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Configuration"),
                        ControllerName = "PayPalCommerce",
                        ActionName = "Configure",
                        IconClass = "fa fa-genderless",
                        RouteValues = new RouteValueDictionary { { "area", AreaNames.Admin } }
                    },
                    new SiteMapNode
                    {
                        Visible = _settings.UseSandbox || _settings.ConfiguratorSupported,
                        SystemName = $"{PayPalCommerceDefaults.SystemName} Pay Later",
                        Title = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.PayLater"),
                        ControllerName = "PayPalCommerce",
                        ActionName = "PayLater",
                        IconClass = "fa fa-genderless",
                        RouteValues = new RouteValueDictionary { { "area", AreaNames.Admin } }
                    }
                }
            });
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            _settingService.SaveSetting(new PayPalCommerceSettings
            {
                SetCredentialsManually = false,
                UseSandbox = false,
                PaymentType = PaymentType.Capture,
                UseCardFields = false,
                CustomerAuthenticationRequired = true,
                UseApplePay = false,
                UseGooglePay = false,
                UseAlternativePayments = false,
                UseVault = false,
                SkipOrderConfirmPage = false,
                UseShipmentTracking = false,
                DisplayButtonsOnPaymentMethod = true,
                DisplayButtonsOnProductDetails = true,
                DisplayButtonsOnShoppingCart = true,
                DisplayLogoInHeaderLinks = false,
                DisplayLogoInFooter = false,
                RequestTimeout = PayPalCommerceDefaults.RequestTimeout,
                EnabledFunding = "paylater,venmo",
                StyleLayout = "vertical",
                StyleColor = "gold",
                StyleShape = "rect",
                StyleLabel = "paypal",
                StyleTagline = "true",
                HideCheckoutButton = false,
                ImmediatePaymentRequired = false,
                OrderValidityInterval = 5 * 60, //5 minutes
                ConfiguratorSupported = false,
                LogoInHeaderLinks =
                    "<!-- PayPal Logo --><li><a href=\"https://www.paypal.com/webapps/mpp/paypal-popup\" title=\"How PayPal Works\" " +
                    "onclick=\"javascript:window.open('https://www.paypal.com/webapps/mpp/paypal-popup','WIPaypal','toolbar=no, location=no, " +
                    "directories=no, status=no, menubar=no, scrollbars=yes, resizable=yes, width=1060, height=700'); return false;\">" +
                    "<img style=\"padding-top:10px;\" src=\"https://www.paypalobjects.com/webstatic/mktg/logo/bdg_now_accepting_pp_2line_w.png\" " +
                    "border=\"0\" alt=\"Now accepting PayPal\"></a></li><!-- PayPal Logo -->",
                LogoInFooter =
                    "<!-- PayPal Logo --><div><a href=\"https://www.paypal.com/webapps/mpp/paypal-popup\" title=\"How PayPal Works\" " +
                    "onclick=\"javascript:window.open('https://www.paypal.com/webapps/mpp/paypal-popup','WIPaypal','toolbar=no, location=no, " +
                    "directories=no, status=no, menubar=no, scrollbars=yes, resizable=yes, width=1060, height=700'); return false;\">" +
                    "<img src=\"https://www.paypalobjects.com/webstatic/mktg/logo/AM_mc_vs_dc_ae.jpg\" " +
                    "border=\"0\" alt=\"PayPal Acceptance Mark\"></a></div><!-- PayPal Logo -->",
            });

            if (!_paymentSettings.ActivePaymentMethodSystemNames.Contains(PayPalCommerceDefaults.SystemName))
            {
                _paymentSettings.ActivePaymentMethodSystemNames.Add(PayPalCommerceDefaults.SystemName);
                _settingService.SaveSetting(_paymentSettings);
            }

            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PayPalCommerceDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(PayPalCommerceDefaults.SystemName);
                _settingService.SaveSetting(_widgetSettings);
            }

            _localizationService.AddPluginLocaleResource(new Dictionary<string, string>
            {
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Cart"] = "Shopping cart",
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Product"] = "Product",
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.PaymentMethod"] = "Checkout",
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType.Authorize"] = "Authorize",
                ["Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType.Capture"] = "Capture",

                ["Plugins.Payments.PayPalCommerce.ApplePay.Discount"] = "Discount",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Shipping"] = "Shipping",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Subtotal"] = "Subtotal",
                ["Plugins.Payments.PayPalCommerce.ApplePay.Tax"] = "Tax",

                ["Plugins.Payments.PayPalCommerce.Card.Button"] = "Pay now with Card",
                ["Plugins.Payments.PayPalCommerce.Card.New"] = "Pay by new card",
                ["Plugins.Payments.PayPalCommerce.Card.Prefix"] = "Pay by",
                ["Plugins.Payments.PayPalCommerce.Card.Save"] = "Save your card",
                ["Plugins.Payments.PayPalCommerce.Configuration"] = "Configuration",
                ["Plugins.Payments.PayPalCommerce.Configuration.Error"] = "Error: {0} (see details in the <a href=\"{1}\" target=\"_blank\">log</a>)",
                ["Plugins.Payments.PayPalCommerce.Credentials.Valid"] = "The specified credentials are valid",
                ["Plugins.Payments.PayPalCommerce.Credentials.Invalid"] = "The specified credentials are invalid",

                ["Plugins.Payments.PayPalCommerce.Fields.ClientId"] = "Client ID",
                ["Plugins.Payments.PayPalCommerce.Fields.ClientId.Hint"] = "Enter your PayPal REST API client ID. This identifies your PayPal account and determines where transactions are paid.",
                ["Plugins.Payments.PayPalCommerce.Fields.ClientId.Required"] = "Client ID is required",
                ["Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired"] = "Use 3D Secure",
                ["Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired.Hint"] = "3D Secure enables you to authenticate card holders through card issuers. It reduces the likelihood of fraud when you use supported cards and improves transaction performance. A successful 3D Secure authentication can shift liability for chargebacks due to fraud from you to the card issuer.",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnProductDetails"] = "Display buttons on product details",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnProductDetails.Hint"] = "Determine whether to display PayPal buttons on product details pages (simple products only) allowing buyers to complete a purchase without going through the full checkout process.",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnShoppingCart"] = "Display buttons on shopping cart",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnShoppingCart.Hint"] = "Determine whether to display PayPal buttons on the shopping cart page in addition to the default checkout button.",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInFooter"] = "Display logo in footer",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInFooter.Hint"] = "Determine whether to display PayPal logo in the footer. These logos and banners are a great way to let your buyers know that you choose PayPal to securely process their payments.",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInHeaderLinks"] = "Display logo in header links",
                ["Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInHeaderLinks.Hint"] = "Determine whether to display PayPal logo in header links. These logos and banners are a great way to let your buyers know that you choose PayPal to securely process their payments.",
                ["Plugins.Payments.PayPalCommerce.Fields.LogoInFooter"] = "Logo source code",
                ["Plugins.Payments.PayPalCommerce.Fields.LogoInFooter.Hint"] = "Enter source code of the logo. Find more logos and banners on PayPal Logo Center. You can also modify the code to fit correctly into your theme and site style.",
                ["Plugins.Payments.PayPalCommerce.Fields.LogoInHeaderLinks"] = "Logo source code",
                ["Plugins.Payments.PayPalCommerce.Fields.LogoInHeaderLinks.Hint"] = "Enter source code of the logo. Find more logos and banners on PayPal Logo Center. You can also modify the code to fit correctly into your theme and site style.",
                ["Plugins.Payments.PayPalCommerce.Fields.MerchantId"] = "Merchant ID",
                ["Plugins.Payments.PayPalCommerce.Fields.MerchantId.Hint"] = "PayPal account ID of the merchant.",
                ["Plugins.Payments.PayPalCommerce.Fields.MerchantId.Required"] = "Merchant ID is required",
                ["Plugins.Payments.PayPalCommerce.Fields.PaymentType"] = "Payment type",
                ["Plugins.Payments.PayPalCommerce.Fields.PaymentType.Hint"] = "Choose a payment type to either capture payment immediately or authorize a payment for an order after order creation. Notice, that alternative payment methods don't work with the 'authorize and capture later' feature.",
                ["Plugins.Payments.PayPalCommerce.Fields.SecretKey"] = "Secret",
                ["Plugins.Payments.PayPalCommerce.Fields.SecretKey.Hint"] = "Enter your PayPal REST API secret.",
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
                ["Plugins.Payments.PayPalCommerce.Fields.UseSandbox"] = "Use sandbox",
                ["Plugins.Payments.PayPalCommerce.Fields.UseSandbox.Hint"] = "Determine whether to use the sandbox environment for testing purposes.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking"] = "Use shipment tracking",
                ["Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking.Hint"] = "Determine whether to use the package tracking. It allows to automatically sync orders and shipment status with PayPal.",
                ["Plugins.Payments.PayPalCommerce.Fields.UseVault"] = "Use Vault",
                ["Plugins.Payments.PayPalCommerce.Fields.UseVault.Hint"] = "Determine whether to use PayPal Vault. It allows to store buyers payment information and use it in subsequent transactions.",

                ["Plugins.Payments.PayPalCommerce.GooglePay.Discount"] = "Discount",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Shipping"] = "Shipping",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Subtotal"] = "Subtotal",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Tax"] = "Tax",
                ["Plugins.Payments.PayPalCommerce.GooglePay.Total"] = "Total",

                ["Plugins.Payments.PayPalCommerce.Onboarding.AccessRevoked"] = "Profile access has been successfully revoked.",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Button"] = "Sign up for PayPal",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Button.Sandbox"] = "Sign up for PayPal (sandbox)",
                ["Plugins.Payments.PayPalCommerce.Onboarding.ButtonRevoke"] = "Revoke access",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Completed"] = "Onboarding is sucessfully completed",
                ["Plugins.Payments.PayPalCommerce.Onboarding.Error"] = "An error occurred during the onboarding process, the credentials are empty",
                ["Plugins.Payments.PayPalCommerce.Onboarding.InProcess"] = "Onboarding is in process, see details below",
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
                ["Plugins.Payments.PayPalCommerce.Prominently"] = "Feature PayPal Prominently",
                ["Plugins.Payments.PayPalCommerce.PaymentMethodDescription"] = "PayPal Checkout with using methods like Venmo, PayPal Credit, credit card payments",
                ["Plugins.Payments.PayPalCommerce.RoundingWarning"] = "It looks like you have <a href=\"{0}\" target=\"_blank\">RoundPricesDuringCalculation</a> setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as PayPal rounds to two decimals only.",

                ["Plugins.Payments.PayPalCommerce.Shipment.Carrier"] = "Carrier",
                ["Plugins.Payments.PayPalCommerce.Shipment.Carrier.Hint"] = "Specify the carrier for the shipment (e.g. UPS or FEDEX_UK, see allowed values on PayPal site).",

                ["Plugins.Payments.PayPalCommerce.WebhookWarning"] = "Webhook was not created, so some functions may not work correctly (see details in the <a href=\"{0}\" target=\"_blank\">log</a>. Please ensure that your store is under SSL, PayPal service doesn't send requests to unsecured sites.)"
            });

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //clear webhooks when uninstall
            var stores = _storeService.GetAllStores();
            var storeIds = new List<int> { 0 }.Union(stores.Select(store => store.Id));
            foreach (var storeId in storeIds)
            {
                var settings = _settingService.LoadSetting<PayPalCommerceSettings>(storeId);
                if (PayPalCommerceServiceManager.IsConnected(settings))
                    _serviceManager.DeleteWebhook(settings);
            }

            if (_paymentSettings.ActivePaymentMethodSystemNames.Contains(PayPalCommerceDefaults.SystemName))
            {
                _paymentSettings.ActivePaymentMethodSystemNames.Remove(PayPalCommerceDefaults.SystemName);
                _settingService.SaveSetting(_paymentSettings);
            }

            if (_widgetSettings.ActiveWidgetSystemNames.Contains(PayPalCommerceDefaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(PayPalCommerceDefaults.SystemName);
                _settingService.SaveSetting(_widgetSettings);
            }

            _settingService.DeleteSetting<PayPalCommerceSettings>();

            _localizationService.DeletePluginLocaleResources("Enums.Nop.Plugin.Payments.PayPalCommerce");
            _localizationService.DeletePluginLocaleResources("Plugins.Payments.PayPalCommerce");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => true;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => true;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => true;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => true;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.PayPalCommerce.PaymentMethodDescription");

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => true;

        #endregion
    }
}