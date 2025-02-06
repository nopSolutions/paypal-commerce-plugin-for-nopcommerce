using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Authentication;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Identity;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.Enums;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.PaymentSources;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Onboarding;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Orders;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Payments;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.PaymentTokens;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Webhooks;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Pickup;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Address = Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.Address;
using NopAddress = Nop.Core.Domain.Common.Address;
using NopOrder = Nop.Core.Domain.Orders.Order;
using NopShippingOption = Nop.Core.Domain.Shipping.ShippingOption;
using Order = Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.Order;
using ShippingOption = Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.ShippingOption;

namespace Nop.Plugin.Payments.PayPalCommerce.Services
{
    /// <summary>
    /// Represents the plugin service manager
    /// </summary>
    public class PayPalCommerceServiceManager
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly CustomerSettings _customerSettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IAddressService _addressService;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPaymentService _paymentService;
        private readonly IPickupPluginManager _pickupPluginManager;
        private readonly IPictureService _pictureService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IProductService _productService;
        private readonly IShipmentService _shipmentService;
        private readonly IShippingPluginManager _shippingPluginManager;
        private readonly IShippingService _shippingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ITaxService _taxService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly OrderSettings _orderSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly PayPalCommerceHttpClient _httpClient;
        private readonly PayPalTokenService _tokenService;
        private readonly ShippingSettings _shippingSettings;

        #endregion

        #region Ctor

        public PayPalCommerceServiceManager(CurrencySettings currencySettings,
            CustomerSettings customerSettings,
            IActionContextAccessor actionContextAccessor,
            IAddressService addressService,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IPaymentPluginManager paymentPluginManager,
            IPaymentService paymentService,
            IPickupPluginManager pickupPluginManager,
            IPictureService pictureService,
            IPriceCalculationService priceCalculationService,
            IProductService productService,
            IShipmentService shipmentService,
            IShippingPluginManager shippingPluginManager,
            IShippingService shippingService,
            IShoppingCartService shoppingCartService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            IStoreService storeService,
            ITaxService taxService,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            IWorkContext workContext,
            OrderSettings orderSettings,
            PaymentSettings paymentSettings,
            PayPalCommerceHttpClient httpClient,
            PayPalTokenService tokenService,
            ShippingSettings shippingSettings)
        {
            _currencySettings = currencySettings;
            _customerSettings = customerSettings;
            _actionContextAccessor = actionContextAccessor;
            _addressService = addressService;
            _checkoutAttributeParser = checkoutAttributeParser;
            _countryService = countryService;
            _currencyService = currencyService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _logger = logger;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _paymentPluginManager = paymentPluginManager;
            _paymentService = paymentService;
            _pickupPluginManager = pickupPluginManager;
            _pictureService = pictureService;
            _priceCalculationService = priceCalculationService;
            _productService = productService;
            _shipmentService = shipmentService;
            _shippingPluginManager = shippingPluginManager;
            _shippingService = shippingService;
            _shoppingCartService = shoppingCartService;
            _stateProvinceService = stateProvinceService;
            _storeContext = storeContext;
            _storeService = storeService;
            _taxService = taxService;
            _urlHelperFactory = urlHelperFactory;
            _urlRecordService = urlRecordService;
            _webHelper = webHelper;
            _workContext = workContext;
            _orderSettings = orderSettings;
            _paymentSettings = paymentSettings;
            _httpClient = httpClient;
            _tokenService = tokenService;
            _shippingSettings = shippingSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Handle function and get result
        /// </summary>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <param name="function">Function</param>
        /// <param name="logErrors">Whether to log errors</param>
        /// <returns>The result; error message if exists</returns>
        private (TResult Result, string Error) HandleFunction<TResult>(Func<TResult> function, bool logErrors = true)
        {
            try
            {
                //invoke function
                return (function(), default);
            }
            catch (Exception exception)
            {
                //log errors
                if (logErrors)
                {
                    var logMessage = $"{PayPalCommerceDefaults.SystemName} error:{Environment.NewLine}{exception.Message}";
                    var exceptionToLog = exception is NopException nopException ? nopException.InnerException ?? nopException : exception;
                    var customer = _workContext.CurrentCustomer;
                    _logger.Error(logMessage, exceptionToLog, customer);
                }

                return (default, exception.Message);
            }
        }

        #region Components

        /// <summary>
        /// Prepare amount value for Pay Later messages
        /// </summary>
        /// <param name="placement">Button placement</param>
        /// <param name="customer">Customer</param>
        /// <param name="currencyCode">Currency code</param>
        /// <param name="productId">Product id</param>
        /// <returns>The amount value</returns>
        private string PrepareMessagesAmount(ButtonPlacement placement, Customer customer, string currencyCode, int? productId)
        {
            var store = _storeContext.CurrentStore;
            var product = _productService.GetProductById(productId ?? 0);
            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);

            _orderTotalCalculationService.GetShoppingCartSubTotal(cart, true, out _, out _, out _, out var subTotal);

            var amount = (decimal?)decimal.Zero;
            switch (placement)
            {
                case ButtonPlacement.Cart:
                    amount = subTotal;
                    break;
                case ButtonPlacement.Product:
                    if (product != null)
                        amount = _priceCalculationService.GetFinalPrice(product, customer); //+ subTotal;
                    break;
                case ButtonPlacement.PaymentMethod:
                    amount = _orderTotalCalculationService.GetShoppingCartTotal(cart, null, usePaymentMethodAdditionalFee: false) ?? subTotal;
                    break;
                default:
                    break;
            }

            return amount != null ? PrepareMoney(amount.Value, currencyCode).Value : null;
        }

        #endregion

        #region Orders

        /// <summary>
        /// Prepare money object
        /// </summary>
        /// <param name="value">Amount value</param>
        /// <param name="currencyCode">Currency code</param>
        /// <returns>Money object</returns>
        private static Money PrepareMoney(decimal value, string currencyCode)
        {
            var format = PayPalCommerceDefaults.CurrenciesWithoutDecimals.Contains(currencyCode.ToUpper()) ? "0" : "0.00";
            return new Money
            {
                CurrencyCode = currencyCode,
                Value = value.ToString(format, CultureInfo.InvariantCulture)
            };
        }

        /// <summary>
        /// Convert money object to decimal value
        /// </summary>
        /// <param name="value">Amount value</param>
        /// <returns>Decimal value</returns>
        private static decimal ConvertMoney(Money amount)
        {
            return decimal.TryParse(amount?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : decimal.Zero;
        }

        /// <summary>
        /// Prepare order context
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="details">Shopping cart details</param>
        /// <param name="orderGuid">Order internal id</param>
        /// <param name="isApplePay">Apple Pay payment</param>
        /// <returns>Order context</returns>
        private ExperienceContext PrepareOrderContext(PayPalCommerceSettings settings, CartDetails details, string orderGuid, bool isApplePay = false)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            var protocol = _webHelper.CurrentRequestProtocol;

            var shippingPreference = ShippingPreferenceType.NO_SHIPPING.ToString().ToUpper();
            if (details.ShippingIsRequired)
            {
                shippingPreference = details.Placement == ButtonPlacement.PaymentMethod && !isApplePay
                    ? ShippingPreferenceType.SET_PROVIDED_ADDRESS.ToString().ToUpper()
                    : ShippingPreferenceType.GET_FROM_FILE.ToString().ToUpper();
            }
            var cancelUrl = string.Empty;
            switch (details.Placement)
            {
                case ButtonPlacement.Cart:
                    cancelUrl = urlHelper.RouteUrl(PayPalCommerceDefaults.Route.ShoppingCart, null, protocol);
                    break;
                case ButtonPlacement.Product:
                    cancelUrl = urlHelper.RouteUrl(PayPalCommerceDefaults.Route.ShoppingCart, null, protocol);
                    break;
                case ButtonPlacement.PaymentMethod:
                    cancelUrl = urlHelper.RouteUrl(PayPalCommerceDefaults.Route.PaymentInfo, null, protocol);
                    break;
            }

            return new ExperienceContext
            {
                //Locale = null, //PayPal auto detects this
                BrandName = CommonHelper.EnsureMaximumLength(details.Store.Name, 127),
                LandingPage = LandingPageType.NO_PREFERENCE.ToString().ToUpper(),
                UserAction = details.Placement == ButtonPlacement.PaymentMethod && settings.SkipOrderConfirmPage
                    ? UserActionType.PAY_NOW.ToString().ToUpper()
                    : UserActionType.CONTINUE.ToString().ToUpper(),
                CancelUrl = cancelUrl,
                ReturnUrl = urlHelper.RouteUrl(PayPalCommerceDefaults.Route.ConfirmOrder, new { token = orderGuid, approve = true }, protocol),
                PaymentMethodPreference = settings.ImmediatePaymentRequired
                    ? PaymentMethodPreferenceType.IMMEDIATE_PAYMENT_REQUIRED.ToString().ToUpper()
                    : PaymentMethodPreferenceType.UNRESTRICTED.ToString().ToUpper(),
                ShippingPreference = shippingPreference
            };
        }

