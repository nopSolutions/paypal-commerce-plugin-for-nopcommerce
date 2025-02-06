using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Payments.PayPalCommerce.Components.Admin
{
    /// <summary>
    /// Represents the view component to display PayPal on the payment methods page in the admin area
    /// </summary>
    [ViewComponent(Name = PayPalCommerceDefaults.PAYMENT_METHOD_VIEW_COMPONENT_NAME)]
    public class PaymentMethodViewComponent : NopViewComponent
    {
        #region Fields

        private readonly PayPalCommerceSettings _settings;
        private readonly PayPalCommerceServiceManager _serviceManager;

        #endregion

        #region Ctor

        public PaymentMethodViewComponent(PayPalCommerceSettings settings,
            PayPalCommerceServiceManager serviceManager)
        {
            _settings = settings;
            _serviceManager = serviceManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>The view component result</returns>
        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            if (!widgetZone.Equals(AdminWidgetZones.PaymentMethodListTop))
                return Content(string.Empty);

            var (active, _) = _serviceManager.IsActive(_settings);

            return View("~/Plugins/Payments.PayPalCommerce/Views/Admin/_PaymentMethod.cshtml", active);
        }

        #endregion
    }
}