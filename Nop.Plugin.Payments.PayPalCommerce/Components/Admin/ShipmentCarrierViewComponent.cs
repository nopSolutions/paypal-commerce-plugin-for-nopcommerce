using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPalCommerce.Models.Admin;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Services.Common;
using Nop.Services.Shipping;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Payments.PayPalCommerce.Components.Admin
{
    /// <summary>
    /// Represents the view component to render an additional input on the shipment details page in the admin area
    /// </summary>
    [ViewComponent(Name = PayPalCommerceDefaults.SHIPMENT_CARRIER_VIEW_COMPONENT_NAME)]
    public class ShipmentCarrierViewComponent : NopViewComponent
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IShipmentService _shipmentService;
        private readonly PayPalCommerceServiceManager _serviceManager;
        private readonly PayPalCommerceSettings _settings;

        #endregion

        #region Ctor

        public ShipmentCarrierViewComponent(IGenericAttributeService genericAttributeService,
            IShipmentService shipmentService,
            PayPalCommerceServiceManager serviceManager,
            PayPalCommerceSettings settings)
        {
            _genericAttributeService = genericAttributeService;
            _shipmentService = shipmentService;
            _serviceManager = serviceManager;
            _settings = settings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invoke the widget view component
        /// </summary>
        /// <param name="widgetZone">Widget zone</param>
        /// <param name="additionalData">Additional parameters</param>
        /// <returns>The view component result</returns>
        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            var (active, _) = _serviceManager.IsActive(_settings);
            if (!active)
                return Content(string.Empty);

            if (!_settings.UseShipmentTracking)
                return Content(string.Empty);

            if (!widgetZone.Equals(AdminWidgetZones.OrderShipmentDetailsButtons) && !widgetZone.Equals(AdminWidgetZones.OrderShipmentAddButtons))
                return Content(string.Empty);

            if (!(additionalData is ShipmentModel shipmentModel))
                return Content(string.Empty);

            var shipment = _shipmentService.GetShipmentById(shipmentModel.Id);
            var model = new ShipmentCarrierModel
            {
                TrackingNumber = shipmentModel.TrackingNumber,
                PayPalCommerceShipmentCarrier = shipment != null
                    ? _genericAttributeService.GetAttribute<string>(shipment, PayPalCommerceDefaults.ShipmentCarrierAttribute)
                    : null
            };

            return View("~/Plugins/Payments.PayPalCommerce/Views/Admin/_ShipmentCarrier.cshtml", model);
        }

        #endregion
    }
}