        /// <summary>
        /// Prepare order billing details
        /// </summary>
        /// <param name="details">Shopping cart details</param>
        /// <returns>The payer with the billing details</returns>
        private Payer PrepareBillingDetails(CartDetails details)
        {
            var customer = details.Customer;
            var address = details.BillingAddress;
            var isPaymentMethodPage = details.Placement == ButtonPlacement.PaymentMethod;
            var firstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
            var lastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
            var dateOfBirth = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.DateOfBirthAttribute);
            var stateId = _genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.StateProvinceIdAttribute);
            var countryId = _genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.CountryIdAttribute);
            var line1 = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.StreetAddressAttribute);
            var line2 = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.StreetAddress2Attribute);
            var city = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.CityAttribute);
            var zip = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.ZipPostalCodeAttribute);

            var email = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.Email : customer.Email, 254);
            var name = new Name
            {
                GivenName = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.FirstName : firstName, 140),
                Surname = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.LastName : lastName, 140)
            };
            //phone number format is unpredictable
            //var customerPhone = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.PhoneAttribute);
            //var phone = isPaymentMethodPage
            //    ? (!string.IsNullOrEmpty(address.PhoneNumber) ? new Phone { PhoneNumber = new() { NationalNumber = CommonHelper.EnsureMaximumLength(CommonHelper.EnsureNumericOnly(address.PhoneNumber), 14) } } : null)
            //    : !string.IsNullOrEmpty(customerPhone) ? new Phone { PhoneNumber = new() { NationalNumber = CommonHelper.EnsureMaximumLength(CommonHelper.EnsureNumericOnly(customerPhone), 14) } } : null;
            var birthDate = DateTime.TryParse(dateOfBirth, out var dateOfBirthValue) ? dateOfBirthValue.ToString("yyyy-MM-dd") : null;
            var country = _countryService.GetCountryById(isPaymentMethodPage ? address.CountryId ?? 0 : countryId);
            var state = _stateProvinceService
                .GetStateProvinceById(isPaymentMethodPage ? address.StateProvinceId ?? 0 : stateId);
            var billingAddress = new Address
            {
                AddressLine1 = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.Address1 : line1, 300),
                AddressLine2 = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.Address2 : line2, 300),
                AdminArea2 = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.City : city, 120),
                AdminArea1 = CommonHelper.EnsureMaximumLength(state?.Abbreviation, 300),
                CountryCode = CommonHelper.EnsureMaximumLength(country?.TwoLetterIsoCode, 2),
                PostalCode = CommonHelper.EnsureMaximumLength(isPaymentMethodPage ? address.ZipPostalCode : zip, 60)
            };

            //country is required
            if (string.IsNullOrEmpty(billingAddress.CountryCode))
                billingAddress = null;

            return new Payer { EmailAddress = email, Name = name, BirthDate = birthDate, Address = billingAddress };
        }

        /// <summary>
        /// Prepare order items
        /// </summary>
        /// <param name="details">Shopping cart details</param>
        /// <returns>The list of purchase items</returns>
        private List<Item> PrepareOrderItems(CartDetails details)
        {
            //cart items
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            var items = details.Cart.Select(item =>
            {
                var product = _productService.GetProductById(item.ProductId);
                var sku = _productService.FormatSku(product, item.AttributesXml);
                var seName = _urlRecordService.GetSeName(product);
                var url = urlHelper.RouteUrl("Product", new { SeName = seName }, _webHelper.CurrentRequestProtocol);
                var picture = _pictureService.GetProductPicture(product, item.AttributesXml);
                var imageUrl = _pictureService.GetPictureUrl(picture);

                var itemSubTotal = _priceCalculationService.GetSubTotal(item, true, out var itemDiscount, out _, out _);
                var unitPrice = itemSubTotal / item.Quantity;
                var unitPriceExclTax = _taxService.GetProductPrice(product, unitPrice, false, details.Customer, out _);

                return new Item
                {
                    Name = CommonHelper.EnsureMaximumLength(product.Name, 127),
                    Description = CommonHelper.EnsureMaximumLength(product.ShortDescription, 127),
                    Sku = CommonHelper.EnsureMaximumLength(sku, 127),
                    Quantity = item.Quantity.ToString(),
                    Category = product.IsDownload
                        ? CategoryType.DIGITAL_GOODS.ToString().ToUpper()
                        : CategoryType.PHYSICAL_GOODS.ToString().ToUpper(),
                    Url = url,
                    ImageUrl = imageUrl,
                    UnitAmount = PrepareMoney(unitPriceExclTax, details.CurrencyCode)
                };
            }).ToList();

            //and checkout attributes
            var checkoutAttributes = _genericAttributeService
                .GetAttribute<string>(details.Customer, NopCustomerDefaults.CheckoutAttributes, details.Store.Id);
            var checkoutAttributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(checkoutAttributes);
            foreach (var attributeValue in checkoutAttributeValues)
            {
                if (attributeValue.CheckoutAttribute is null)
                    continue;

                var attributePriceExclTax = _taxService.GetCheckoutAttributePrice(attributeValue, false, details.Customer, out _);

                items.Add(new Item
                {
                    Name = CommonHelper.EnsureMaximumLength(attributeValue.CheckoutAttribute.Name, 127),
                    Description = CommonHelper.EnsureMaximumLength($"{attributeValue.CheckoutAttribute.Name} - {attributeValue.Name}", 127),
                    Quantity = 1.ToString(),
                    UnitAmount = PrepareMoney(attributePriceExclTax, details.CurrencyCode)
                });
            }

            return items;
        }

        /// <summary>
        /// Prepare order amount with breakdown
        /// </summary>
        /// <param name="details">Shopping cart details</param>
        /// <param name="items">Purchase items</param>
        /// <returns>The order amount with breakdown</returns>
        private OrderMoney PrepareOrderMoney(CartDetails details, List<Item> items)
        {
            //in some rare cases we need an additional item to adjust the order total
            //this can happen due to complex discounts or a large order and related to rounding in calculations
            //PayPal uses two decimal places, while nopCommerce can use more complex types of rounding (configured for each currency separately) 
            var adjustmentName = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Adjustment.Name");
            var adjustmentDescription = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Adjustment.Description");
            if (items.FirstOrDefault(item => adjustmentName.Equals(item.Name) && adjustmentDescription.Equals(item.Description)) is Item adjustmentItem)
                items.Remove(adjustmentItem);

            var total = _orderTotalCalculationService.GetShoppingCartTotal(details.Cart, usePaymentMethodAdditionalFee: false);
            if (total is null)
            {
                if (details.Placement == ButtonPlacement.PaymentMethod)
                    throw new NopException("Shopping cart total couldn't be calculated now");

                //on product and cart pages the total is not yet calculated, so use subtotal here
                _orderTotalCalculationService.GetShoppingCartSubTotal(details.Cart, includingTax: false, out _, out _, out var subTotal, out _);
                total = subTotal;
            }
            var orderTotal = PrepareMoney(total.Value, details.CurrencyCode);

            var shippingPlugins = _shippingPluginManager.LoadActivePlugins(details.Customer, details.Store.Id);
            var shippingTotal = _orderTotalCalculationService.GetShoppingCartShippingTotal(details.Cart, includingTax: false, shippingPlugins);
            var orderShippingTotal = PrepareMoney(shippingTotal ?? decimal.Zero, details.CurrencyCode);

            var taxTotal = _orderTotalCalculationService.GetTaxTotal(details.Cart, shippingPlugins, usePaymentMethodAdditionalFee: false);
            var orderTaxTotal = PrepareMoney(taxTotal, details.CurrencyCode);

            var itemAdjustment = decimal.Zero;
            var itemTotal = items.Sum(item => ConvertMoney(item.UnitAmount) * int.Parse(item.Quantity));
            var discountTotal = itemTotal + ConvertMoney(orderTaxTotal) + ConvertMoney(orderShippingTotal) - ConvertMoney(orderTotal);
            if (discountTotal < decimal.Zero)
            {
                itemAdjustment = -discountTotal;
                itemTotal += itemAdjustment;
                discountTotal = decimal.Zero;
            }
            var orderItemTotal = PrepareMoney(itemTotal, details.CurrencyCode);
            var orderDiscount = PrepareMoney(discountTotal, details.CurrencyCode);

            //set adjustment item if needed
            if (itemAdjustment > decimal.Zero)
            {
                var unitAmount = PrepareMoney(itemAdjustment, details.CurrencyCode);
                if (ConvertMoney(unitAmount) > decimal.Zero)
                {
                    items.Add(new Item
                    {
                        Name = adjustmentName,
                        Description = adjustmentDescription,
                        Quantity = 1.ToString(),
                        UnitAmount = unitAmount
                    });
                }
            }

            return new OrderMoney
            {
                CurrencyCode = details.CurrencyCode,
                Value = orderTotal.Value,
                Breakdown = new OrderAmountBreakdown
                {
                    ItemTotal = orderItemTotal,
                    TaxTotal = orderTaxTotal,
                    Shipping = orderShippingTotal,
                    Discount = orderDiscount
                }
            };
        }

        /// <summary>
        /// Prepare order shipping details
        /// </summary>
        /// <param name="details">Shopping cart details</param>
        /// <param name="selectedOptionId">Selected shipping option</param>
        /// <param name="isApplePay">Apple Pay payment</param>
        /// <returns>The shipping details</returns>
        private Shipping PrepareShippingDetails(CartDetails details, string selectedOptionId, bool isApplePay = false)
        {
            if (!details.ShippingIsRequired)
                return null;

            var shippingAddress = details.ShippingAddress;
            var fullName = shippingAddress != null && !details.IsPickup
                ? $"{shippingAddress.FirstName} {shippingAddress.LastName}"
                : null;
            if (string.IsNullOrEmpty(fullName))
                fullName = _customerService.GetCustomerFullName(details.Customer);

            //if the shipping option type is set to PICKUP, then the full name should start with S2S meaning ship to store (for example, S2S My Store)
            if (details.IsPickup && details.PickupPoint != null)
                fullName = $"S2S {details.PickupPoint.Name}";

            var address = shippingAddress != null ? new Address
            {
                AddressLine1 = CommonHelper.EnsureMaximumLength(shippingAddress.Address1, 300),
                AddressLine2 = CommonHelper.EnsureMaximumLength(shippingAddress.Address2, 300),
                AdminArea2 = CommonHelper.EnsureMaximumLength(shippingAddress.City, 120),
                AdminArea1 = CommonHelper.EnsureMaximumLength(shippingAddress.StateProvince?.Abbreviation, 300),
                CountryCode = shippingAddress.Country?.TwoLetterIsoCode,
                PostalCode = CommonHelper.EnsureMaximumLength(shippingAddress.ZipPostalCode, 60)
            } : null;

            var shipping = new Shipping
            {
                Name = new Name { FullName = CommonHelper.EnsureMaximumLength(fullName, 300), },
                Address = address
            };

            if (details.Placement == ButtonPlacement.PaymentMethod && !isApplePay)
            {
                shipping.Type = details.IsPickup
                    ? ShippingType.SHIPPING.ToString().ToUpper() //PICKUP_IN_STORE option doesn't work for some reason
                    : ShippingType.SHIPPING.ToString().ToUpper();

                return shipping;
            }

            var (shippingOptions, pickupPoints) = PrepareShippingOptions(details);
            if (!shippingOptions?.Any() ?? true)
                throw new NopException("No available shipping options");

            var selectedShippingOption = shippingOptions.FirstOrDefault();
            if (!string.IsNullOrEmpty(selectedOptionId))
            {
                var existingOption = shippingOptions
                    .FirstOrDefault(option => string.Equals(option.Name, selectedOptionId, StringComparison.InvariantCultureIgnoreCase))
                    ?? throw new NopException("Selected shipping option is unavailable");

                selectedShippingOption = existingOption;
            }

            if (selectedShippingOption is null)
                throw new NopException("Selected shipping option is unavailable");

            PickupPoint pickupPoint = null;
            if (IsPickup(selectedShippingOption))
            {
                pickupPoint = pickupPoints.FirstOrDefault(point =>
                    string.Equals(GetShippingOptionName(new NopShippingOption { Name = point.Name, Description = $"PICKUP|{point.Description}" }), selectedShippingOption.Name) &&
                    string.Equals(point.ProviderSystemName, selectedShippingOption.ShippingRateComputationMethodSystemName));

                details.IsPickup = true;
                details.PickupPoint = pickupPoint;

                //if the shipping option type is set to PICKUP, then the full name should start with S2S meaning ship to store (for example, S2S My Store)
                if (details.IsPickup && details.PickupPoint != null)
                    shipping.Name.FullName = $"S2S {details.PickupPoint.Name}";
            }
            details.ShippingOption = selectedShippingOption;

            //save selected options in attributes
            _genericAttributeService
                .SaveAttribute(details.Customer, NopCustomerDefaults.SelectedShippingOptionAttribute, selectedShippingOption, details.Store.Id);
            _genericAttributeService
                .SaveAttribute(details.Customer, NopCustomerDefaults.SelectedPickupPointAttribute, pickupPoint, details.Store.Id);

            ShippingOption convertOption(NopShippingOption option)
            {
                var adjustedShippingRate = _orderTotalCalculationService.AdjustShippingRate(option.Rate, details.Cart, out _);
                //var (rate, _) = _taxService.GetShippingPrice(adjustedShippingRate, details.Customer);
                //PayPal currently handles taxable shipping incorrectly, so we display shipping rates without tax, but it'll be included to tax total
                var rate = adjustedShippingRate;

                return new ShippingOption
                {
                    Id = CommonHelper.EnsureMaximumLength(option.Name, 127),
                    Label = CommonHelper.EnsureMaximumLength(option.Name, 127),
                    Selected = false,
                    Type = IsPickup(option)
                        ? ShippingType.PICKUP.ToString().ToUpper()
                        : ShippingType.SHIPPING.ToString().ToUpper(),
                    Amount = PrepareMoney(rate, details.CurrencyCode)
                };
            }

            shipping.Options = details.Placement == ButtonPlacement.PaymentMethod
                ? new List<ShippingOption> { convertOption(selectedShippingOption) }
                : shippingOptions.Select(option => convertOption(option)).ToList();

            //set default shipping option
            (shipping.Options
                .FirstOrDefault(option => string.Equals(option.Id, details.ShippingOption?.Name, StringComparison.InvariantCultureIgnoreCase))
                ?? shipping.Options.First())
                .Selected = true;

            return shipping;
        }

        /// <summary>
        /// Prepare available shipping options
        /// </summary>
        /// <param name="details">Shopping cart details</param>
        /// <returns>The list of shipping options; list of pickup points</returns>
        private (List<NopShippingOption> ShippingOptions, List<PickupPoint> PickupPoints) PrepareShippingOptions(CartDetails details)
        {
            if (!details.ShippingIsRequired)
                return (null, null);

            if (details.ShippingAddress is null && !_shippingSettings.AllowPickupInStore)
                return (null, null);

            var shippingOptions = new List<NopShippingOption>();
            var pickupPoints = new List<PickupPoint>();

            //pickup points
            if (_shippingSettings.AllowPickupInStore)
            {
                var pickupPointProviders = _pickupPluginManager.LoadActivePlugins(details.Customer, details.Store.Id);
                if (pickupPointProviders.Any())
                {
                    var pickupPointsResponse = _shippingService
                        .GetPickupPoints(details.Customer.BillingAddress, details.Customer, storeId: details.Store.Id);
                    if (pickupPointsResponse.Success)
                    {
                        shippingOptions.AddRange(pickupPointsResponse.PickupPoints.Select(point => new NopShippingOption
                        {
                            Name = GetShippingOptionName(new NopShippingOption { Name = point.Name, Description = $"PICKUP|{point.Description}" }),
                            Rate = point.PickupFee,
                            Description = $"PICKUP|{point.Description}",
                            ShippingRateComputationMethodSystemName = point.ProviderSystemName
                        }).ToList());
                        pickupPoints.AddRange(pickupPointsResponse.PickupPoints);
                    }
                }
            }

            //and shipping options
            if (details.ShippingAddress != null)
            {
                var shippingOptionResponse = _shippingService
                    .GetShippingOptions(details.Cart, details.ShippingAddress, details.Customer, storeId: details.Store.Id);
                if (shippingOptionResponse.Success)
                    shippingOptions.AddRange(shippingOptionResponse.ShippingOptions);
            }

            //sort options
            shippingOptions = shippingOptions.OrderBy(option => option.Rate).ToList();

            return (shippingOptions, pickupPoints);
        }

        /// <summary>
        /// Prepare the updated shipping details
        /// </summary>
        /// <param name="details">Shopping cart details</param>
        /// <param name="email">Customer email</param>
        /// <param name="selectedAddress">Selected shipping address</param>
        /// <param name="selectedOption">Selected shipping option</param>
        /// <returns>The shipping details</returns>
        private Shipping PrepareUpdatedShipping(CartDetails details, string email,
            (string City, string State, string Country, string PostalCode) selectedAddress,
            (string Id, string Type) selectedOption)
        {
            //change shipping address when customer selects another one
            if (!string.IsNullOrEmpty(selectedAddress.City) && !string.IsNullOrEmpty(selectedAddress.State) &&
                !string.IsNullOrEmpty(selectedAddress.Country) && !string.IsNullOrEmpty(selectedAddress.PostalCode))
            {
                var country = _countryService.GetCountryByTwoLetterIsoCode(selectedAddress.Country);
                var state = _stateProvinceService.GetStateProvinceByAbbreviation(selectedAddress.State, country?.Id);
                var newShippingAddress = PrepareCustomerAddress(details.Customer, new NopAddress
                {
                    Email = email ?? details.Customer.Email,
                    City = selectedAddress.City,
                    StateProvinceId = state?.Id,
                    CountryId = country?.Id,
                    ZipPostalCode = selectedAddress.PostalCode
                });
                if (newShippingAddress.Id != details.Customer.ShippingAddressId)
                {
                    details.Customer.ShippingAddressId = newShippingAddress.Id;
                    _customerService.UpdateCustomer(details.Customer);
                }
            }

            //change shipping option when customer selects another one
            if (string.IsNullOrEmpty(selectedOption.Id))
            {
                var shippingOption = _genericAttributeService
                    .GetAttribute<NopShippingOption>(details.Customer, NopCustomerDefaults.SelectedShippingOptionAttribute, details.Store.Id);
                if (shippingOption != null)
                {
                    var type = IsPickup(shippingOption) ? ShippingType.PICKUP.ToString() : ShippingType.SHIPPING.ToString();
                    selectedOption = (shippingOption.Name, type);
                }
            }

            //set new parameters to update shipping details
            details.BillingAddress = details.Customer.BillingAddress;
            details.ShippingAddress = details.Customer.ShippingAddress;
            details.IsPickup =
                selectedOption.Type?.ToUpper() == ShippingType.PICKUP.ToString() ||
                selectedOption.Type?.ToUpper() == ShippingType.PICKUP_IN_STORE.ToString() ||
                selectedOption.Type?.ToUpper() == ShippingType.PICKUP_FROM_PERSON.ToString();
            if (details.ShippingAddress is null && !details.IsPickup)
                return null;

            var shipping = PrepareShippingDetails(details, selectedOption.Id);

            return shipping;
        }

        /// <summary>
        /// Prepare patches to update an order
        /// </summary>
        /// <param name="purchaseUnit">Purchase unit details</param>
        /// <returns>List of patch objects</returns>
        private static List<Patch<object>> PreparePatches(PurchaseUnit purchaseUnit)
        {
            var patches = new List<Patch<object>>
            {
                new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/amount",
                    Value = purchaseUnit.Amount
                },
                new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/items",
                    Value = purchaseUnit.Items
                },
                new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/supplementary_data/card",
                    Value = purchaseUnit.SupplementaryData.Card
                }
            };

            if (purchaseUnit.Shipping?.Name != null)
            {
                patches.Add(new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/shipping/name",
                    Value = purchaseUnit.Shipping.Name
                });
            }
            if (purchaseUnit.Shipping?.Address != null)
            {
                patches.Add(new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/shipping/address",
                    Value = purchaseUnit.Shipping.Address
                });
            }
            if (purchaseUnit.Shipping?.Options != null)
            {
                patches.Add(new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/shipping/options",
                    Value = purchaseUnit.Shipping.Options
                });
            }
            if (!string.IsNullOrEmpty(purchaseUnit.Shipping?.Type))
            {
                patches.Add(new Patch<object>
                {
                    Op = PatchOpType.REPLACE.ToString().ToLower(),
                    Path = "/purchase_units/@reference_id=='default'/shipping/type",
                    Value = purchaseUnit.Shipping.Type
                });
            }

            return patches;
        }

        /// <summary>
        /// Get customer's address (existing or a new one)
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="newAddress">Address to check</param>
        /// <returns>The customer address</returns>
        private NopAddress PrepareCustomerAddress(Customer customer, NopAddress newAddress)
        {
            var customerAddresses = customer.Addresses;
            var query = customerAddresses.AsQueryable();
            if (!string.IsNullOrEmpty(newAddress.Email))
                query = query.Where(address => string.Equals(address.Email, newAddress.Email));
            if (!string.IsNullOrEmpty(newAddress.FirstName))
                query = query.Where(address => string.Equals(address.FirstName, newAddress.FirstName));
            if (!string.IsNullOrEmpty(newAddress.LastName))
                query = query.Where(address => string.Equals(address.LastName, newAddress.LastName));
            if (!string.IsNullOrEmpty(newAddress.Address1))
                query = query.Where(address => string.Equals(address.Address1, newAddress.Address1));
            if (!string.IsNullOrEmpty(newAddress.Address2))
                query = query.Where(address => string.Equals(address.Address2, newAddress.Address2));
            if (!string.IsNullOrEmpty(newAddress.City))
                query = query.Where(address => string.Equals(address.City, newAddress.City));
            if (newAddress.StateProvinceId > 0)
                query = query.Where(address => address.StateProvinceId == newAddress.StateProvinceId);
            if (newAddress.CountryId > 0)
                query = query.Where(address => address.CountryId == newAddress.CountryId);
            if (!string.IsNullOrEmpty(newAddress.ZipPostalCode))
                query = query.Where(address => string.Equals(address.ZipPostalCode, newAddress.ZipPostalCode));

            var existingAddress = query.FirstOrDefault();
            if (existingAddress != null)
                return existingAddress;

            _addressService.InsertAddress(newAddress);
            customer.Addresses.Add(newAddress);
            _customerService.UpdateCustomer(customer);

            return newAddress;
        }

        /// <summary>
        /// Get shipping option name
        /// </summary>
        /// <param name="option">Shipping option</param>
        /// <returns>The shipping option name</returns>
        private string GetShippingOptionName(NopShippingOption option)
        {
            return IsPickup(option)
                ? (string.IsNullOrEmpty(option.Name)
                ? _localizationService.GetResource("Checkout.PickupPoints.NullName")
                : string.Format(_localizationService.GetResource("Checkout.PickupPoints.Name"), option.Name))
                : option.Name;
        }

        /// <summary>
        /// Check whether the shipping option is pickup point
        /// </summary>
        /// <param name="option">Shipping option</param>
        /// <returns>Result</returns>
        private bool IsPickup(NopShippingOption option)
        {
            return string.Equals("PICKUP", option.Description?.Split('|').FirstOrDefault(), StringComparison.InvariantCultureIgnoreCase);
        }

        #endregion

        #region Payment tokens

        /// <summary>
        /// Prepare payment tokens with additional details
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="tokens">Payment tokens</param>
        /// <returns>The list of payment tokens</returns>
        private List<PayPalToken> PreparePaymentTokens(PayPalCommerceSettings settings, IList<PayPalToken> tokens)
        {
            var paymentTokens = new List<PaymentToken>();

            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token.VaultCustomerId))
                    continue;

                //try to get payment tokens from the vault
                var response = _httpClient
                    .Request<GetPaymentTokensRequest, GetPaymentTokensResponse>(new GetPaymentTokensRequest { VaultCustomerId = token.VaultCustomerId }, settings);
                paymentTokens.AddRange(response?.PaymentTokens ?? new List<PaymentToken>());
            }
            if (paymentTokens?.Any() != true)
                return new List<PayPalToken>();

            return tokens.OrderBy(token => token.IsPrimaryMethod ? 0 : 1).ThenBy(token => token.Id).Select(paymentToken =>
            {
                var existingToken = paymentTokens
                    .FirstOrDefault(token => string.Equals(token.Id, paymentToken.VaultId, StringComparison.InvariantCultureIgnoreCase));
                if (existingToken is null)
                    return null;

                //set title and expiration date
                paymentToken.Title = existingToken.PaymentSource?.Card != null
                    ? $"{existingToken.PaymentSource.Card.Brand} *{existingToken.PaymentSource.Card.LastDigits}"
                    : (existingToken.PaymentSource?.Venmo != null
                    ? existingToken.PaymentSource.Venmo.UserName
                    : (existingToken.PaymentSource?.PayPal != null
                    ? existingToken.PaymentSource.PayPal.EmailAddress
                    : "N/A"));
                paymentToken.Expiration = existingToken.PaymentSource?.Card != null ? existingToken.PaymentSource.Card.Expiry : "N/A";

                return paymentToken;
            }).Where(token => token != null).ToList();
        }

        #endregion

        #region Common

        /// <summary>
        /// Get calculated SHA256 hash for the input string
        /// </summary>
        /// <param name="stringToHash">Input string for the hash</param>
        /// <returns>SHA256 hash</returns>
        private static string GetSha256Hash(string stringToHash)
        {
            return new SHA256Managed().ComputeHash(Encoding.Default.GetBytes(stringToHash))
                .Aggregate(string.Empty, (current, next) => $"{current}{next:x2}");
        }

        /// <summary>
        /// Generate an order GUID
        /// </summary>
        /// <param name="processPaymentRequest">Process payment request</param>
        private void GenerateOrderGuid(ProcessPaymentRequest processPaymentRequest)
        {
            if (processPaymentRequest == null)
                return;

            var previousPaymentRequest = _actionContextAccessor.ActionContext.HttpContext.Session.Get<ProcessPaymentRequest>("OrderPaymentInfo");
            if (_paymentSettings.RegenerateOrderGuidInterval > 0 &&
                previousPaymentRequest != null &&
                previousPaymentRequest.OrderGuidGeneratedOnUtc.HasValue)
            {
                var interval = DateTime.UtcNow - previousPaymentRequest.OrderGuidGeneratedOnUtc.Value;
                if (interval.TotalSeconds < _paymentSettings.RegenerateOrderGuidInterval)
                {
                    processPaymentRequest.OrderGuid = previousPaymentRequest.OrderGuid;
                    processPaymentRequest.OrderGuidGeneratedOnUtc = previousPaymentRequest.OrderGuidGeneratedOnUtc;
                }
            }

            if (processPaymentRequest.OrderGuid == Guid.Empty)
            {
                processPaymentRequest.OrderGuid = Guid.NewGuid();
                processPaymentRequest.OrderGuidGeneratedOnUtc = DateTime.UtcNow;
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Convert object properties to a dictionary
        /// </summary>
        /// <param name="data">Object to convert</param>
        /// <returns>Dictionary of properties names and values</returns>
        public static Dictionary<string, string> ObjectToDictionary(object data)
        {
            return new Dictionary<string, string>(data.GetType().GetProperties().Select(property =>
            {
                var key = property
                    ?.GetCustomAttributes(typeof(JsonPropertyAttribute), false).OfType<JsonPropertyAttribute>().FirstOrDefault()
                    ?.PropertyName ?? string.Empty;
                var value = property.GetValue(data) is bool boolValue
                    ? boolValue.ToString().ToLower()
                    : property.GetValue(data)?.ToString();
                return new KeyValuePair<string, string>(key, value);
            }).Where(pair => !string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value)));
        }

        #region Configuration

        /// <summary>
        /// Check whether the plugin is configured
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>Result</returns>
        public static bool IsConfigured(PayPalCommerceSettings settings)
        {
            //client id and secret are required to request remote services
            return !string.IsNullOrEmpty(settings?.ClientId) && !string.IsNullOrEmpty(settings.SecretKey);
        }

        /// <summary>
        /// Check whether the plugin is configured and connected
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>Result</returns>
        public static bool IsConnected(PayPalCommerceSettings settings)
        {
            //webhook is required to accept notifications
            return IsConfigured(settings) && !string.IsNullOrEmpty(settings.WebhookUrl);
        }

        /// <summary>
        /// Check whether the plugin is configured, connected and active
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>The check result; plugin instance</returns>
        public (bool Active, IPaymentMethod paymentMethod) IsActive(PayPalCommerceSettings settings)
        {
            if (!IsConnected(settings))
                return (false, null);

            var customer = _workContext.CurrentCustomer;
            var store = _storeContext.CurrentStore;
            var plugin = _paymentPluginManager.LoadPluginBySystemName(PayPalCommerceDefaults.SystemName, customer, store.Id);
            if (!_paymentPluginManager.IsPluginActive(plugin))
                return (false, plugin);

            return (true, plugin);
        }

        /// <summary>
        /// Get access token
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>The access token; error message if exists</returns>
        public (AccessToken AccessToken, string Error) GetAccessToken(PayPalCommerceSettings settings)
        {
            return HandleFunction(() =>
            {
                return _httpClient.Request<GetAccessTokenRequest, GetAccessTokenResponse>(new GetAccessTokenRequest
                {
                    ClientId = settings.ClientId,
                    Secret = settings.SecretKey,
                    GrantType = "client_credentials"
                }, settings);
            });
        }

        #endregion

        #region Components

        /// <summary>
        /// Prepare details to render payment buttons/messages
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="placement">Button placement</param>
        /// <param name="productId">Product id</param>
        /// <returns>The script details, customer details, messages details; error message if exists</returns>
        public (((string ScriptUrl, string ClientToken, string UserToken),
            (string Email, string Name),
            (string MessageConfig, string Amount)),
            string Error)
            PreparePaymentDetails(PayPalCommerceSettings settings, ButtonPlacement placement, int? productId)
        {
            return HandleFunction(() =>
            {
                //get the primary store currency
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                //customer details
                var customer = _workContext.CurrentCustomer;
                var isGuest = customer.IsGuest();
                var address = customer.BillingAddress;
                var email = address != null ? address.Email : customer.Email;
                var fullName = address != null
                    ? $"{address.FirstName} {address.LastName}"
                    : _customerService.GetCustomerFullName(customer);

                //prepare script components
                var components = new List<string>() { "buttons", "funding-eligibility" };
                if (placement == ButtonPlacement.PaymentMethod && settings.UseCardFields)
                    components.Add("card-fields");
                if (placement == ButtonPlacement.PaymentMethod && settings.UseAlternativePayments)
                    components.Add("payment-fields");
                if (settings.UseApplePay && placement != ButtonPlacement.Product)
                    components.Add("applepay");
                if (settings.UseGooglePay)
                    components.Add("googlepay");
                if (settings.UseSandbox || settings.ConfiguratorSupported)
                    components.Add("messages");

                var script = new Script
                {
                    ClientId = settings.ClientId,
                    Currency = currencyCode.ToUpper(),
                    Intent = settings.PaymentType.ToString().ToLower(),
                    Commit = placement == ButtonPlacement.PaymentMethod && settings.SkipOrderConfirmPage,
                    Components = string.Join(',', components),
                    EnableFunding = settings.EnabledFunding,
                    DisableFunding = settings.DisabledFunding,
                    Vault = settings.UseVault && !isGuest,
                    Debug = false,
                    //BuyerCountry = null,    //PayPal auto detects this
                    //Locale = null,          //PayPal auto detects this
                    //IntegrationDate = null  //defaults to the date when client ID was created
                };

                var scriptUrl = QueryHelpers.AddQueryString(PayPalCommerceDefaults.ServiceScriptUrl, ObjectToDictionary(script));

                //client token is required for Advanced Credit and Debit Card Payments
                string clientToken = null;
                if (placement == ButtonPlacement.PaymentMethod && settings.UseCardFields)
                {
                    var identityToken = _httpClient.Request<CreateIdentityTokenRequest, CreateIdentityTokenResponse>(new CreateIdentityTokenRequest
                    {
                        CustomerId = CommonHelper.EnsureMaximumLength(GetSha256Hash(customer.CustomerGuid.ToString()), 22)
                    }, settings);
                    clientToken = identityToken?.ClientToken;
                }

                //user ID token is required for Vault feature
                string userToken = null;
                if (settings.UseVault && !isGuest)
                {
                    var tokens = _tokenService.GetAllTokens(settings.ClientId, customer.Id);
                    var vaultCustomerId = tokens
                        .OrderBy(token => token.IsPrimaryMethod ? 0 : 1)
                        .ThenBy(token => token.Id)
                        .FirstOrDefault()
                        ?.VaultCustomerId;
                    var accessToken = _httpClient.Request<GetAccessTokenRequest, GetAccessTokenResponse>(new GetAccessTokenRequest
                    {
                        ClientId = settings.ClientId,
                        Secret = settings.SecretKey,
                        GrantType = "client_credentials",
                        ResponseType = "id_token",
                        TargetCustomerId = vaultCustomerId
                    }, settings);
                    userToken = accessToken?.UserIdToken;
                }

                //Pay Later details
                var amount = PrepareMessagesAmount(placement, customer, currencyCode, productId);
                var payLaterConfig = new
                {
                    cart = new MessageConfiguration(),
                    product = new MessageConfiguration(),
                    checkout = new MessageConfiguration()
                };
                payLaterConfig = JsonConvert.DeserializeAnonymousType(settings.PayLaterConfig ?? string.Empty, payLaterConfig);
                var config = new MessageConfiguration();
                switch (placement)
                {
                    case ButtonPlacement.Cart:
                        config = payLaterConfig?.cart;
                        break;
                    case ButtonPlacement.Product:
                        config = payLaterConfig?.product;
                        break;
                    case ButtonPlacement.PaymentMethod:
                        config = payLaterConfig?.checkout;
                        break;
                }
                var messageConfig = !string.IsNullOrEmpty(config?.Status) ? JsonConvert.SerializeObject(config, Formatting.Indented) : "{}";

                return ((scriptUrl, clientToken, userToken), (email, fullName), (messageConfig, amount));
            });
        }

        /// <summary>
        /// Prepare details to render Pay Later messages
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="placement">Button placement</param>
        /// <returns>The message configuration, amount value, currency code; error message if exists</returns>
        public ((string Config, string Amount, string CurrencyCode), string Error)
            PrepareMessages(PayPalCommerceSettings settings, ButtonPlacement placement)
        {
            return HandleFunction(() =>
            {
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var amount = PrepareMessagesAmount(placement, customer, currencyCode, null);

                var payLaterConfig = new
                {
                    cart = new MessageConfiguration(),
                    product = new MessageConfiguration(),
                    checkout = new MessageConfiguration()
                };
                payLaterConfig = JsonConvert.DeserializeAnonymousType(settings.PayLaterConfig ?? string.Empty, payLaterConfig);
                var config = new MessageConfiguration();
                switch (placement)
                {
                    case ButtonPlacement.Cart:
                        config = payLaterConfig?.cart;
                        break;
                    case ButtonPlacement.Product:
                        config = payLaterConfig?.product;
                        break;
                    case ButtonPlacement.PaymentMethod:
                        config = payLaterConfig?.checkout;
                        break;
                }
                var messageConfig = !string.IsNullOrEmpty(config?.Status) ? JsonConvert.SerializeObject(config, Formatting.Indented) : "{}";

                return (messageConfig, amount, currencyCode);
            });
        }

        #endregion

        #region Checkout

        /// <summary>
        /// Check whether the checkout is enabled for the customer
        /// </summary>
        /// <returns>The check results; shopping cart</returns>
        public (bool IsEnabled, bool LoginIsRequired, IList<ShoppingCartItem> Cart) CheckoutIsEnabled()
        {
            return HandleFunction(() =>
            {
                if (_orderSettings.CheckoutDisabled)
                    return (false, false, null);

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    return (false, false, null);

                if (customer.IsGuest())
                {
                    if (!_orderSettings.AnonymousCheckoutAllowed)
                        return (true, true, cart);

                    var downloadableProductsRequireRegistration = _customerSettings.RequireRegistrationForDownloadableProducts &&
                        cart.Any(sci => sci.Product.IsDownload);
                    if (downloadableProductsRequireRegistration)
                        return (true, true, cart);
                }

                return (true, false, cart);
            }, false).Result;
        }

        /// <summary>
        /// Check whether the shopping cart is valid
        /// </summary>
        /// <returns>The validation warnings; error message if exists</returns>
        public (IList<string> Warnings, string Error) ValidateShoppingCart()
        {
            return HandleFunction(() =>
            {
                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                _customerService.ResetCheckoutData(customer, store.Id, clearShippingMethod: false);

                var checkoutAttributesXml = _genericAttributeService
                    .GetAttribute<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id);
                var cartWarnings = _shoppingCartService.GetShoppingCartWarnings(cart, checkoutAttributesXml, true);
                if (cartWarnings.Any())
                    return cartWarnings;

                foreach (var item in cart)
                {
                    var product = _productService.GetProductById(item.ProductId);

                    var itemWarnings = _shoppingCartService
                        .GetShoppingCartItemWarnings(customer, item.ShoppingCartType, product, item.StoreId, item.AttributesXml,
                        item.CustomerEnteredPrice, item.RentalStartDateUtc, item.RentalEndDateUtc, item.Quantity, false, item.Id);
                    if (itemWarnings.Any())
                        return itemWarnings;
                }

                return null;
            });
        }

        /// <summary>
        /// Check whether the shipping is required for the current cart/product
        /// </summary>
        /// <param name="productId">Product id</param>
        /// <returns>The check result; error message if exists</returns>
        public (bool ShippingIsRequired, string Error) CheckShippingIsRequired(int? productId)
        {
            return HandleFunction(() =>
            {
                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);

                if (!shippingIsRequired && _productService.GetProductById(productId ?? 0) is Product product)
                    shippingIsRequired = product.IsShipEnabled;

                return shippingIsRequired;
            }, false);
        }

        #endregion

        #region Orders

        /// <summary>
        /// Get the order by id
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="orderId">Order id</param>
        /// <returns>The order; error message if exists</returns>
        public (Order Order, string Error) GetOrder(PayPalCommerceSettings settings, string orderId)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var paymentRequest = _actionContextAccessor.ActionContext.HttpContext.Session
                    .Get<ProcessPaymentRequest>(PayPalCommerceDefaults.PaymentRequestSessionKey)
                    ?? throw new NopException("Order payment info not found");

                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                    !string.Equals(orderIdValue.ToString(), orderId, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                var placementKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Placement");
                if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                    !Enum.TryParse<ButtonPlacement>(placementValue.ToString(), out var placement))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                var order = _httpClient.Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = orderId }, settings);

                return order;
            });
        }

        /// <summary>
        /// Get a previously created order if exists
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="paymentRequest">Payment request</param>
        /// <param name="placement">Button placement</param>
        /// <param name="shippingIsRequired">Whether the shipping is required (used for validation)</param>
        /// <param name="paymentSource">Payment source</param>
        /// <returns>The created order; error message if exists</returns>
        public (Order Order, string Error) GetCreatedOrder(PayPalCommerceSettings settings,
            ProcessPaymentRequest paymentRequest, ButtonPlacement placement, bool shippingIsRequired, string paymentSource)
        {
            return HandleFunction(() =>
            {
                if (paymentRequest is null)
                    return null;

                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) || string.IsNullOrEmpty(orderIdValue.ToString()))
                    return null;

                var placementKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Placement");
                if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                    !Enum.TryParse<ButtonPlacement>(placementValue.ToString(), out var previousPlacement) ||
                    previousPlacement != placement)
                {
                    return null;
                }

                var order = _httpClient
                    .Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = orderIdValue.ToString() }, settings);

                //we cannot use completed order
                if (order.Status?.ToUpper() != OrderStatusType.CREATED.ToString() &&
                    order.Status?.ToUpper() != OrderStatusType.PAYER_ACTION_REQUIRED.ToString() &&
                    order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString())
                {
                    return null;
                }

                //check validity interval
                if (!DateTime.TryParse(order.CreateTime, out var createTime) ||
                    (DateTime.UtcNow - createTime.ToUniversalTime()).TotalSeconds > settings.OrderValidityInterval)
                {
                    return null;
                }

                var unit = order.PurchaseUnits.FirstOrDefault();
                if (unit is null || (unit.Shipping is null != !shippingIsRequired))
                    return null;

                //payment sources must match
                if (string.Equals(paymentSource, nameof(PaymentSource.PayPal), StringComparison.InvariantCultureIgnoreCase) &&
                    order.PaymentSource?.PayPal is null)
                {
                    return null;
                }

                if (string.Equals(paymentSource, nameof(PaymentSource.Card), StringComparison.InvariantCultureIgnoreCase) &&
                    order.PaymentSource?.Card is null)
                {
                    return null;
                }

                return order;
            }, false);
        }

        /// <summary>
        /// Create an order
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="placement">Button placement</param>
        /// <param name="paymentSource">Payment source</param>
        /// <param name="cardId">Saved card id</param>
        /// <param name="saveCard">Whether to save card payment token</param>
        /// <returns>The created order; error message if exists</returns>
        public (Order Order, string Error) CreateOrder(PayPalCommerceSettings settings,
            ButtonPlacement placement, string paymentSource, int? cardId, bool saveCard)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                if (string.IsNullOrEmpty(settings.MerchantId))
                    throw new NopException("Merchant PayPal ID not set");

                //get the primary store currency
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                var billingAddress = _addressService.GetAddressById(customer.BillingAddressId ?? 0);
                if (placement == ButtonPlacement.PaymentMethod && billingAddress is null)
                    throw new NopException("Customer billing address not set");

                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);
                var shippingOption = _genericAttributeService
                    .GetAttribute<NopShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);
                var pickupPoint = _genericAttributeService
                    .GetAttribute<PickupPoint>(customer, NopCustomerDefaults.SelectedPickupPointAttribute, store.Id);
                var pickupInStore = _shippingSettings.AllowPickupInStore && pickupPoint != null;
                var shippingAddress = pickupInStore ? new NopAddress
                {
                    Address1 = pickupPoint.Address,
                    City = pickupPoint.City,
                    County = pickupPoint.County,
                    CountryId = _countryService.GetCountryByTwoLetterIsoCode(pickupPoint.CountryCode)?.Id,
                    StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(pickupPoint.StateAbbreviation,
                        _countryService.GetCountryByTwoLetterIsoCode(pickupPoint.CountryCode)?.Id)?.Id,
                    ZipPostalCode = pickupPoint.ZipPostalCode,
                    CreatedOnUtc = DateTime.UtcNow
                } : _addressService.GetAddressById(customer.ShippingAddressId ?? 0);
                if (placement == ButtonPlacement.PaymentMethod && shippingIsRequired && shippingAddress is null)
                    throw new NopException("Customer shipping address not set");

                var savedPaymentToken = _tokenService.GetById(cardId ?? 0);
                if (savedPaymentToken != null && savedPaymentToken.CustomerId != customer.Id)
                    throw new NopException("Card details not found");

                var paymentRequest = _actionContextAccessor.ActionContext.HttpContext.Session
                    .Get<ProcessPaymentRequest>(PayPalCommerceDefaults.PaymentRequestSessionKey);
                var (order, _) = GetCreatedOrder(settings, paymentRequest, placement, shippingIsRequired, paymentSource);
                if (paymentRequest is null || order is null)
                {
                    paymentRequest = new ProcessPaymentRequest();
                    GenerateOrderGuid(paymentRequest);
                }
                var orderGuid = paymentRequest.OrderGuid.ToString();

                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    Cart = cart.ToList(),
                    CurrencyCode = currencyCode,
                    BillingAddress = billingAddress,
                    ShippingAddress = shippingAddress,
                    ShippingIsRequired = shippingIsRequired,
                    IsPickup = pickupInStore,
                    ShippingOption = shippingOption,
                    PickupPoint = pickupPoint
                };

                //prepare purchase unit
                var shipping = PrepareShippingDetails(details, shippingOption?.Name);
                var items = PrepareOrderItems(details);
                var orderAmount = PrepareOrderMoney(details, items);
                var cardData = new CardData
                {
                    Level2 = new CardDataLevel2 { InvoiceId = CommonHelper.EnsureMaximumLength(orderGuid, 127) },
                    Level3 = new CardDataLevel3
                    {
                        LineItems = items,
                        ShippingAmount = orderAmount.Breakdown.Shipping,
                        ShippingAddress = shipping?.Address,
                        ShipsFromPostalCode = CommonHelper.EnsureMaximumLength((_addressService
                            .GetAddressById(_shippingSettings.ShippingOriginAddressId))?.ZipPostalCode, 60)
                    },
                };
                var purchaseUnit = new PurchaseUnit
                {
                    CustomId = CommonHelper.EnsureMaximumLength(orderGuid, 127),
                    InvoiceId = CommonHelper.EnsureMaximumLength(orderGuid, 127),
                    Description = CommonHelper.EnsureMaximumLength($"Purchase at '{store.Name}'", 127),
                    SoftDescriptor = CommonHelper.EnsureMaximumLength(store.Name, 22),
                    Payee = new Payee { MerchantId = settings.MerchantId },
                    Items = items,
                    Amount = orderAmount,
                    Shipping = shipping,
                    SupplementaryData = new SupplementaryData { Card = cardData }
                };

                //whether we should create a new order
                if (order is null)
                {
                    var isCard = string.Equals(paymentSource, nameof(PaymentSource.Card), StringComparison.InvariantCultureIgnoreCase);
                    var isVenmo = string.Equals(paymentSource, nameof(PaymentSource.Venmo), StringComparison.InvariantCultureIgnoreCase);
                    var isApplepay = string.Equals(paymentSource, nameof(PaymentSource.ApplePay), StringComparison.InvariantCultureIgnoreCase);

                    var context = PrepareOrderContext(settings, details, orderGuid, isApplepay);
                    var payer = PrepareBillingDetails(details);

                    //only registered customers can save payment tokens
                    var isGuest = customer.IsGuest();
                    var vault = !settings.UseVault || isGuest ? null : new VaultInstruction
                    {
                        UsageType = VaultUsageType.MERCHANT.ToString().ToUpper(),
                        CustomerType = VaultUsageType.CONSUMER.ToString().ToUpper(),
                        StoreInVault = VaultInstructionType.ON_SUCCESS.ToString().ToUpper(),
                        PermitMultiplePaymentTokens = false
                    };
                    if (vault != null)
                    {
                        payer.Id = (_tokenService.GetAllTokens(settings.ClientId, customer.Id))
                            .OrderBy(token => token.IsPrimaryMethod ? 0 : 1)
                            .ThenBy(token => token.Type == nameof(PaymentSource.Card) ? 0 : 1)
                            .ThenBy(token => token.Id)
                            .FirstOrDefault()
                            ?.VaultCustomerId;
                    }

                    //set payment source
                    var paymentSourceDetails = new PaymentSource();
                    if (isCard)
                    {
                        paymentSourceDetails.Card = new Card
                        {
                            ExperienceContext = context,
                            BillingAddress = !string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? null : payer.Address,
                            VaultId = savedPaymentToken?.VaultId,
                            Attributes = vault is null || !saveCard || !string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? null : new Attributes
                            {
                                Vault = vault,
                                Customer = payer
                            }
                        };

                        if (vault != null && (saveCard || !string.IsNullOrEmpty(savedPaymentToken?.VaultId)))
                        {
                            paymentSourceDetails.Card.StoredCredential = new StoredCredential
                            {
                                PaymentInitiator = PaymentInitiatorType.CUSTOMER.ToString().ToUpper(),
                                PaymentType = Api.Models.Enums.PaymentType.ONE_TIME.ToString().ToUpper(),
                                Usage = saveCard
                                    ? StoredPaymentUsageType.FIRST.ToString().ToUpper()
                                    : StoredPaymentUsageType.SUBSEQUENT.ToString().ToUpper()
                            };
                        }

                        if (placement == ButtonPlacement.PaymentMethod && settings.UseCardFields)
                        {
                            paymentSourceDetails.Card.Attributes = new Attributes
                            {
                                Vault = vault != null && saveCard && string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? vault : null,
                                Customer = vault != null && saveCard && string.IsNullOrEmpty(savedPaymentToken?.VaultId) ? payer : null,
                                Verification = new VerificationInstruction
                                {
                                    Method = settings.CustomerAuthenticationRequired
                                        ? VerificationInstructionMethodType.SCA_ALWAYS.ToString().ToUpper()
                                        : VerificationInstructionMethodType.SCA_WHEN_REQUIRED.ToString().ToUpper()
                                }
                            };
                        }
                    }
                    else if (isVenmo)
                    {
                        paymentSourceDetails.Venmo = new Venmo
                        {
                            ExperienceContext = context,
                            EmailAddress = payer.EmailAddress,
                            Attributes = vault != null ? new Attributes { Vault = vault, Customer = payer } : null
                        };
                    }
                    else
                    {
                        paymentSourceDetails.PayPal = new PayPal
                        {
                            ExperienceContext = context,
                            EmailAddress = payer.EmailAddress,
                            Name = payer.Name,
                            BirthDate = payer.BirthDate,
                            Address = payer.Address,
                            Attributes = vault != null ? new Attributes { Vault = vault, Customer = payer } : null
                        };
                    }

                    order = _httpClient.Request<CreateOrderRequest, CreateOrderResponse>(new CreateOrderRequest
                    {
                        Intent = settings.PaymentType.ToString().ToUpper(),
                        PaymentSource = paymentSourceDetails,
                        PurchaseUnits = new List<PurchaseUnit> { purchaseUnit }
                    }, settings);
                }
                else
                {
                    //order exists, so just update some details
                    var patches = PreparePatches(purchaseUnit);
                    patches.Add(new Patch<object>
                    {
                        Op = PatchOpType.REPLACE.ToString().ToLower(),
                        Path = "/intent",
                        Value = settings.PaymentType.ToString().ToUpper()
                    });
                    var updateRequest = new UpdateOrderRequest<object>(patches) { OrderId = order.Id };
                    _httpClient.Request<UpdateOrderRequest<object>, EmptyResponse>(updateRequest, settings);
                }

                //save order details for future using as the payment request
                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                paymentRequest.CustomValues[orderIdKey] = order.Id;
                var placementKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Placement");
                paymentRequest.CustomValues[placementKey] = placement.ToString();
                _actionContextAccessor.ActionContext.HttpContext.Session.Set(PayPalCommerceDefaults.PaymentRequestSessionKey, paymentRequest);

                return order;
            });
        }

        /// <summary>
        /// Update order shipping details
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="orderId">Order id</param>
        /// <param name="selectedAddress">Selected shipping address</param>
        /// <param name="selectedOption">Selected shipping option</param>
        /// <returns>The result of update; error message if exists</returns>
        public (bool Result, string Error) UpdateOrderShipping(PayPalCommerceSettings settings, string orderId,
            (string City, string State, string Country, string PostalCode) selectedAddress,
            (string Id, string Type) selectedOption)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                var paymentRequest = _actionContextAccessor.ActionContext.HttpContext.Session
                    .Get<ProcessPaymentRequest>(PayPalCommerceDefaults.PaymentRequestSessionKey)
                    ?? throw new NopException("Order payment info not found");

                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                    (!string.IsNullOrEmpty(orderId) && !string.Equals(orderIdValue.ToString(), orderId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                var placementKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Placement");
                if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                    !Enum.TryParse<ButtonPlacement>(placementValue.ToString(), out var placement))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                //changing shipping address or option on the payment method page is not available
                if (placement == ButtonPlacement.PaymentMethod)
                    return false;

                //check the order status
                var order = _httpClient
                    .Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = orderIdValue.ToString() }, settings);
                if (order.Status?.ToUpper() != OrderStatusType.CREATED.ToString() &&
                    order.Status?.ToUpper() != OrderStatusType.PAYER_ACTION_REQUIRED.ToString() &&
                    order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString())
                {
                    throw new NopException($"Order is in '{order.Status}' status");
                }

                var unit = order.PurchaseUnits.FirstOrDefault();
                if (unit is null || !string.Equals(unit.CustomId, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    throw new NopException("Failed to get PayPal order info");

                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);
                if (!shippingIsRequired)
                    return false;

                //update shipping details
                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    Cart = cart.ToList(),
                    CurrencyCode = currencyCode,
                    ShippingIsRequired = shippingIsRequired
                };
                var shipping = PrepareUpdatedShipping(details, order.Payer?.EmailAddress, selectedAddress, selectedOption);
                if (shipping is null)
                    return false;

                //recalculate the total and update the items, since the shipping price may have changed
                var items = PrepareOrderItems(details);
                var orderAmount = PrepareOrderMoney(details, items);
                var cardData = new CardData
                {
                    Level2 = new CardDataLevel2 { InvoiceId = CommonHelper.EnsureMaximumLength(paymentRequest.OrderGuid.ToString(), 127) },
                    Level3 = new CardDataLevel3
                    {
                        LineItems = items,
                        ShippingAmount = orderAmount.Breakdown.Shipping,
                        ShippingAddress = shipping?.Address,
                        ShipsFromPostalCode = CommonHelper.EnsureMaximumLength((_addressService
                            .GetAddressById(_shippingSettings.ShippingOriginAddressId))?.ZipPostalCode, 60)
                    },
                };

                var patches = PreparePatches(new PurchaseUnit
                {
                    Shipping = shipping,
                    Items = items,
                    Amount = orderAmount,
                    SupplementaryData = new SupplementaryData { Card = cardData }
                });
                var updateRequest = new UpdateOrderRequest<object>(patches) { OrderId = order.Id };
                _httpClient.Request<UpdateOrderRequest<object>, EmptyResponse>(updateRequest, settings);

                return true;
            });
        }

        /// <summary>
        /// The method is called after the customer approves the transaction
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="orderId">Order id</param>
        /// <param name="orderGuid">Internal order id</param>
        /// <param name="liabilityShift">Liability shift</param>
        /// <returns>The order; whether to process the payment immediately; error message if exists</returns>
        public ((Order Order, bool PayNow), string Error)
            OrderIsApproved(PayPalCommerceSettings settings, string orderId, string orderGuid, string liabilityShift)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                var paymentRequest = _actionContextAccessor.ActionContext.HttpContext.Session
                    .Get<ProcessPaymentRequest>(PayPalCommerceDefaults.PaymentRequestSessionKey)
                    ?? throw new NopException("Order payment info not found");

                if (!string.IsNullOrEmpty(orderGuid) &&
                    !string.Equals(orderGuid, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                if (!paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                    (!string.IsNullOrEmpty(orderId) && !string.Equals(orderIdValue.ToString(), orderId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                var placementKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Placement");
                if (!paymentRequest.CustomValues.TryGetValue(placementKey, out var placementValue) ||
                    !Enum.TryParse<ButtonPlacement>(placementValue.ToString(), out var placement))
                {
                    throw new NopException("Failed to get PayPal order info");
                }

                //check the order status
                var order = _httpClient
                    .Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = orderIdValue.ToString() }, settings);
                if (order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString() && order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
                {
                    if (order.Status?.ToUpper() == OrderStatusType.CREATED.ToString())
                    {
                        if (liabilityShift?.ToUpper() == LiabilityShiftType.NO.ToString())
                            throw new NopException($"3D Secure contingency is not resolved");

                        if (liabilityShift?.ToUpper() == LiabilityShiftType.UNKNOWN.ToString())
                            throw new NopException($"The authentication system isn't available, please retry later");
                    }
                    else
                        throw new NopException($"Order is in '{order.Status}' status");
                }

                var unit = order.PurchaseUnits.FirstOrDefault();
                if (unit is null || !string.Equals(unit.CustomId, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    throw new NopException("Failed to get PayPal order info");

                _genericAttributeService
                    .SaveAttribute(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, PayPalCommerceDefaults.SystemName, store.Id);

                //place order immediately, once order is completed
                if (order.Status.ToUpper() == OrderStatusType.COMPLETED.ToString())
                    return (order, true);

                var pickupPoint = _genericAttributeService
                    .GetAttribute<PickupPoint>(customer, NopCustomerDefaults.SelectedPickupPointAttribute, store.Id);
                var pickupInStore = _shippingSettings.AllowPickupInStore && pickupPoint != null;
                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    Cart = cart.ToList(),
                    CurrencyCode = currencyCode,
                    IsPickup = pickupInStore,
                    PickupPoint = pickupPoint
                };

                //recalculate the total and update the items, since the amounts may have changed
                var items = PrepareOrderItems(details);
                var orderAmount = PrepareOrderMoney(details, items);
                var cardData = new CardData
                {
                    Level2 = new CardDataLevel2 { InvoiceId = CommonHelper.EnsureMaximumLength(paymentRequest.OrderGuid.ToString(), 127) },
                    Level3 = new CardDataLevel3
                    {
                        LineItems = items,
                        ShippingAmount = orderAmount.Breakdown.Shipping,
                        ShippingAddress = unit.Shipping?.Address,
                        ShipsFromPostalCode = CommonHelper.EnsureMaximumLength((_addressService
                            .GetAddressById(_shippingSettings.ShippingOriginAddressId))?.ZipPostalCode, 60)
                    },
                };

                var patches = PreparePatches(new PurchaseUnit
                {
                    //if the shipping option type is set to PICKUP, then the full name should start with S2S meaning ship to store (for example, S2S My Store)
                    Shipping = details.IsPickup && details.PickupPoint != null
                        ? new Shipping { Name = new Name { FullName = $"S2S {details.PickupPoint.Name}" } }
                        : null,
                    Items = items,
                    Amount = orderAmount,
                    SupplementaryData = new SupplementaryData { Card = cardData }
                });
                var updateRequest = new UpdateOrderRequest<object>(patches) { OrderId = order.Id };
                _httpClient.Request<UpdateOrderRequest<object>, EmptyResponse>(updateRequest, settings);

                //place order immediately, if the appropriate setting is enabled
                if (placement == ButtonPlacement.PaymentMethod)
                    return (order, settings.SkipOrderConfirmPage);

                //or update billing details and redirect customer to the confirmation page
                if (order.Payer != null)
                {
                    var firstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
                    var lastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
                    var billingCountry = _countryService.GetCountryByTwoLetterIsoCode(order.Payer.Address?.CountryCode);
                    var billingState = _stateProvinceService
                        .GetStateProvinceByAbbreviation(order.Payer.Address?.AdminArea1, billingCountry?.Id);
                    var billingAddress = PrepareCustomerAddress(customer, new NopAddress
                    {
                        Email = order.Payer.EmailAddress ?? customer.Email,
                        FirstName = order.Payer.Name?.GivenName ?? firstName,
                        LastName = order.Payer.Name?.Surname ?? lastName,
                        Address1 = order.Payer.Address?.AddressLine1,
                        Address2 = order.Payer.Address?.AddressLine2,
                        City = order.Payer.Address?.AdminArea2,
                        ZipPostalCode = order.Payer.Address?.PostalCode,
                        StateProvinceId = billingState?.Id,
                        CountryId = billingCountry?.Id
                    });
                    if (billingAddress.Id != customer.BillingAddressId)
                        customer.BillingAddressId = billingAddress.Id;

                    if (_shoppingCartService.ShoppingCartRequiresShipping(cart) &&
                        _genericAttributeService.GetAttribute<NopShippingOption>(customer,
                            NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id) is NopShippingOption shippingOption &&
                        !IsPickup(shippingOption) &&
                        order.PurchaseUnits.FirstOrDefault()?.Shipping is Shipping shipping &&
                        shipping.Address is Address shippingAddress)
                    {
                        var shippingCountry = _countryService.GetCountryByTwoLetterIsoCode(shippingAddress.CountryCode);
                        var shippingState = _stateProvinceService
                            .GetStateProvinceByAbbreviation(shippingAddress.AdminArea1, shippingCountry?.Id);
                        var newShippingAddress = PrepareCustomerAddress(customer, new NopAddress
                        {
                            Email = order.Payer.EmailAddress ?? customer.Email,
                            Address1 = shippingAddress.AddressLine1,
                            Address2 = shippingAddress.AddressLine2,
                            City = shippingAddress.AdminArea2,
                            ZipPostalCode = shippingAddress.PostalCode,
                            StateProvinceId = shippingState?.Id,
                            CountryId = shippingCountry?.Id
                        });
                        if (newShippingAddress.Id != customer.ShippingAddressId)
                            customer.ShippingAddressId = newShippingAddress.Id;
                    }

                    _customerService.UpdateCustomer(customer);
                }

                return (order, false);
            });
        }

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="orderId">Order id</param>
        /// <param name="liabilityShift">Liability shift</param>
        /// <returns>The placed order; created order; error message if exists</returns>
        public ((NopOrder NopOrder, Order Order), string Error)
            PlaceOrder(PayPalCommerceSettings settings, string orderId, string liabilityShift)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                var paymentRequest = _actionContextAccessor.ActionContext.HttpContext.Session
                    .Get<ProcessPaymentRequest>(PayPalCommerceDefaults.PaymentRequestSessionKey);
                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                if (paymentRequest is null ||
                    !paymentRequest.CustomValues.TryGetValue(orderIdKey, out var orderIdValue) ||
                    !string.Equals(orderIdValue.ToString(), orderId, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new NopException("Failed to get the PayPal order ID");
                }

                //check the order status
                var order = _httpClient
                    .Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = orderId }, settings) as Order;
                if (order.Status?.ToUpper() != OrderStatusType.APPROVED.ToString() && order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
                {
                    if (order.Status?.ToUpper() == OrderStatusType.CREATED.ToString())
                    {
                        if (liabilityShift?.ToUpper() == LiabilityShiftType.NO.ToString())
                            throw new NopException($"3D Secure contingency is not resolved");

                        if (liabilityShift?.ToUpper() == LiabilityShiftType.UNKNOWN.ToString())
                            throw new NopException($"The authentication system isn't available, please retry later");
                    }
                    else
                        throw new NopException($"Order is in '{order.Status}' status");
                }

                var unit = order.PurchaseUnits.FirstOrDefault();
                if (unit is null || !string.Equals(unit.CustomId, paymentRequest.OrderGuid.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    throw new NopException("Failed to get PayPal order info");

                //totals must match
                var cartTotal = _orderTotalCalculationService.GetShoppingCartTotal(cart, usePaymentMethodAdditionalFee: false);
                var difference = Math.Abs(ConvertMoney(unit.Amount) - Math.Round(cartTotal ?? decimal.Zero, 2));
                if (difference > decimal.Zero)
                    throw new NopException($"Shopping cart total and approved order amount differ by {difference}");

                //prevent 2 orders being placed within an X seconds time frame
                if (_orderSettings.MinimumOrderPlacementInterval > 0)
                {
                    var lastOrder = (_orderService.SearchOrders(storeId: store.Id, customerId: customer.Id, pageSize: 1)).FirstOrDefault();
                    if (lastOrder != null && (DateTime.UtcNow - lastOrder.CreatedOnUtc).TotalSeconds < _orderSettings.MinimumOrderPlacementInterval)
                        throw new NopException(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));
                }

                paymentRequest.StoreId = store.Id;
                paymentRequest.CustomerId = customer.Id;
                paymentRequest.PaymentMethodSystemName = PayPalCommerceDefaults.SystemName;
                paymentRequest.CustomValues.Remove(_localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Placement"));

                //try to place an order
                var placeOrderResult = _orderProcessingService.PlaceOrder(paymentRequest);
                if (placeOrderResult?.Success != true || placeOrderResult.PlacedOrder is null)
                    throw new NopException(string.Join(',', placeOrderResult?.Errors ?? new List<string>()));

                //clear payment request
                _actionContextAccessor.ActionContext.HttpContext.Session
                    .Set<ProcessPaymentRequest>(PayPalCommerceDefaults.PaymentRequestSessionKey, null);

                return (placeOrderResult.PlacedOrder, order);
            });
        }

        /// <summary>
        /// Confirm the placed order
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="nopOrder">Placed order</param>
        /// <param name="order">Order</param>
        /// <returns>The confirmed order; error message if exists</returns>
        public (Order Order, string Error) ConfirmOrder(PayPalCommerceSettings settings, NopOrder nopOrder, Order order)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                //authorize or capture previously created order if not yet completed
                if (order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
                {
                    //update invoice id
                    var patch = new Patch<object>
                    {
                        Op = PatchOpType.REPLACE.ToString().ToLower(),
                        Path = "/purchase_units/@reference_id=='default'/invoice_id",
                        Value = nopOrder.CustomOrderNumber
                    };
                    var updateRequest = new UpdateOrderRequest<object>(new List<Patch<object>> { patch }) { OrderId = order.Id };
                    _httpClient.Request<UpdateOrderRequest<object>, EmptyResponse>(updateRequest, settings);

                    if (settings.PaymentType == Domain.PaymentType.Authorize)
                    {
                        order = _httpClient.Request<CreateAuthorizationRequest, CreateAuthorizationResponse>
                            (new CreateAuthorizationRequest { OrderId = order.Id }, settings);
                    }
                    else if (settings.PaymentType == Domain.PaymentType.Capture)
                    {
                        order = _httpClient.Request<Api.Orders.CreateCaptureRequest, Api.Orders.CreateCaptureResponse>
                            (new Api.Orders.CreateCaptureRequest { OrderId = order.Id }, settings);
                    }
                    else
                        order = null;
                }

                //check the authorization object or the capture object
                var purchaseUnit = order.PurchaseUnits.FirstOrDefault();
                var authorization = purchaseUnit.Payments?.Authorizations?.FirstOrDefault();
                if (authorization != null)
                {
                    if (authorization.Status?.ToUpper() == AuthorizationStatusType.DENIED.ToString())
                        throw new NopException("Cannot authorize funds for this authorized payment");

                    if (authorization.Status?.ToUpper() == AuthorizationStatusType.PENDING.ToString())
                    {
                        nopOrder.OrderNotes.Add(new OrderNote
                        {
                            Note = $"Authorization is in {authorization.Status} status due to {authorization.StatusDetails?.Reason}",
                            DisplayToCustomer = true,
                            CreatedOnUtc = DateTime.UtcNow
                        });
                        _orderService.UpdateOrder(nopOrder);

                        if (settings.PaymentType == Domain.PaymentType.Authorize && settings.ImmediatePaymentRequired)
                            throw new NopException($"Immediate payment required but authorization is {authorization.Status}");
                    }

                    if (authorization.Status?.ToUpper() == AuthorizationStatusType.CREATED.ToString())
                    {
                        if (_orderProcessingService.CanMarkOrderAsAuthorized(nopOrder))
                        {
                            nopOrder.AuthorizationTransactionId = authorization.Id;
                            nopOrder.AuthorizationTransactionResult = authorization.Status;
                            nopOrder.AuthorizationTransactionCode = authorization.ProcessorResponse?.ResponseCode;
                            _orderProcessingService.MarkAsAuthorized(nopOrder);
                        }
                    }
                }

                var capture = purchaseUnit.Payments?.Captures?.FirstOrDefault();
                if (capture != null)
                {
                    if (capture.Status?.ToUpper() == CaptureStatusType.DECLINED.ToString())
                        throw new NopException("The funds could not be captured");

                    if (capture.Status?.ToUpper() == CaptureStatusType.FAILED.ToString())
                        throw new NopException("There was an error while capturing payment");

                    if (capture.Status?.ToUpper() == CaptureStatusType.PENDING.ToString())
                    {
                        nopOrder.OrderNotes.Add(new OrderNote
                        {
                            Note = $"Capture is in {capture.Status} status due to {capture.StatusDetails?.Reason}",
                            DisplayToCustomer = true,
                            CreatedOnUtc = DateTime.UtcNow
                        });
                        _orderService.UpdateOrder(nopOrder);

                        if (settings.ImmediatePaymentRequired)
                            throw new NopException($"Immediate payment required but capture is {capture.Status}");
                    }

                    if (capture.Status?.ToUpper() == CaptureStatusType.COMPLETED.ToString())
                    {
                        if (_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                        {
                            nopOrder.CaptureTransactionId = capture.Id;
                            nopOrder.CaptureTransactionResult = capture.Status;
                            _orderProcessingService.MarkOrderAsPaid(nopOrder);
                        }
                    }
                }

                //try to get saved in vault payment token
                var vaultedPaymentMethod = order.PaymentSource?.Vault;
                if (vaultedPaymentMethod?.Status?.ToUpper() == VaultStatusType.VAULTED.ToString())
                {
                    _tokenService.Insert(new PayPalToken
                    {
                        ClientId = settings.ClientId,
                        CustomerId = nopOrder.CustomerId,
                        IsPrimaryMethod = false,
                        VaultId = vaultedPaymentMethod.Id,
                        VaultCustomerId = vaultedPaymentMethod.Customer?.Id,
                        TransactionId = order.Id,
                        Type = order.PaymentSource?.Card != null
                            ? nameof(order.PaymentSource.Card)
                            : (order.PaymentSource?.Venmo != null
                            ? nameof(order.PaymentSource.Venmo)
                            : (order.PaymentSource?.PayPal != null
                            ? nameof(order.PaymentSource.PayPal)
                            : null))
                    });
                }

                return order;
            });
        }

        #region Alternative payment methods

        /// <summary>
        /// Get Apple Pay transaction info
        /// </summary>
        /// <param name="placement">Button placement</param>
        /// <param name="withShipping">Whether to prepare shipping details</param>
        /// <returns>The Apple Pay transaction info; error message if exists</returns>
        public ((OrderMoney Amount, Contact BillingAddress, Contact ShippingAddress, Shipping Shipping, string StoreName), string Error)
            GetAppleTransactionInfo(ButtonPlacement placement, bool withShipping)
        {
            return HandleFunction(() =>
            {
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                var billingAddress = customer.BillingAddress;
                if (placement == ButtonPlacement.PaymentMethod && billingAddress is null)
                    throw new NopException("Customer billing address not set");

                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);
                var shippingOption = _genericAttributeService
                    .GetAttribute<NopShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);
                var pickupPoint = _genericAttributeService
                    .GetAttribute<PickupPoint>(customer, NopCustomerDefaults.SelectedPickupPointAttribute, store.Id);
                var pickupInStore = _shippingSettings.AllowPickupInStore && pickupPoint != null;
                var shippingAddress = pickupInStore ? new NopAddress
                {
                    Address1 = pickupPoint.Address,
                    City = pickupPoint.City,
                    County = pickupPoint.County,
                    CountryId = _countryService.GetCountryByTwoLetterIsoCode(pickupPoint.CountryCode)?.Id,
                    StateProvinceId = _stateProvinceService.GetStateProvinceByAbbreviation(pickupPoint.StateAbbreviation,
                        _countryService.GetCountryByTwoLetterIsoCode(pickupPoint.CountryCode)?.Id)?.Id,
                    ZipPostalCode = pickupPoint.ZipPostalCode,
                    CreatedOnUtc = DateTime.UtcNow
                } : _addressService.GetAddressById(customer.ShippingAddressId ?? 0);
                if (placement == ButtonPlacement.PaymentMethod && shippingIsRequired && shippingAddress is null)
                    throw new NopException("Customer shipping address not set");

                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    CurrencyCode = currencyCode,
                    BillingAddress = billingAddress,
                    Cart = cart.ToList(),
                    ShippingAddress = shippingAddress,
                    ShippingIsRequired = shippingIsRequired,
                    IsPickup = pickupInStore,
                    PickupPoint = pickupPoint,
                    ShippingOption = shippingOption
                };
                var payer = PrepareBillingDetails(details);
                var billingContact = new Contact
                {
                    Email = payer.EmailAddress,
                    FirstName = payer.Name.GivenName,
                    LastName = payer.Name.Surname,
                    AddressLines = new List<string> { payer.Address?.AddressLine1, payer.Address?.AddressLine2 },
                    City = payer.Address?.AdminArea2,
                    State = payer.Address?.AdminArea1,
                    Country = payer.Address?.CountryCode,
                    PostalCode = payer.Address?.PostalCode
                };

                var items = PrepareOrderItems(details);
                var orderAmount = PrepareOrderMoney(details, items);

                var firstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
                var lastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
                var shipping = PrepareShippingDetails(details, shippingOption?.Name, true);
                var shippingContact = new Contact
                {
                    FirstName = shippingAddress != null && !pickupInStore ? shippingAddress.FirstName : firstName,
                    LastName = shippingAddress != null && !pickupInStore ? shippingAddress.LastName : lastName,
                    AddressLines = new List<string> { shipping.Address?.AddressLine1, shipping.Address?.AddressLine2 },
                    City = shipping.Address?.AdminArea2,
                    State = shipping.Address?.AdminArea1,
                    Country = shipping.Address?.CountryCode,
                    PostalCode = shipping.Address?.PostalCode,
                    PickupInStore = pickupInStore
                };

                return (orderAmount, billingContact, shippingContact, shipping, store.Name);
            });
        }

        /// <summary>
        /// Update Apple Pay shipping details
        /// </summary>
        /// <param name="placement">Button placement</param>
        /// <param name="selectedAddress">Selected shipping address</param>
        /// <param name="selectedOption">Selected shipping option</param>
        /// <returns>The updated shipping details; error message if exists</returns>
        public (Shipping Shipping, string Error) UpdateAppleShipping(ButtonPlacement placement,
            (string City, string State, string Country, string PostalCode) selectedAddress, string selectedOption)
        {
            return HandleFunction(() =>
            {
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                //changing shipping address or option on the payment method page is not available
                if (placement == ButtonPlacement.PaymentMethod)
                    return null;

                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);
                if (!shippingIsRequired)
                    return null;

                //get option id
                var optionValues = selectedOption?.Split('|', StringSplitOptions.RemoveEmptyEntries)?.ToList() ?? new List<string>();
                var option = (optionValues.FirstOrDefault(), optionValues.LastOrDefault());

                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    Cart = cart.ToList(),
                    CurrencyCode = currencyCode,
                    ShippingIsRequired = shippingIsRequired
                };
                var shipping = PrepareUpdatedShipping(details, customer.Email, selectedAddress, option);

                return shipping;
            });
        }

        /// <summary>
        /// Get Google Pay transaction info
        /// </summary>
        /// <param name="placement">Button placement</param>
        /// <returns>The Google Pay transaction info; error message if exists</returns>
        public ((OrderMoney Amount, string Country, bool ShippingIsRequired), string Error)
            GetGoogleTransactionInfo(ButtonPlacement placement)
        {
            return HandleFunction(() =>
            {
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                var billingAddress = customer.BillingAddress;
                if (placement == ButtonPlacement.PaymentMethod && billingAddress is null)
                    throw new NopException("Customer billing address not set");

                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    Cart = cart.ToList(),
                    CurrencyCode = currencyCode
                };
                var items = PrepareOrderItems(details);
                var orderAmount = PrepareOrderMoney(details, items);

                var countryId = _genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.CountryIdAttribute);
                var country = _countryService.GetCountryById(billingAddress?.CountryId ?? countryId);
                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);

                return (orderAmount, country?.TwoLetterIsoCode ?? "US", shippingIsRequired);
            });
        }

        /// <summary>
        /// Update Google Pay shipping details
        /// </summary>
        /// <param name="placement">Button placement</param>
        /// <param name="selectedAddress">Selected shipping address</param>
        /// <param name="selectedOption">Selected shipping option</param>
        /// <returns>The updated shipping details; error message if exists</returns>
        public (Shipping Shipping, string Error) UpdateGoogleShipping(ButtonPlacement placement,
            (string City, string State, string Country, string PostalCode) selectedAddress, string selectedOption)
        {
            return HandleFunction(() =>
            {
                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new NopException("Shopping cart is empty");

                //changing shipping address or option on the payment method page is not available
                if (placement == ButtonPlacement.PaymentMethod)
                    return null;

                var shippingIsRequired = _shoppingCartService.ShoppingCartRequiresShipping(cart);
                if (!shippingIsRequired)
                    return null;

                //get option id
                var optionValues = selectedOption?.Split('|', StringSplitOptions.RemoveEmptyEntries)?.ToList() ?? new List<string>();
                var option = (optionValues.FirstOrDefault(), optionValues.LastOrDefault());

                var details = new CartDetails
                {
                    Placement = placement,
                    Customer = customer,
                    Store = store,
                    Cart = cart.ToList(),
                    CurrencyCode = currencyCode,
                    ShippingIsRequired = shippingIsRequired
                };
                var shipping = PrepareUpdatedShipping(details, customer.Email, selectedAddress, option);

                return shipping;
            });
        }

        #endregion

        #endregion

        #region Payments

        /// <summary>
        /// Capture an authorization
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="authorizationId">Authorization id</param>
        /// <returns>The capture details; error message if exists</returns>
        public (Capture Capture, string Error) CaptureAuthorization(PayPalCommerceSettings settings, string authorizationId)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                if (string.IsNullOrEmpty(authorizationId))
                    throw new NopException("Authorization ID not set");

                var request = new Api.Payments.CreateCaptureRequest { AuthorizationId = authorizationId };
                var capture = _httpClient.Request<Api.Payments.CreateCaptureRequest, Api.Payments.CreateCaptureResponse>(request, settings);

                if (capture.Status?.ToUpper() == CaptureStatusType.DECLINED.ToString())
                    throw new NopException("The funds could not be captured");

                if (capture.Status?.ToUpper() == CaptureStatusType.FAILED.ToString())
                    throw new NopException("There was an error while capturing payment");

                if (capture.Status?.ToUpper() == CaptureStatusType.PENDING.ToString())
                    throw new NopException($"Capture is in {capture.Status} status due to {capture.StatusDetails?.Reason}");

                return capture;
            });
        }

        /// <summary>
        /// Void an authorization
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="authorizationId">Authorization id</param>
        /// <returns>The void result; error message if exists</returns>
        public (bool Result, string Error) Void(PayPalCommerceSettings settings, string authorizationId)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                if (string.IsNullOrEmpty(authorizationId))
                    throw new NopException("Authorization ID not set");

                var request = new CreateVoidRequest { AuthorizationId = authorizationId };
                _httpClient.Request<CreateVoidRequest, EmptyResponse>(request, settings);

                return true;
            });
        }

        /// <summary>
        /// Refund a captured payment
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="nopOrder">Order</param>
        /// <param name="amount">Amount to refund; pass null to refund the full captured amount</param>
        /// <returns>The refund details; error message if exists</returns>
        public (Refund Refund, string Error) Refund(PayPalCommerceSettings settings, NopOrder nopOrder, decimal? amount = null)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
                if (string.IsNullOrEmpty(currencyCode))
                    throw new NopException("Primary store currency not set");

                if (string.IsNullOrEmpty(nopOrder.CaptureTransactionId))
                    throw new NopException("Capture ID not set");

                var request = new CreateRefundRequest
                {
                    CaptureId = nopOrder.CaptureTransactionId,
                    Amount = amount.HasValue ? PrepareMoney(amount.Value, currencyCode) : null
                };
                var refund = _httpClient.Request<CreateRefundRequest, CreateRefundResponse>(request, settings);

                if (refund.Status?.ToUpper() == RefundStatusType.CANCELLED.ToString())
                    throw new NopException("The refund was cancelled");

                if (refund.Status?.ToUpper() == RefundStatusType.FAILED.ToString())
                    throw new NopException("The refund could not be processed");

                if (refund.Status?.ToUpper() == RefundStatusType.PENDING.ToString())
                    throw new NopException($"Capture is in {refund.Status} status due to {refund.StatusDetails?.Reason}");

                //save id to avoid double refund
                var refundIds = _genericAttributeService
                    .GetAttribute<List<string>>(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName)
                    ?? new List<string>();
                if (!refundIds.Contains(refund.Id))
                    refundIds.Add(refund.Id);
                _genericAttributeService.SaveAttribute(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName, refundIds);

                return refund;
            });
        }

        #endregion

        #region Shipping

        /// <summary>
        /// Add package tracking number to an order
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="shipment">Shipment</param>
        /// <returns>The operation result; error message if exists</returns>
        public (bool Result, string Error) SetTracking(PayPalCommerceSettings settings, Shipment shipment)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var carrier = _genericAttributeService.GetAttribute<string>(shipment, PayPalCommerceDefaults.ShipmentCarrierAttribute);
                if (string.IsNullOrEmpty(carrier))
                    return false;

                var nopOrder = _orderService.GetOrderById(shipment?.OrderId ?? 0)
                    ?? throw new NopException("Order cannot be loaded");

                if (!string.Equals(nopOrder.PaymentMethodSystemName, PayPalCommerceDefaults.SystemName, StringComparison.InvariantCultureIgnoreCase))
                    return false;

                var customValues = _paymentService.DeserializeCustomValues(nopOrder);
                var orderIdKey = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.Order.Id");
                if (!customValues.TryGetValue(orderIdKey, out var orderIdValue))
                    throw new NopException("Failed to get PayPal order info");

                var order = _httpClient
                    .Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = orderIdValue.ToString() }, settings) as Order;
                if (order.Status?.ToUpper() != OrderStatusType.COMPLETED.ToString())
                    throw new NopException($"Unable to assign tracking information to orders in {order.Status} status");

                var unit = order.PurchaseUnits?.FirstOrDefault();
                if (unit?.Shipping is null)
                    throw new NopException("No shipping info found for PayPal order");

                var capture = unit.Payments?.Captures?.FirstOrDefault();
                if (capture is null ||
                    (capture.Status?.ToUpper() != CaptureStatusType.COMPLETED.ToString() &&
                    capture.Status?.ToUpper() != CaptureStatusType.PARTIALLY_REFUNDED.ToString() &&
                    capture.Status?.ToUpper() != CaptureStatusType.PENDING.ToString()))
                {
                    throw new NopException($"Unable to assign tracking information to orders with the payment in {order.Status} status");
                }

                var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
                var shipmentItems = shipment.ShipmentItems;
                var items = shipmentItems.Select(shipmentItem =>
                {
                    var orderItem = _orderService.GetOrderItemById(shipmentItem.OrderItemId);
                    var product = _productService.GetProductById(orderItem.ProductId);
                    var sku = _productService.FormatSku(product, orderItem.AttributesXml);
                    var seName = _urlRecordService.GetSeName(product);
                    var url = urlHelper.RouteUrl("Product", new { SeName = seName }, _webHelper.CurrentRequestProtocol);
                    var picture = _pictureService.GetProductPicture(product, orderItem.AttributesXml);
                    var imageUrl = _pictureService.GetPictureUrl(picture);

                    return new Item
                    {
                        Name = CommonHelper.EnsureMaximumLength(product.Name, 127),
                        Description = CommonHelper.EnsureMaximumLength(product.ShortDescription, 127),
                        Sku = CommonHelper.EnsureMaximumLength(sku, 127),
                        Quantity = shipmentItem.Quantity.ToString(),
                        Url = url,
                        ImageUrl = imageUrl
                    };
                }).ToList();

                var request = new CreateTrackingRequest
                {
                    OrderId = order.Id,
                    CaptureId = capture.Id,
                    TrackingNumber = shipment.TrackingNumber,
                    NotifyPayer = true,
                    Carrier = carrier,
                    Items = items
                };
                order = _httpClient.Request<CreateTrackingRequest, CreateTrackingResponse>(request, settings);

                return true;
            });
        }

        #endregion

        #region Webhooks

        /// <summary>
        /// Get webhook by the URL
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="webhookUrl">Webhook URL</param>
        /// <returns>The webhook; error message if exists</returns>
        public (Webhook Webhook, string Error) GetWebhook(PayPalCommerceSettings settings, string webhookUrl)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                var webhookList = _httpClient.Request<GetWebhooksRequest, GetWebhooksResponse>(new GetWebhooksRequest(), settings);
                var webhookByUrl = webhookList?.Webhooks
                    ?.FirstOrDefault(webhook => webhook.Url?.Equals(webhookUrl, StringComparison.InvariantCultureIgnoreCase) ?? false);

                return webhookByUrl;
            });
        }

        /// <summary>
        /// Create webhook that receive events for the subscribed event types
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="storeId">Store id</param>
        /// <returns>The webhook; error message if exists</returns>
        public (Webhook Webhook, string Error) CreateWebhook(PayPalCommerceSettings settings, int storeId)
        {
            return HandleFunction(() =>
            {
                if (!IsConfigured(settings))
                    throw new NopException("Plugin not configured");

                //prepare webhook URL
                var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
                var store = storeId > 0
                    ? _storeService.GetStoreById(storeId)
                    : _storeContext.CurrentStore;
                var webhookUrl = $"{store.Url.TrimEnd('/')}{urlHelper.RouteUrl(PayPalCommerceDefaults.Route.Webhook)}".ToLowerInvariant();

                //check whether the webhook already exists
                var (webhook, _) = GetWebhook(settings, webhookUrl);
                if (webhook != null)
                    return webhook;

                //or try to create a new one
                var request = new CreateWebhookRequest
                {
                    EventTypes = PayPalCommerceDefaults.WebhookEventNames.Select(name => new EventType { Name = name }).ToList(),
                    Url = webhookUrl
                };
                var result = _httpClient.Request<CreateWebhookRequest, CreateWebhookResponse>(request, settings);

                return result;
            });
        }

        /// <summary>
        /// Delete webhook
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        public void DeleteWebhook(PayPalCommerceSettings settings)
        {
            HandleFunction(() =>
            {
                if (!IsConnected(settings))
                    throw new NopException("Plugin not connected");

                var (webhook, _) = GetWebhook(settings, settings.WebhookUrl);
                if (webhook != null)
                    _httpClient.Request<DeleteWebhookRequest, EmptyResponse>(new DeleteWebhookRequest() { WebhookId = webhook.Id }, settings);

                return true;
            });
        }

        /// <summary>
        /// Handle webhook request
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="request">HTTP request</param>
        public void HandleWebhook(PayPalCommerceSettings settings, Microsoft.AspNetCore.Http.HttpRequest request)
        {
            HandleFunction(() =>
            {
                //ensure that plugin is configured and connected
                if (!IsConnected(settings))
                    throw new NopException("Webhook error", new NopException("Plugin not connected"));

                //get request content
                var webhookEvent = string.Empty;
                using (var streamReader = new StreamReader(request.Body))
                    webhookEvent = streamReader.ReadToEnd();

                var (webhook, _) = GetWebhook(settings, settings.WebhookUrl);
                if (webhook is null)
                    throw new NopException("Webhook error", new NopException($"No webhook configured for URL '{settings.WebhookUrl}'"));

                //define a local function to validate the webhook event and get its resource
                IWebhookResource getWebhookResource<TResource>() where TResource : class, IWebhookResource
                {
                    //verify webhook event data
                    var verifyRequest = new CreateWebhookSignatureRequest
                    {
                        AuthAlgo = request.Headers["PAYPAL-AUTH-ALGO"],
                        CertUrl = request.Headers["PAYPAL-CERT-URL"],
                        TransmissionId = request.Headers["PAYPAL-TRANSMISSION-ID"],
                        TransmissionSig = request.Headers["PAYPAL-TRANSMISSION-SIG"],
                        TransmissionTime = request.Headers["PAYPAL-TRANSMISSION-TIME"],
                        WebhookId = webhook.Id,
                        WebhookEvent = new JRaw(webhookEvent)
                    };
                    var result = _httpClient.Request<CreateWebhookSignatureRequest, CreateWebhookSignatureResponse>(verifyRequest, settings);

                    if (result?.VerificationStatus?.ToUpper() != WebhookSignatureVerificationStatusType.SUCCESS.ToString())
                        throw new NopException("Webhook error", new NopException($"Webhook signature verification {result?.VerificationStatus}"));

                    return JsonConvert.DeserializeObject<Event<TResource>>(webhookEvent)?.Resource;
                }

                //try to get webhook resource
                var webhookResourceType = JsonConvert.DeserializeObject<Event<WebhookResource>>(webhookEvent)?.ResourceType;
                IWebhookResource webhookResource = null;
                if (string.Equals(webhookResourceType, nameof(Authorization), StringComparison.InvariantCultureIgnoreCase))
                    webhookResource = getWebhookResource<Authorization>();
                else if (string.Equals(webhookResourceType, nameof(Capture), StringComparison.InvariantCultureIgnoreCase))
                    webhookResource = getWebhookResource<Capture>();
                else if (string.Equals(webhookResourceType, nameof(Refund), StringComparison.InvariantCultureIgnoreCase))
                    webhookResource = getWebhookResource<Refund>();
                else if (string.Equals(webhookResourceType?.Replace("checkout-", string.Empty), nameof(Order), StringComparison.InvariantCultureIgnoreCase))
                    webhookResource = getWebhookResource<Order>();
                else if (string.Equals(webhookResourceType?.Replace("_", string.Empty), nameof(PaymentToken), StringComparison.InvariantCultureIgnoreCase))
                    webhookResource = getWebhookResource<PaymentToken>();
                else
                    webhookResourceType = null;
                if (webhookResource is null)
                    throw new NopException("Webhook error", new NopException($"Unknown webhook resource type '{webhookResourceType}'"));

                var paymentToken = webhookResource as PaymentToken;
                if (paymentToken != null)
                {
                    //payment token actions
                    var eventType = JsonConvert.DeserializeObject<Event<WebhookResource>>(webhookEvent)?.EventType;
                    var paymentTokenCreated = string.Equals(eventType, "VAULT.PAYMENT-TOKEN.CREATED", StringComparison.InvariantCultureIgnoreCase);
                    var paymentTokenDeleted =
                        string.Equals(eventType, "VAULT.PAYMENT-TOKEN.DELETION-INITIATED", StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(eventType, "VAULT.PAYMENT-TOKEN.DELETED", StringComparison.InvariantCultureIgnoreCase);

                    if (paymentTokenCreated)
                    {
                        if (string.IsNullOrEmpty(paymentToken.Metadata?.OrderId))
                            throw new NopException("Webhook error", new NopException("No transaction associated with the payment token"));

                        var paymentTokenOrder = _httpClient
                            .Request<GetOrderRequest, GetOrderResponse>(new GetOrderRequest { OrderId = paymentToken.Metadata.OrderId }, settings);
                        paymentToken.CustomId = paymentTokenOrder.CustomId;
                    }

                    if (paymentTokenDeleted)
                    {
                        var tokens = _tokenService.GetAllTokens(settings.ClientId, vaultId: paymentToken.Id);
                        if (tokens.Any())
                            _tokenService.Delete(tokens);

                        return true;
                    }
                }

                if (!Guid.TryParse(webhookResource.CustomId, out var orderGuid) ||
                    !(_orderService.GetOrderByGuid(orderGuid) is NopOrder nopOrder))
                {
                    throw new NopException("Webhook error", new NopException($"Could not find an order '{orderGuid}'"));
                }

                if (paymentToken != null)
                {
                    //payment token actions (continuation)
                    _tokenService.Insert(new PayPalToken
                    {
                        ClientId = settings.ClientId,
                        CustomerId = nopOrder.CustomerId,
                        IsPrimaryMethod = false,
                        VaultId = paymentToken.Id,
                        VaultCustomerId = paymentToken.Customer?.Id,
                        TransactionId = paymentToken.Metadata.OrderId,
                        Type = paymentToken.PaymentSource?.Card != null
                            ? nameof(paymentToken.PaymentSource.Card)
                            : (paymentToken.PaymentSource?.Venmo != null
                            ? nameof(paymentToken.PaymentSource.Venmo)
                            : (paymentToken.PaymentSource?.PayPal != null
                            ? nameof(paymentToken.PaymentSource.PayPal)
                            : null))
                    });

                    return true;
                }

                nopOrder.OrderNotes.Add(new OrderNote
                {
                    Note = $"Webhook details: {Environment.NewLine}{JToken.Parse(webhookEvent).ToString(Formatting.Indented)}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.UpdateOrder(nopOrder);

                //authorization actions
                if (webhookResource is Authorization authorization && Enum.TryParse<AuthorizationStatusType>(authorization.Status, true, out var authorizationStatus))
                {
                    nopOrder.AuthorizationTransactionId = authorization.Id;
                    nopOrder.AuthorizationTransactionResult = authorization.Status;

                    switch (authorizationStatus)
                    {
                        case AuthorizationStatusType.PENDING:
                            nopOrder.PaymentStatus = PaymentStatus.Pending;
                            _orderProcessingService.CheckOrderStatus(nopOrder);

                            break;

                        case AuthorizationStatusType.VOIDED:
                            if (_orderProcessingService.CanVoidOffline(nopOrder))
                                _orderProcessingService.VoidOffline(nopOrder);

                            break;

                        case AuthorizationStatusType.CREATED:
                            if (!_orderProcessingService.CanMarkOrderAsAuthorized(nopOrder))
                                break;

                            if (ConvertMoney(authorization.Amount) >= Math.Round(nopOrder.OrderTotal, 2))
                                _orderProcessingService.MarkAsAuthorized(nopOrder);

                            break;

                        case AuthorizationStatusType.DENIED:
                            nopOrder.OrderNotes.Add(new OrderNote
                            {
                                Note = "Cannot authorize funds for this authorized payment",
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });
                            _orderService.UpdateOrder(nopOrder);

                            break;

                        case AuthorizationStatusType.CAPTURED:
                        case AuthorizationStatusType.PARTIALLY_CAPTURED:

                            //processed by the capture object

                            break;
                    }
                }

                //capture actions
                if (webhookResource is Capture capture && Enum.TryParse<CaptureStatusType>(capture.Status, true, out var captureStatus))
                {
                    nopOrder.CaptureTransactionId = capture.Id;
                    nopOrder.CaptureTransactionResult = capture.Status;

                    switch (captureStatus)
                    {
                        case CaptureStatusType.PENDING:
                            nopOrder.PaymentStatus = PaymentStatus.Pending;
                            _orderProcessingService.CheckOrderStatus(nopOrder);

                            break;

                        case CaptureStatusType.COMPLETED:
                            if (!_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                                break;

                            if (ConvertMoney(capture.Amount) >= Math.Round(nopOrder.OrderTotal, 2))
                                _orderProcessingService.MarkOrderAsPaid(nopOrder);

                            break;

                        case CaptureStatusType.DECLINED:
                        case CaptureStatusType.FAILED:
                            nopOrder.OrderNotes.Add(new OrderNote
                            {
                                Note = "The funds could not be captured",
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });
                            _orderService.UpdateOrder(nopOrder);

                            break;

                        case CaptureStatusType.PARTIALLY_REFUNDED:
                        case CaptureStatusType.REFUNDED:

                            //processed by the refund object

                            break;
                    }
                }

                //refund actions
                if (webhookResource is Refund refund && Enum.TryParse<RefundStatusType>(refund.Status, true, out var refundStatus))
                {
                    switch (refundStatus)
                    {
                        case RefundStatusType.CANCELLED:
                        case RefundStatusType.FAILED:
                            nopOrder.OrderNotes.Add(new OrderNote
                            {
                                Note = "The refund could not be processed or was cancelled",
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });
                            _orderService.UpdateOrder(nopOrder);

                            break;

                        case RefundStatusType.COMPLETED:
                            var refundIds = _genericAttributeService
                                .GetAttribute<List<string>>(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName)
                                ?? new List<string>();
                            if (refundIds.Contains(refund.Id))
                                break;

                            if (!decimal.TryParse(refund.Amount?.Value, out var refundedAmount))
                                break;

                            if (!_orderProcessingService.CanPartiallyRefundOffline(nopOrder, refundedAmount))
                                break;

                            _orderProcessingService.PartiallyRefundOffline(nopOrder, refundedAmount);

                            refundIds.Add(refund.Id);
                            _genericAttributeService.SaveAttribute(nopOrder, PayPalCommerceDefaults.RefundIdAttributeName, refundIds);

                            break;

                        case RefundStatusType.PENDING:

                            //waiting the subsequent notification

                            break;
                    }
                }

                //order actions
                if (webhookResource is Order order && Enum.TryParse<OrderStatusType>(order.Status, true, out var orderStatus))
                {
                    switch (orderStatus)
                    {
                        case OrderStatusType.COMPLETED:
                            var orderCapture = order.PurchaseUnits.FirstOrDefault().Payments?.Captures?.FirstOrDefault();
                            if (orderCapture is null)
                                break;

                            if (orderCapture.Status?.ToUpper() != CaptureStatusType.COMPLETED.ToString())
                                break;

                            if (!_orderProcessingService.CanMarkOrderAsPaid(nopOrder))
                                break;

                            nopOrder.CaptureTransactionId = orderCapture.Id;
                            nopOrder.CaptureTransactionResult = orderCapture.Status;

                            if (ConvertMoney(orderCapture.Amount) >= Math.Round(nopOrder.OrderTotal, 2))
                                _orderProcessingService.MarkOrderAsPaid(nopOrder);

                            break;
                    }
                }

                _orderService.UpdateOrder(nopOrder);

                return true;
            });
        }

        #endregion

        #region Onboarding

        /// <summary>
        /// Prepare URL to sign up a merchant
        /// </summary>
        /// <param name="merchantGuid">Merchant internal id</param>
        /// <returns>The URL to sign up; error message if exists</returns>
        public ((string SandboxUrl, string LiveUrl), string Error) PrepareSignUpUrl(string merchantGuid)
        {
            return HandleFunction(() =>
            {
                if (string.IsNullOrEmpty(merchantGuid))
                    throw new NopException("Merchant internal id is not set");

                var storeId = _storeContext.ActiveStoreScopeConfiguration;
                var store = storeId > 0
                    ? _storeService.GetStoreById(storeId)
                    : _storeContext.CurrentStore;
                var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
                var returnUrl = $"{store.Url.TrimEnd('/')}" +
                    $"{urlHelper.RouteUrl(PayPalCommerceDefaults.Route.OnboardingCallback, new { storeId = storeId })}";

                //sandbox URL
                var onboarding = new Onboarding
                {
                    Id = PayPalCommerceDefaults.Onboarding.Id.Sandbox,
                    Product = PayPalProductType.PPCP.ToString().ToLower(),
                    SecondaryProducts = string.Join(',',
                        PayPalProductType.PAYMENT_METHODS.ToString().ToLower(),
                        PayPalProductType.ADVANCED_VAULTING.ToString().ToLower()),
                    Capabilities = string.Join(',',
                        ProductCapabilityType.APPLE_PAY.ToString().ToLower(),
                        ProductCapabilityType.GOOGLE_PAY.ToString().ToLower(),
                        ProductCapabilityType.PAYPAL_WALLET_VAULTING_ADVANCED.ToString().ToLower()),
                    IntegrationType = IntegrationType.FO.ToString().ToUpper(),
                    Features = string.Join(',',
                        FeatureType.PAYMENT.ToString().ToLower(),
                        FeatureType.REFUND.ToString().ToLower(),
                        FeatureType.ACCESS_MERCHANT_INFORMATION.ToString().ToLower(),
                        FeatureType.BILLING_AGREEMENT.ToString().ToLower(),
                        FeatureType.VAULT.ToString().ToLower()),
                    ClientId = PayPalCommerceDefaults.Onboarding.ClientId.Sandbox,
                    ReturnToUrl = returnUrl.ToLowerInvariant(),
                    LogoUrl = PayPalCommerceDefaults.Onboarding.LogoUrl,
                    SellerNonce = GetSha256Hash(merchantGuid),
                    DisplayMode = "minibrowser"
                };
                var sandboxUrl = QueryHelpers.AddQueryString(PayPalCommerceDefaults.Onboarding.Url.Sandbox, ObjectToDictionary(onboarding));

                //live URL
                onboarding.Id = PayPalCommerceDefaults.Onboarding.Id.Live;
                onboarding.ClientId = PayPalCommerceDefaults.Onboarding.ClientId.Live;
                var liveUrl = QueryHelpers.AddQueryString(PayPalCommerceDefaults.Onboarding.Url.Live, ObjectToDictionary(onboarding));

                return (sandboxUrl, liveUrl);
            });
        }

        /// <summary>
        /// Sign up a merchant with the passed authentication parameters
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="authCode">Authentication parameters</param>
        /// <param name="sharedId">Authentication parameters</param>
        /// <returns>The REST API application credentials; error message if exists</returns>
        public (Credentials Credentials, string Error) SignUp(PayPalCommerceSettings settings, string authCode, string sharedId)
        {
            return HandleFunction(() =>
            {
                if (string.IsNullOrEmpty(settings.MerchantGuid))
                    throw new NopException("Merchant internal id is not set");

                if (string.IsNullOrEmpty(sharedId) || string.IsNullOrEmpty(authCode))
                    throw new NopException("Authentication parameters are empty");

                //try to get an access token
                var accessTokenRequest = new GetAccessTokenRequest
                {
                    GrantType = "authorization_code",
                    CodeVerifier = GetSha256Hash(settings.MerchantGuid.ToString()),
                    Code = authCode,
                    ClientId = sharedId,
                    Secret = string.Empty
                };
                var accessToken = _httpClient.Request<GetAccessTokenRequest, GetAccessTokenResponse>(accessTokenRequest, settings);

                //and change it to the credentials
                var credentialsRequest = new GetCredentialsRequest
                {
                    Id = settings.UseSandbox ? PayPalCommerceDefaults.Onboarding.Id.Sandbox : PayPalCommerceDefaults.Onboarding.Id.Live,
                    AccessToken = accessToken?.Token
                };
                var credentials = _httpClient.Request<GetCredentialsRequest, GetCredentialsResponse>(credentialsRequest, settings);

                return credentials;
            });
        }

        /// <summary>
        /// Get the merchant details
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>The merchant details; error message if exists</returns>
        public (Merchant Merchant, string Error) GetMerchant(PayPalCommerceSettings settings)
        {
            return HandleFunction(() =>
            {
                if (string.IsNullOrEmpty(settings.MerchantGuid))
                    throw new NopException("Merchant internal id is not set");

                //ensure that merchant id exists
                if (string.IsNullOrEmpty(settings.MerchantId))
                    throw new NopException("Onboarding process failed, please try again");

                var request = new GetMerchantRequest
                {
                    Id = settings.UseSandbox ? PayPalCommerceDefaults.Onboarding.Id.Sandbox : PayPalCommerceDefaults.Onboarding.Id.Live,
                    MerchantId = settings.MerchantId
                };

                var merchant = _httpClient.Request<GetMerchantRequest, GetMerchantResponse>(request, settings);

                //check capabilities statuses
                var ppcpStatus = merchant.Products
                    ?.FirstOrDefault(product => product.Name?.ToUpper() == PayPalProductType.PPCP_CUSTOM.ToString())
                    ?.VettingStatus?.ToUpper()
                    ?? ProductStatusType.PENDING.ToString();
                var advancedProcessingEnabled = ppcpStatus == ProductStatusType.SUBSCRIBED.ToString();
                (bool Active, string Status) getCapability(ProductCapabilityType type)
                {
                    var capabilityStatus = merchant.Capabilities
                        ?.FirstOrDefault(capability => capability.Name?.ToUpper() == type.ToString())
                        ?.Status?.ToUpper();
                    var active = capabilityStatus == ProductCapabilityStatusType.ACTIVE.ToString();
                    return (active && advancedProcessingEnabled, capabilityStatus ?? ProductCapabilityStatusType.PENDING.ToString());
                }

                merchant.AdvancedCards = getCapability(ProductCapabilityType.CUSTOM_CARD_PROCESSING);
                merchant.ApplePay = getCapability(ProductCapabilityType.APPLE_PAY);
                merchant.GooglePay = getCapability(ProductCapabilityType.GOOGLE_PAY);
                merchant.Vaulting = getCapability(ProductCapabilityType.PAYPAL_WALLET_VAULTING_ADVANCED);

                var review = ppcpStatus == ProductStatusType.IN_REVIEW.ToString();
                var needMoreData = ppcpStatus == ProductStatusType.NEED_MORE_DATA.ToString();
                var denied = ppcpStatus == ProductStatusType.DENIED.ToString();

                var cardsCapability = merchant.Capabilities
                    ?.FirstOrDefault(capability => capability.Name?.ToUpper() == ProductCapabilityType.CUSTOM_CARD_PROCESSING.ToString());
                var withdrawCapability = merchant.Capabilities
                    ?.FirstOrDefault(capability => capability.Name?.ToUpper() == ProductCapabilityType.WITHDRAW_MONEY.ToString());
                var sendMoneyCapability = merchant.Capabilities
                    ?.FirstOrDefault(capability => capability.Name?.ToUpper() == ProductCapabilityType.SEND_MONEY.ToString());
                var inLimit = advancedProcessingEnabled &&
                    merchant.AdvancedCards.Active &&
                    cardsCapability?.Limits?.FirstOrDefault()?.Type?.ToUpper() == "GENERAL";
                var belowLimit = inLimit && withdrawCapability?.Limits is null && sendMoneyCapability?.Limits is null;
                var overLimit = inLimit && withdrawCapability?.Limits != null && sendMoneyCapability?.Limits != null;

                merchant.AdvancedCardsDetails = (review, needMoreData, belowLimit, overLimit, denied);

                merchant.ConfiguratorSupported = PayPalCommerceDefaults.PayLaterSupportedCountries.Contains(merchant.Country);

                return merchant;
            }, false);
        }

        #endregion

        #region Payment tokens

        /// <summary>
        /// Get customer's payment tokens
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="withDetails">Whether to load additional details of payment tokens</param>
        /// <param name="deleteTokenId">Identifier of the token to delete</param>
        /// <param name="defaultTokenId">Identifier of the token to mark as default</param>
        /// <returns>The list of payment tokens; error message if exists</returns>
        public (List<PayPalToken> PaymentTokens, string Error)
            GetPaymentTokens(PayPalCommerceSettings settings, bool withDetails = false, int? deleteTokenId = null, int? defaultTokenId = null)
        {
            return HandleFunction(() =>
            {
                //only registered customers can save payment tokens
                var customer = _workContext.CurrentCustomer;
                if (customer.IsGuest())
                    return new List<PayPalToken>();

                //try to delete token
                if (deleteTokenId != null)
                {
                    var deleteToken = _tokenService.GetById(deleteTokenId.Value)
                        ?? throw new NopException("No payment token found with the specified id");

                    if (deleteToken.CustomerId != customer.Id)
                        throw new NopException("You cannot delete this token");

                    _tokenService.Delete(deleteToken);
                    _httpClient.Request<DeletePaymentTokenRequest, EmptyResponse>(new DeletePaymentTokenRequest { Id = deleteToken.VaultId }, settings);
                }

                //try to mark token as default
                if (defaultTokenId != null)
                {
                    var defaultToken = _tokenService.GetById(defaultTokenId.Value)
                        ?? throw new NopException("No payment token found with the specified id");

                    if (defaultToken.CustomerId != customer.Id)
                        throw new NopException("You cannot edit this token");

                    defaultToken.IsPrimaryMethod = true;
                    _tokenService.Update(defaultToken);

                    var tokensToUpdate = _tokenService.GetAllTokens(settings.ClientId, customer.Id)
                        .Where(token => token.Id != defaultToken.Id && token.IsPrimaryMethod);
                    foreach (var token in tokensToUpdate)
                    {
                        token.IsPrimaryMethod = false;
                        _tokenService.Update(token);
                    }
                }

                var tokens = _tokenService.GetAllTokens(settings.ClientId, customer.Id);
                if (!tokens.Any())
                    return new List<PayPalToken>();

                if (!withDetails)
                    return tokens.ToList();

                //load additional details
                return PreparePaymentTokens(settings, tokens);
            });
        }

        /// <summary>
        /// Delete all customer's payment tokens
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="customerId">Customer id</param>
        /// <returns>The delete result; error message if exists</returns>
        public (bool Result, string Error) DeletePaymentTokens(PayPalCommerceSettings settings, int customerId)
        {
            return HandleFunction(() =>
            {
                var tokens = _tokenService.GetAllTokens(settings.ClientId, customerId);
                _tokenService.Delete(tokens);
                foreach (var token in tokens)
                {
                    try
                    { _httpClient.Request<DeletePaymentTokenRequest, EmptyResponse>(new DeletePaymentTokenRequest { Id = token.VaultId }, settings); }
                    catch { }
                }

                return true;
            });
        }

        /// <summary>
        /// Get previously saved cards (payment tokens)
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <param name="placement">Button placement</param>
        /// <returns>The list of payment tokens; error message if exists</returns>
        public (List<PayPalToken> PaymentTokens, string Error) GetSavedCards(PayPalCommerceSettings settings, ButtonPlacement placement)
        {
            return HandleFunction(() =>
            {
                if (placement != ButtonPlacement.PaymentMethod || !settings.UseCardFields || !settings.UseVault)
                    return null;

                var customer = _workContext.CurrentCustomer;
                if (customer.IsGuest())
                    return null;

                //get cards only
                var tokens = _tokenService.GetAllTokens(settings.ClientId, customer.Id, type: nameof(PaymentSource.Card));

                return PreparePaymentTokens(settings, tokens);
            });
        }

        #endregion

        #endregion

        #region Nested classes

        /// <summary>
        /// Represents the shopping cart details
        /// </summary>
        private class CartDetails
        {
            #region Properties

            /// <summary>
            /// Gets or sets the button placement
            /// </summary>
            public ButtonPlacement Placement { get; set; }

            /// <summary>
            /// Gets or sets the current customer
            /// </summary>
            public Customer Customer { get; set; }

            /// <summary>
            /// Gets or sets the current store
            /// </summary>
            public Store Store { get; set; }

            /// <summary>
            /// Gets or sets the customer's shopping cart
            /// </summary>
            public List<ShoppingCartItem> Cart { get; set; } = new List<ShoppingCartItem>();

            /// <summary>
            /// Gets or sets the primary store currency code
            /// </summary>
            public string CurrencyCode { get; set; }

            /// <summary>
            /// Gets or sets the customer's billing address
            /// </summary>
            public NopAddress BillingAddress { get; set; }

            /// <summary>
            /// Gets or sets the customer's shipping address
            /// </summary>
            public NopAddress ShippingAddress { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the shipping is required for this cart
            /// </summary>
            public bool ShippingIsRequired { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the pick up in store option is selected
            /// </summary>
            public bool IsPickup { get; set; }

            /// <summary>
            /// Gets or sets the selected shipping option
            /// </summary>
            public NopShippingOption ShippingOption { get; set; }

            /// <summary>
            /// Gets or sets the selected pickup point
            /// </summary>
            public PickupPoint PickupPoint { get; set; }

            #endregion
        }

        #endregion
    }
}