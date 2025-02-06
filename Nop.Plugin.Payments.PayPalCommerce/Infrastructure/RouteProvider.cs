using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Localization;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPalCommerce.Infrastructure
{
    /// <summary>
    /// Represents the plugin route provider
    /// </summary>
    public class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute(name: PayPalCommerceDefaults.Route.Configuration,
                template: "Admin/PayPalCommerce/Configure",
                defaults: new { controller = "PayPalCommerce", action = "Configure", area = AreaNames.Admin });

            routeBuilder.MapRoute(name: PayPalCommerceDefaults.Route.OnboardingCallback,
                template: "Admin/PayPalCommerce/Onboarding/{storeId:int}",
                defaults: new { controller = "PayPalCommerce", action = "OnboardingCallback", area = AreaNames.Admin });

            routeBuilder.MapRoute(name: PayPalCommerceDefaults.Route.Webhook,
                template: "Plugins/PayPalCommerce/Webhook",
                defaults: new { controller = "PayPalCommerceWebhook", action = "WebhookHandler" });

            routeBuilder.MapLocalizedRoute(name: PayPalCommerceDefaults.Route.PaymentInfo,
                template: $"paypal/payment-info",
                defaults: new { controller = "PayPalCommercePublic", action = "PluginPaymentInfo" });

            routeBuilder.MapLocalizedRoute(name: PayPalCommerceDefaults.Route.ConfirmOrder,
                template: $"paypal/confirm-order",
                defaults: new { controller = "PayPalCommercePublic", action = "ConfirmOrder" });

            routeBuilder.MapLocalizedRoute(name: PayPalCommerceDefaults.Route.PaymentTokens,
                template: $"customer/paypal-payment-methods",
                defaults: new { controller = "PayPalCommercePublic", action = "PaymentTokens" });

        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}