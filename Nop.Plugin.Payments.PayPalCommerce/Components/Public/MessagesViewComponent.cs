using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Factories;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPalCommerce.Components.Public
{
    /// <summary>
    /// Represents the view component to display Pay Later messages in the public store
    /// </summary>
    [ViewComponent(Name = PayPalCommerceDefaults.MESSAGES_VIEW_COMPONENT_NAME)]
    public class MessagesViewComponent : NopViewComponent
    {
        #region Fields

        private readonly PayPalCommerceModelFactory _modelFactory;
        private readonly PayPalCommerceServiceManager _serviceManager;
        private readonly PayPalCommerceSettings _settings;

        #endregion

        #region Ctor

        public MessagesViewComponent(PayPalCommerceModelFactory modelFactory,
            PayPalCommerceServiceManager serviceManager,
            PayPalCommerceSettings settings)
        {
            _modelFactory = modelFactory;
            _serviceManager = serviceManager;
            _settings = settings;
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
            var (active, _) = _serviceManager.IsActive(_settings);
            if (!active)
                return Content(string.Empty);

            if (!widgetZone.Equals(PayPalCommerceDefaults.OrderSummaryContentAfter))
                return Content(string.Empty);

            if (!_settings.UseSandbox && !_settings.ConfiguratorSupported)
                return Content(string.Empty);

            //get messages placement
            var routeNames = RouteData.Routers.OfType<INamedRouter>();
            var isCartPage = routeNames.Any(routeName => routeName.Name == PayPalCommerceDefaults.Route.ShoppingCart);
            var isPaymentMethodPage = routeNames.Any(routeName => routeName.Name == PayPalCommerceDefaults.Route.PaymentInfo);
            var isCheckoutPage = (RouteData.Values?.TryGetValue("controller", out var controller) ?? false) &&
                string.Equals(controller.ToString(), "Checkout", StringComparison.InvariantCultureIgnoreCase);
            if (!isCartPage && !isPaymentMethodPage && !isCheckoutPage)
                return Content(string.Empty);

            //load script only on checkout pages (excluding payment method page) to avoid double loading
            var loadScript = !isCartPage && !isPaymentMethodPage;
            var placement = isCartPage ? ButtonPlacement.Cart : ButtonPlacement.PaymentMethod;
            var model = _modelFactory.PrepareMessagesModel(placement, loadScript);

            return View("~/Plugins/Payments.PayPalCommerce/Views/Public/_Messages.cshtml", model);
        }

        #endregion
    }
}