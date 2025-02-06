using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Factories;
using Nop.Plugin.Payments.PayPalCommerce.Models.Admin;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayPalCommerce.Controllers
{
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    [ValidateIpAddress]
    [AuthorizeAdmin]
    public class PayPalCommerceController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly PayPalCommerceModelFactory _modelFactory;
        private readonly PayPalCommerceServiceManager _serviceManager;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        #endregion

        #region Ctor

        public PayPalCommerceController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWorkContext workContext,
            PayPalCommerceModelFactory modelFactory,
            PayPalCommerceServiceManager serviceManager,
            ShoppingCartSettings shoppingCartSettings)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _workContext = workContext;
            _modelFactory = modelFactory;
            _serviceManager = serviceManager;
            _shoppingCartSettings = shoppingCartSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Load plugin settings
        /// </summary>
        /// <param name="storeId">Store id; pass null to use active store scope configuration</param>
        /// <returns>The plugin settings; store id</returns>
        private (PayPalCommerceSettings Settings, int StoreId) LoadSettings(int? storeId = null)
        {
            if (storeId is null)
                storeId = _storeContext.ActiveStoreScopeConfiguration;

            var settings = _settingService.LoadSetting<PayPalCommerceSettings>(storeId ?? 0);

            //we don't need some of the shared settings that loaded above, so load them separately for the chosen store
            if (storeId > 0)
            {
                TPropType getSetting<TPropType>(Expression<Func<PayPalCommerceSettings, TPropType>> keySelector) =>
                    _settingService.GetSettingByKey<TPropType>(_settingService.GetSettingKey(settings, keySelector), storeId: storeId ?? 0);

                settings.MerchantGuid = getSetting(setting => setting.MerchantGuid);
                settings.MerchantId = getSetting(setting => setting.MerchantId);
                settings.WebhookUrl = getSetting(setting => setting.WebhookUrl);
                settings.SetCredentialsManually = getSetting(setting => setting.SetCredentialsManually);
                settings.UseSandbox = getSetting(setting => setting.UseSandbox);
                settings.ClientId = getSetting(setting => setting.ClientId);
                settings.SecretKey = getSetting(setting => setting.SecretKey);
                settings.ConfiguratorSupported = getSetting(setting => setting.ConfiguratorSupported);
            }

            return (settings, storeId ?? 0);
        }

        /// <summary>
        /// Save settings for the passed store
        /// </summary>
        /// <typeparam name="TPropType">Property type</typeparam>
        /// <param name="settings">Plugin settings</param>
        /// <param name="keySelector">Key selector</param>
        /// <param name="storeId">Store id</param>
        /// <param name="overrideForStore">Whether to overridde this setting for the passed store</param>
        private void SaveSetting<TPropType>(PayPalCommerceSettings settings,
            Expression<Func<PayPalCommerceSettings, TPropType>> keySelector, int storeId, bool? overrideForStore = null)
        {
            //save overridden settings
            _settingService.SaveSettingOverridablePerStore(settings, keySelector, overrideForStore ?? true, storeId, false);

            //save shared settings
            if (storeId > 0 && overrideForStore is null)
                _settingService.SaveSettingOverridablePerStore(settings, keySelector, true, 0, false);
        }

        /// <summary>
        /// Get details to sign up a merchant
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>The URL to sign up; merchant internal id</returns>
        private (string SandboxUrl, string LiveUrl, string MerchantGuid) GetSignUpDetails(PayPalCommerceSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.MerchantGuid))
                return (null, null, settings.MerchantGuid);

            //set new GUID
            var merchantGuid = Guid.NewGuid().ToString();

            //prepare URL to sign up
            var ((sandboxUrl, liveUrl), error) = _serviceManager.PrepareSignUpUrl(merchantGuid);
            if (!string.IsNullOrEmpty(error))
            {
                var locale = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Configuration.Error");
                var errorMessage = string.Format(locale, error, Url.Action("List", "Log"));
                _notificationService.ErrorNotification(errorMessage, false);
            }

            return (sandboxUrl, liveUrl, merchantGuid);
        }

        /// <summary>
        /// Check the merchant status
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="storeId">Store id</param>
        /// <returns>The merchant model</returns>
        private MerchantModel CheckMerchantStatus(PayPalCommerceSettings settings, int storeId)
        {
            //no need to check the status when credentials were manually set
            if (string.IsNullOrEmpty(settings.MerchantGuid) || settings.SetCredentialsManually)
                return new MerchantModel();

            var model = _modelFactory.PrepareMerchantModel(settings);

            //disable appropriate settings when unavailable
            if (settings.ConfiguratorSupported != model.ConfiguratorSupported)
            {
                settings.ConfiguratorSupported = model.ConfiguratorSupported;
                SaveSetting(settings, setting => setting.ConfiguratorSupported, storeId);
            }
            if (!model.AdvancedCardsEnabled && settings.UseCardFields)
            {
                settings.UseCardFields = false;
                SaveSetting(settings, setting => setting.UseCardFields, storeId);
            }
            if (!model.ApplePayEnabled && settings.UseApplePay)
            {
                settings.UseApplePay = false;
                SaveSetting(settings, setting => setting.UseApplePay, storeId);
            }
            if (!model.GooglePayEnabled && settings.UseGooglePay)
            {
                settings.UseGooglePay = false;
                SaveSetting(settings, setting => setting.UseGooglePay, storeId);
            }
            if (!model.VaultingEnabled && settings.UseVault)
            {
                settings.UseVault = false;
                SaveSetting(settings, setting => setting.UseVault, storeId);
            }
            _settingService.ClearCache();

            //display notifications
            foreach (var warning in model.Messages.Warning)
            {
                _notificationService.WarningNotification(warning, false);
            }

            foreach (var error in model.Messages.Error)
            {
                _notificationService.ErrorNotification(error, false);
            }

            foreach (var message in model.Messages.Success)
            {
                _notificationService.SuccessNotification(message);
            }

            return model;
        }

        /// <summary>
        /// Set credentials manually
        /// </summary>
        /// <param name="model">Configuration model</param>
        /// <param name="settings">Plugin settings</param>
        /// <param name="storeId">Store id</param>
        private void SetCredentialsManually(ConfigurationModel model, PayPalCommerceSettings settings, int storeId)
        {
            if (!model.SetCredentialsManually)
                return;

            //first delete the unused webhook on a previous client, if changed
            if (PayPalCommerceServiceManager.IsConnected(settings) && !string.Equals(model.ClientId, settings.ClientId))
            {
                _serviceManager.DeleteWebhook(settings);
                settings.WebhookUrl = string.Empty;
            }

            settings.ClientId = model.ClientId;
            settings.SecretKey = model.SecretKey;
            settings.MerchantId = model.MerchantId;
            SaveSetting(settings, setting => setting.ClientId, storeId);
            SaveSetting(settings, setting => setting.SecretKey, storeId);
            SaveSetting(settings, setting => setting.MerchantId, storeId);
        }

        /// <summary>
        /// Ensure that webhook created, display warning in case of fail
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="storeId">Store id</param>
        private void EnsureWebhookCreated(PayPalCommerceSettings settings, int storeId)
        {
            if (!PayPalCommerceServiceManager.IsConfigured(settings))
                return;

            var (webhook, _) = _serviceManager.CreateWebhook(settings, storeId);
            if (string.IsNullOrEmpty(webhook?.Url))
            {
                var url = Url.Action("List", "Log");
                var warningMessage = string.Format(_localizationService.GetResource("Plugins.Payments.PayPalCommerce.WebhookWarning"), url);
                _notificationService.WarningNotification(warningMessage, false);

                return;
            }

            if (string.Equals(settings.WebhookUrl, webhook.Url, StringComparison.InvariantCultureIgnoreCase))
                return;

            settings.WebhookUrl = webhook.Url;
            SaveSetting(settings, setting => setting.WebhookUrl, storeId);
            _settingService.ClearCache();
        }

        #endregion

        #region Methods

        #region Configuration

        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var (settings, storeId) = LoadSettings();

            var model = new ConfigurationModel
            {
                SetCredentialsManually = settings.SetCredentialsManually,
                UseSandbox = settings.UseSandbox,
                ClientId = settings.SetCredentialsManually ? settings.ClientId : string.Empty,
                SecretKey = settings.SetCredentialsManually ? settings.SecretKey : string.Empty,
                MerchantId = settings.SetCredentialsManually ? settings.MerchantId : string.Empty,
                PaymentTypeId = (int)settings.PaymentType,
                UseCardFields = settings.UseCardFields,
                CustomerAuthenticationRequired = settings.CustomerAuthenticationRequired,
                UseApplePay = settings.UseApplePay,
                UseGooglePay = settings.UseGooglePay,
                UseAlternativePayments = settings.UseAlternativePayments,
                UseVault = settings.UseVault,
                SkipOrderConfirmPage = settings.SkipOrderConfirmPage,
                UseShipmentTracking = settings.UseShipmentTracking,
                DisplayButtonsOnShoppingCart = settings.DisplayButtonsOnShoppingCart,
                DisplayButtonsOnProductDetails = settings.DisplayButtonsOnProductDetails,
                DisplayLogoInHeaderLinks = settings.DisplayLogoInHeaderLinks,
                LogoInHeaderLinks = settings.LogoInHeaderLinks,
                DisplayLogoInFooter = settings.DisplayLogoInFooter,
                LogoInFooter = settings.LogoInFooter,
                ActiveStoreScopeConfiguration = storeId,
                IsConfigured = PayPalCommerceServiceManager.IsConfigured(settings)
            };

            if (storeId > 0)
            {
                model.SetCredentialsManually_OverrideForStore = true;
                model.UseSandbox_OverrideForStore = true;
                model.ClientId_OverrideForStore = true;
                model.SecretKey_OverrideForStore = true;
                model.MerchantId_OverrideForStore = true;

                model.PaymentTypeId_OverrideForStore = _settingService.SettingExists(settings, setting => setting.PaymentType, storeId);
                model.UseCardFields_OverrideForStore = _settingService.SettingExists(settings, setting => setting.UseCardFields, storeId);
                model.CustomerAuthenticationRequired_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.CustomerAuthenticationRequired, storeId);
                model.UseApplePay_OverrideForStore = _settingService.SettingExists(settings, setting => setting.UseApplePay, storeId);
                model.UseGooglePay_OverrideForStore = _settingService.SettingExists(settings, setting => setting.UseGooglePay, storeId);
                model.UseAlternativePayments_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.UseAlternativePayments, storeId);
                model.UseVault_OverrideForStore = _settingService.SettingExists(settings, setting => setting.UseVault, storeId);
                model.SkipOrderConfirmPage_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.SkipOrderConfirmPage, storeId);
                model.DisplayButtonsOnShoppingCart_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.DisplayButtonsOnShoppingCart, storeId);
                model.DisplayButtonsOnProductDetails_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.DisplayButtonsOnProductDetails, storeId);
                model.DisplayLogoInHeaderLinks_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.DisplayLogoInHeaderLinks, storeId);
                model.LogoInHeaderLinks_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.LogoInHeaderLinks, storeId);
                model.DisplayLogoInFooter_OverrideForStore = _settingService
                    .SettingExists(settings, setting => setting.DisplayLogoInFooter, storeId);
                model.LogoInFooter_OverrideForStore = _settingService.SettingExists(settings, setting => setting.LogoInFooter, storeId);
            }

            model.PaymentTypes = (PaymentType.Capture.ToSelectList(false, new[] { (int)PaymentType.Subscription, (int)PaymentType.Tokenize }))
                .Select(item => new SelectListItem(item.Text, item.Value))
                .ToList();

            //merchant and onboarding details
            (model.SandboxSignUpUrl, model.LiveSignUpUrl, model.MerchantGuid) = GetSignUpDetails(settings);
            model.MerchantModel = CheckMerchantStatus(settings, storeId);
            if (!settings.SetCredentialsManually)
                model.MerchantId = model.MerchantModel.MerchantId;

            EnsureWebhookCreated(settings, storeId);

            if (PayPalCommerceServiceManager.IsConnected(settings) && !_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                //prices and total aren't rounded, so display warning
                var url = Url.Action("AllSettings", "Setting", new { settingName = nameof(ShoppingCartSettings.RoundPricesDuringCalculation) });
                var warningMessage = string.Format(_localizationService.GetResource("Plugins.Payments.PayPalCommerce.RoundingWarning"), url);
                _notificationService.WarningNotification(warningMessage, false);
            }

            //ensure credentials are valid
            if (PayPalCommerceServiceManager.IsConnected(settings))
            {
                var (_, credentialsError) = _serviceManager.GetAccessToken(settings);
                if (!string.IsNullOrEmpty(credentialsError))
                {
                    _notificationService.ErrorNotification(_localizationService
                        .GetResource("Plugins.Payments.PayPalCommerce.Credentials.Invalid"));
                }
                else
                {
                    _notificationService.SuccessNotification(_localizationService
                        .GetResource("Plugins.Payments.PayPalCommerce.Credentials.Valid"));
                }
            }

            return View("~/Plugins/Payments.PayPalCommerce/Views/Admin/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            var (settings, storeId) = LoadSettings();

            //set new settings values
            var configureCredentials = Request.Form.TryGetValue(nameof(model.SetCredentialsManually), out _);
            if (configureCredentials)
            {
                settings.SetCredentialsManually = model.SetCredentialsManually;
                settings.UseSandbox = model.UseSandbox;
            }
            settings.PaymentType = (PaymentType)model.PaymentTypeId;
            settings.UseCardFields = model.UseCardFields;
            settings.CustomerAuthenticationRequired = model.CustomerAuthenticationRequired;
            settings.UseApplePay = model.UseApplePay;
            settings.UseGooglePay = model.UseGooglePay;
            settings.UseAlternativePayments = model.UseAlternativePayments;
            settings.UseVault = model.UseVault;
            settings.SkipOrderConfirmPage = model.SkipOrderConfirmPage;
            settings.UseShipmentTracking = model.UseShipmentTracking;
            settings.DisplayButtonsOnShoppingCart = model.DisplayButtonsOnShoppingCart;
            settings.DisplayButtonsOnProductDetails = model.DisplayButtonsOnProductDetails;
            settings.DisplayLogoInHeaderLinks = model.DisplayLogoInHeaderLinks;
            settings.LogoInHeaderLinks = model.LogoInHeaderLinks;
            settings.DisplayLogoInFooter = model.DisplayLogoInFooter;
            settings.LogoInFooter = model.LogoInFooter;

            SetCredentialsManually(model, settings, storeId);

            SaveSetting(settings, setting => setting.SetCredentialsManually, storeId);
            SaveSetting(settings, setting => setting.UseSandbox, storeId);
            SaveSetting(settings, setting => setting.PaymentType, storeId, model.PaymentTypeId_OverrideForStore);
            SaveSetting(settings, setting => setting.UseCardFields, storeId, model.UseCardFields_OverrideForStore);
            SaveSetting(settings, setting
                => setting.CustomerAuthenticationRequired, storeId, model.CustomerAuthenticationRequired_OverrideForStore);
            SaveSetting(settings, setting => setting.UseApplePay, storeId, model.UseApplePay_OverrideForStore);
            SaveSetting(settings, setting => setting.UseGooglePay, storeId, model.UseGooglePay_OverrideForStore);
            SaveSetting(settings, setting => setting.UseAlternativePayments, storeId, model.UseAlternativePayments_OverrideForStore);
            SaveSetting(settings, setting => setting.UseVault, storeId, model.UseVault_OverrideForStore);
            SaveSetting(settings, setting => setting.SkipOrderConfirmPage, storeId, model.SkipOrderConfirmPage_OverrideForStore);
            SaveSetting(settings, setting => setting.UseShipmentTracking, storeId);
            SaveSetting(settings, setting
                => setting.DisplayButtonsOnShoppingCart, storeId, model.DisplayButtonsOnShoppingCart_OverrideForStore);
            SaveSetting(settings, setting
                => setting.DisplayButtonsOnProductDetails, storeId, model.DisplayButtonsOnProductDetails_OverrideForStore);
            SaveSetting(settings, setting => setting.DisplayLogoInHeaderLinks, storeId, model.DisplayLogoInHeaderLinks_OverrideForStore);
            SaveSetting(settings, setting => setting.LogoInHeaderLinks, storeId, model.LogoInHeaderLinks_OverrideForStore);
            SaveSetting(settings, setting => setting.DisplayLogoInFooter, storeId, model.DisplayLogoInFooter_OverrideForStore);
            SaveSetting(settings, setting => setting.LogoInFooter, storeId, model.LogoInFooter_OverrideForStore);
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }

        [HttpPost]
        public IActionResult Onboarding(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedDataTablesJson();

            var (settings, storeId) = LoadSettings();

            //set onboarding values
            settings.MerchantGuid = model.MerchantGuid;
            settings.MerchantId = string.Empty;
            settings.WebhookUrl = string.Empty;

            //set credentials values
            settings.SetCredentialsManually = false;
            settings.UseSandbox = model.UseSandbox;
            settings.ClientId = string.Empty;
            settings.SecretKey = string.Empty;

            settings.ConfiguratorSupported = false;

            SaveSetting(settings, setting => setting.MerchantGuid, storeId);
            SaveSetting(settings, setting => setting.MerchantId, storeId);
            SaveSetting(settings, setting => setting.WebhookUrl, storeId);
            SaveSetting(settings, setting => setting.SetCredentialsManually, storeId);
            SaveSetting(settings, setting => setting.UseSandbox, storeId);
            SaveSetting(settings, setting => setting.ClientId, storeId);
            SaveSetting(settings, setting => setting.SecretKey, storeId);
            SaveSetting(settings, setting => setting.ConfiguratorSupported, storeId);
            _settingService.ClearCache();

            return Json(new { success = true });
        }

        public IActionResult Onboarding(OnboardingCallbackModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var storeId = model.StoreId;
            var (settings, storeIdTmp) = LoadSettings(storeId);
            storeId = storeIdTmp;

            if (!string.IsNullOrEmpty(settings.MerchantGuid))
            {
                //we need some time to complete the create credentials request before redirecting the merchant
                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5));

                if (string.IsNullOrEmpty(settings.MerchantId))
                {
                    settings.MerchantId = model.MerchantIdInPayPal;
                    SaveSetting(settings, setting => setting.MerchantId, storeId);
                    _settingService.ClearCache();
                }
            }
            else
                _notificationService.ErrorNotification(_localizationService.GetResource("Plugins.Payments.PayPalCommerce.Onboarding.Error"));

            return RedirectToAction("Configure");
        }

        [HttpPost]
        public IActionResult SignUp(AuthenticationModel model)
        {
            var storeId = model.StoreId;
            var (settings, storeIdTmp) = LoadSettings(storeId);
            storeId = storeIdTmp;

            //try to get credentials by authentication parameters
            var (credentials, _) = _serviceManager.SignUp(settings, model.AuthCode, model.SharedId);
            if (credentials is null)
                return ErrorJson(_localizationService.GetResource("Plugins.Payments.PayPalCommerce.Onboarding.Error"));

            //first delete the unused webhook on a previous client, if changed
            if (PayPalCommerceServiceManager.IsConnected(settings) && !string.Equals(credentials.ClientId, settings.ClientId))
            {
                _serviceManager.DeleteWebhook(settings);
                settings.WebhookUrl = string.Empty;
            }

            //set onboarding values
            settings.MerchantId = credentials.PayerId;

            //set credentials values
            settings.ClientId = credentials.ClientId;
            settings.SecretKey = credentials.ClientSecret;

            SaveSetting(settings, setting => setting.MerchantId, storeId);
            SaveSetting(settings, setting => setting.WebhookUrl, storeId);
            SaveSetting(settings, setting => setting.ClientId, storeId);
            SaveSetting(settings, setting => setting.SecretKey, storeId);
            _settingService.ClearCache();

            return Json(new { success = true });
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("revoke")]
        public IActionResult RevokeAccess()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var (settings, storeId) = LoadSettings();

            //delete webhook
            if (PayPalCommerceServiceManager.IsConnected(settings))
            {
                _serviceManager.DeleteWebhook(settings);
                settings.WebhookUrl = string.Empty;
            }

            //clear onboarding values
            settings.MerchantGuid = string.Empty;
            settings.MerchantId = string.Empty;

            //clear credentials values
            settings.ClientId = string.Empty;
            settings.SecretKey = string.Empty;

            settings.ConfiguratorSupported = false;

            SaveSetting(settings, setting => setting.MerchantGuid, storeId);
            SaveSetting(settings, setting => setting.MerchantId, storeId);
            SaveSetting(settings, setting => setting.WebhookUrl, storeId);
            SaveSetting(settings, setting => setting.ClientId, storeId);
            SaveSetting(settings, setting => setting.SecretKey, storeId);
            SaveSetting(settings, setting => setting.ConfiguratorSupported, storeId);
            _settingService.ClearCache();

            var accessRevokedMessage = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Onboarding.AccessRevoked");
            _notificationService.SuccessNotification(accessRevokedMessage);

            return RedirectToAction("Configure");
        }

        #endregion

        #region Pay Later

        public IActionResult PayLater()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var (settings, _) = LoadSettings();
            if (!settings.UseSandbox && !settings.ConfiguratorSupported)
                return RedirectToAction("Configure");

            var language = _workContext.WorkingLanguage;
            var model = new PayLaterConfigurationModel
            {
                ClientId = settings.ClientId,
                UseSandbox = settings.UseSandbox,
                Config = !string.IsNullOrEmpty(settings.PayLaterConfig) ? settings.PayLaterConfig : "{}",
                Locale = language.LanguageCulture?.Replace('-', '_') ?? "en_US"
            };

            return View("~/Plugins/Payments.PayPalCommerce/Views/Admin/PayLater.cshtml", model);
        }

        [HttpPost]
        public IActionResult PayLaterConfig(string config)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedDataTablesJson();

            var (settings, storeId) = LoadSettings();
            if (!settings.UseSandbox && !settings.ConfiguratorSupported)
                return ErrorJson("Merchant messaging configurator is not available");

            settings.PayLaterConfig = config;
            SaveSetting(settings, setting => setting.PayLaterConfig, storeId);
            _settingService.ClearCache();

            return Json(new { message = _localizationService.GetResource("Admin.Plugins.Saved") });

        }

        #endregion

        #endregion
    }
}