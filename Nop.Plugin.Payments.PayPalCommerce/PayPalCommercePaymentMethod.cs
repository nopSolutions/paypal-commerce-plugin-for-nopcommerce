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
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayPalCommerce.Data;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
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
        private readonly PayPalTokenObjectContext _objectContext;
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
            PayPalTokenObjectContext objectContext,
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
            _objectContext = objectContext;
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
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = PayPalCommerceDefaults.PAYMENT_INFO_VIEW_COMPONENT_NAME;
        }

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>The widget zones</returns>
        public IList<string> GetWidgetZones()
        {
            return new List<string>
            {
                PayPalCommerceDefaults.ProductDetailsAddInfo,
                PayPalCommerceDefaults.OrderSummaryContentBefore,
                PayPalCommerceDefaults.HeaderLinksBefore,
                PayPalCommerceDefaults.Footer,
                PayPalCommerceDefaults.OrderSummaryContentAfter,
                PayPalCommerceDefaults.OrderShipmentDetailsButtons,
                PayPalCommerceDefaults.PaymentMethodListTop
            };
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <param name="viewComponentName">View component name</param>
        public void GetPublicViewComponent(string widgetZone, out string viewComponentName)
        {
            viewComponentName = null;

            if (widgetZone is null)
                throw new ArgumentNullException(nameof(widgetZone));

            if (widgetZone.Equals(PayPalCommerceDefaults.ProductDetailsAddInfo) || widgetZone.Equals(PayPalCommerceDefaults.OrderSummaryContentBefore))
                viewComponentName = PayPalCommerceDefaults.BUTTONS_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(PayPalCommerceDefaults.HeaderLinksBefore) || widgetZone.Equals(PayPalCommerceDefaults.Footer))
                viewComponentName = PayPalCommerceDefaults.LOGO_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(PayPalCommerceDefaults.OrderSummaryContentAfter))
                viewComponentName = PayPalCommerceDefaults.MESSAGES_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(PayPalCommerceDefaults.OrderShipmentDetailsButtons))
                viewComponentName = PayPalCommerceDefaults.SHIPMENT_CARRIER_VIEW_COMPONENT_NAME;

            if (widgetZone.Equals(PayPalCommerceDefaults.PaymentMethodListTop))
                viewComponentName = PayPalCommerceDefaults.PAYMENT_METHOD_VIEW_COMPONENT_NAME;
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
                ?? configurationItem.ChildNodes.FirstOrDefault(node => node.SystemName.Equals("Plugins"));
            if (nextItem is null)
                return;

            var index = configurationItem.ChildNodes.IndexOf(nextItem);
            if (index < 0)
                return;

            configurationItem.ChildNodes.Insert(index + 1, new SiteMapNode
            {
                Visible = true,
                SystemName = PluginDescriptor.SystemName,
                Title = this.GetLocalizedFriendlyName(_localizationService, _workContext.WorkingLanguage.Id),
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
            _objectContext.Install();

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

            this.AddOrUpdatePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Cart", "Shopping cart");
            this.AddOrUpdatePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Product", "Product");
            this.AddOrUpdatePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.PaymentMethod", "Checkout");
            this.AddOrUpdatePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType.Authorize", "Authorize");
            this.AddOrUpdatePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType.Capture", "Capture");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Discount", "Discount");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Shipping", "Shipping");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Subtotal", "Subtotal");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Tax", "Tax");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.Button", "Pay now with Card");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.New", "Pay by new card");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.Prefix", "Pay by");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.Save", "Save your card");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Configuration", "Configuration");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Configuration.Error", "Error: {0} (see details in the log)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Credentials.Valid", "The specified credentials are valid");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Credentials.Invalid", "The specified credentials are invalid");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.ClientId", "Client ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.ClientId.Hint", "Enter your PayPal REST API client ID. This identifies your PayPal account and determines where transactions are paid.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.ClientId.Required", "Client ID is required");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired", "Use 3D Secure");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired.Hint", "3D Secure enables you to authenticate card holders through card issuers. It reduces the likelihood of fraud when you use supported cards and improves transaction performance. A successful 3D Secure authentication can shift liability for chargebacks due to fraud from you to the card issuer.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnProductDetails", "Display buttons on product details");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnProductDetails.Hint", "Determine whether to display PayPal buttons on product details pages (simple products only) allowing buyers to complete a purchase without going through the full checkout process.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnShoppingCart", "Display buttons on shopping cart");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnShoppingCart.Hint", "Determine whether to display PayPal buttons on the shopping cart page in addition to the default checkout button.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInFooter", "Display logo in footer");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInFooter.Hint", "Determine whether to display PayPal logo in the footer. These logos and banners are a great way to let your buyers know that you choose PayPal to securely process their payments.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInHeaderLinks", "Display logo in header links");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInHeaderLinks.Hint", "Determine whether to display PayPal logo in header links. These logos and banners are a great way to let your buyers know that you choose PayPal to securely process their payments.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInFooter", "Logo source code");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInFooter.Hint", "Enter source code of the logo. Find more logos and banners on PayPal Logo Center. You can also modify the code to fit correctly into your theme and site style.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInHeaderLinks", "Logo source code");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInHeaderLinks.Hint", "Enter source code of the logo. Find more logos and banners on PayPal Logo Center. You can also modify the code to fit correctly into your theme and site style.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.MerchantId", "Merchant ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.MerchantId.Hint", "PayPal account ID of the merchant.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.MerchantId.Required", "Merchant ID is required");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.PaymentType", "Payment type");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.PaymentType.Hint", "Choose a payment type to either capture payment immediately or authorize a payment for an order after order creation. Notice, that alternative payment methods don't work with the 'authorize and capture later' feature.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SecretKey", "Secret");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SecretKey.Hint", "Enter your PayPal REST API secret.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SecretKey.Required", "Secret is required");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SetCredentialsManually", "Specify API credentials manually");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SetCredentialsManually.Hint", "Determine whether to manually set the credentials (for example, there is already the REST API application created, or if you want to use the sandbox mode).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SkipOrderConfirmPage", "Skip 'Confirm Order' page");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SkipOrderConfirmPage.Hint", "Determine whether to skip the 'Confirm Order' step during checkout so that after approving the payment on PayPal site, customers will redirected directly to the 'Order Completed' page.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseAlternativePayments", "Use Alternative Payments Methods");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseAlternativePayments.Hint", "With alternative payment methods, customers across the globe can pay with their bank accounts, wallets, and other local payment methods.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseApplePay", "Use Apple Pay");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseApplePay.Hint", "Apple Pay is a mobile payment and digital wallet service provided by Apple Inc.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseCardFields", "Use Custom Card Fields");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseCardFields.Hint", "Advanced Credit and Debit Card Payments (Custom Card Fields) are a PCI compliant solution to accept debit and credit card payments.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseGooglePay", "Use Google Pay");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseGooglePay.Hint", "Google Pay is a mobile payment and digital wallet service provided by Alphabet Inc.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseSandbox", "Use sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseSandbox.Hint", "Determine whether to use the sandbox environment for testing purposes.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking", "Use shipment tracking");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking.Hint", "Determine whether to use the package tracking. It allows to automatically sync orders and shipment status with PayPal.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseVault", "Use Vault");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseVault.Hint", "Determine whether to use PayPal Vault. It allows to store buyers payment information and use it in subsequent transactions.");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Discount", "Discount");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Shipping", "Shipping");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Subtotal", "Subtotal");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Tax", "Tax");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Total", "Total");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.AccessRevoked", "Profile access has been successfully revoked.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Button", "Sign up for PayPal");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Button.Sandbox", "Sign up for PayPal (sandbox)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.ButtonRevoke", "Revoke access");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Completed", "Onboarding is sucessfully completed");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Error", "An error occurred during the onboarding process, the credentials are empty");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.InProcess", "Onboarding is in process, see details below");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Process.Account.Success", "PayPal account is created");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Process.Email.Success", "Email address is confirmed");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Process.Payments.Success", "Billing information is set");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Sandbox", "After you finish testing the plugin in the PayPal sandbox, move it into the production environment so you can process live transactions. To take the plugin live: 1. Revoke access to the sandbox account, 2. Disable 'Use sandbox' setting, 3. Sign up for the live PayPal account.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Title", "Connect PayPal account");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Adjustment.Name", "Adjustment item");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Adjustment.Description", "Used to adjust the order total amount when applying complex discounts or/and calculations");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Error", "Failed to get order details");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Id", "PayPal order ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Placement", "PayPal component placement");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens", "Payment methods");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.Default", "Default");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.Expiration", "Expires");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.None", "No payment methods saved yet");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.MarkDefault", "Make default");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.Title", "Method");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PayLater", "Pay Later");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Prominently", "Feature PayPal Prominently");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentMethodDescription", "PayPal Checkout with using methods like Venmo, PayPal Credit, credit card payments");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.RoundingWarning", "It looks like you have RoundPricesDuringCalculation setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as PayPal rounds to two decimals only.");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Shipment.Carrier", "Carrier");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.Shipment.Carrier.Hint", "Specify the carrier for the shipment (e.g. UPS or FEDEX_UK, see allowed values on PayPal site).");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalCommerce.WebhookWarning", "Webhook was not created, so some functions may not work correctly (see details in the log). Please ensure that your store is under SSL, PayPal service doesn't send requests to unsecured sites.");

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

            _objectContext.Uninstall();

            this.DeletePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Cart");
            this.DeletePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.Product");
            this.DeletePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.ButtonPlacement.PaymentMethod");
            this.DeletePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType.Authorize");
            this.DeletePluginLocaleResource("Enums.Nop.Plugin.Payments.PayPalCommerce.Domain.PaymentType.Capture");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Discount");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Shipping");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Subtotal");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.ApplePay.Tax");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.Button");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.New");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.Prefix");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Card.Save");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Configuration");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Configuration.Error");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Credentials.Valid");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Credentials.Invalid");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.ClientId");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.ClientId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.ClientId.Required");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.CustomerAuthenticationRequired.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnProductDetails");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnProductDetails.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnShoppingCart");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayButtonsOnShoppingCart.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInFooter");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInFooter.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInHeaderLinks");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.DisplayLogoInHeaderLinks.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInFooter");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInFooter.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInHeaderLinks");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.LogoInHeaderLinks.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.MerchantId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.MerchantId.Required");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.PaymentType");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.PaymentType.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SecretKey");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SecretKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SecretKey.Required");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SetCredentialsManually");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SetCredentialsManually.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SkipOrderConfirmPage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.SkipOrderConfirmPage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseAlternativePayments");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseAlternativePayments.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseApplePay");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseApplePay.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseApplePay.Warning");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseCardFields");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseCardFields.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseGooglePay");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseGooglePay.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseShipmentTracking.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseVault");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Fields.UseVault.Hint");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Discount");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Shipping");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Subtotal");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Tax");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.GooglePay.Total");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.AccessRevoked");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Button");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Button.Sandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.ButtonRevoke");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Completed");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Error");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.InProcess");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Process.Account.Success");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Process.Email.Success");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Process.Payments.Success");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Sandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Onboarding.Title");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Adjustment.Name");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Adjustment.Description");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Error");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Id");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Order.Placement");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.Default");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.Expiration");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.None");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.MarkDefault");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentTokens.Title");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PayLater");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Prominently");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.PaymentMethodDescription");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.RoundingWarning");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Shipment.Carrier");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.Shipment.Carrier.Hint");

            this.DeletePluginLocaleResource("Plugins.Payments.PayPalCommerce.WebhookWarning");

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