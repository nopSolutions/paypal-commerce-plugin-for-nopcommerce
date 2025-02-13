using System.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Shipping;
using Nop.Core.Events;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Shipping;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Mvc.Models;
using Nop.Web.Models.Customer;

namespace Nop.Plugin.Payments.PayPalCommerce.Services
{
    /// <summary>
    /// Represents the plugin event consumer
    /// </summary>
    public class EventConsumer :
        IConsumer<ModelPrepared<BaseNopModel>>,
        IConsumer<ModelReceived<BaseNopModel>>,
        IConsumer<EntityInserted<Shipment>>,
        IConsumer<EntityUpdated<Shipment>>
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IShipmentService _shipmentService;
        private readonly PayPalCommerceServiceManager _serviceManager;
        private readonly PayPalCommerceSettings _settings;

        #endregion

        #region Ctor

        public EventConsumer(IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IShipmentService shipmentService,
            PayPalCommerceServiceManager serviceManager,
            PayPalCommerceSettings settings)
        {
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _shipmentService = shipmentService;
            _serviceManager = serviceManager;
            _settings = settings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle model prepared event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(ModelPrepared<BaseNopModel> eventMessage)
        {
            if (!(eventMessage.Model is CustomerNavigationModel navigationModel))
                return;

            var (active, _) = _serviceManager.IsActive(_settings);
            if (!active)
                return;

            var (tokens, _) = _serviceManager.GetPaymentTokens(_settings);
            if (!_settings.UseVault && !tokens.Any())
                return;

            //add a new menu item in the customer navigation
            var orderItem = navigationModel.CustomerNavigationItems.FirstOrDefault(item => item.Tab == CustomerNavigationEnum.Orders);
            var position = navigationModel.CustomerNavigationItems.IndexOf(orderItem) + 1;
            navigationModel.CustomerNavigationItems.Insert(position, new CustomerNavigationItemModel
            {
                RouteName = PayPalCommerceDefaults.Route.PaymentTokens,
                ItemClass = "paypal-payment-tokens",
                Title = _localizationService.GetResource("Plugins.Payments.PayPalCommerce.PaymentTokens")
            });
        }

        /// <summary>
        /// Handle model received event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(ModelReceived<BaseNopModel> eventMessage)
        {
            if (!(eventMessage.Model is ShipmentModel shipmentModel))
                return;

            if (!PayPalCommerceServiceManager.IsConnected(_settings))
                return;

            if (!_settings.UseShipmentTracking)
                return;

            //save specified shipment carrier
            if (_httpContextAccessor.HttpContext.Request.Form.TryGetValue(PayPalCommerceDefaults.ShipmentCarrierAttribute, out var carrierValue))
            {
                var carrier = carrierValue.ToString();
                var shipment = _shipmentService.GetShipmentById(shipmentModel.Id);
                if (shipment != null)
                    _genericAttributeService.SaveAttribute(shipment, PayPalCommerceDefaults.ShipmentCarrierAttribute, carrier);
                else if (!string.IsNullOrEmpty(carrier))
                {
                    //when we add a new shipping, it's not in the db yet and we cannot save a generic attribute to it,
                    //so we temporarily store the data in the context, we'll move it to the attribute later during the same request
                    if (!_httpContextAccessor.HttpContext.Items.ContainsKey(PayPalCommerceDefaults.ShipmentCarrierAttribute))
                        _httpContextAccessor.HttpContext.Items.Add(PayPalCommerceDefaults.ShipmentCarrierAttribute, carrier);
                }
            }
        }

        /// <summary>
        /// Handle shipment inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInserted<Shipment> eventMessage)
        {
            if (!PayPalCommerceServiceManager.IsConnected(_settings))
                return;

            if (!_settings.UseShipmentTracking || eventMessage.Entity is null)
                return;

            //move the saved data from context to the generic attribute
            if (_httpContextAccessor.HttpContext.Items.TryGetValue(PayPalCommerceDefaults.ShipmentCarrierAttribute, out var carrier))
            {
                _genericAttributeService
                    .SaveAttribute(eventMessage.Entity, PayPalCommerceDefaults.ShipmentCarrierAttribute, carrier.ToString());
            }

            if (!string.IsNullOrEmpty(eventMessage.Entity.TrackingNumber))
                _serviceManager.SetTracking(_settings, eventMessage.Entity);
        }

        /// <summary>
        /// Handle shipment updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdated<Shipment> eventMessage)
        {
            if (!PayPalCommerceServiceManager.IsConnected(_settings))
                return;

            if (!_settings.UseShipmentTracking || eventMessage.Entity is null)
                return;

            if (!string.IsNullOrEmpty(eventMessage.Entity.TrackingNumber))
                _serviceManager.SetTracking(_settings, eventMessage.Entity);
        }

        #endregion
    }
}