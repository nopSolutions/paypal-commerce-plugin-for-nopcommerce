using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Payments.PayPalCommerce.Components.Public
{
    /// <summary>
    /// Represents the view component to display PayPal logo in the public store
    /// </summary>
    [ViewComponent(Name = PayPalCommerceDefaults.LOGO_VIEW_COMPONENT_NAME)]
    public class LogoViewComponent : NopViewComponent
    {
        #region Fields

        private readonly PayPalCommerceSettings _settings;
        private readonly PayPalCommerceServiceManager _serviceManager;

        #endregion

        #region Ctor

        public LogoViewComponent(PayPalCommerceSettings settings,
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
            var (active, _) = _serviceManager.IsActive(_settings);
            if (!active)
                return Content(string.Empty);

            var script = widgetZone.Equals(PublicWidgetZones.HeaderLinksBefore) && _settings.DisplayLogoInHeaderLinks
                ? _settings.LogoInHeaderLinks
                : widgetZone.Equals(PublicWidgetZones.Footer) && _settings.DisplayLogoInFooter
                ? _settings.LogoInFooter
                : null;

            return new HtmlContentViewComponentResult(new HtmlString(script ?? string.Empty));
        }

        #endregion
    }
}