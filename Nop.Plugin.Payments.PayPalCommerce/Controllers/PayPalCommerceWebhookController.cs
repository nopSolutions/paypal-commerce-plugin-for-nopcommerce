using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPalCommerce.Services;

namespace Nop.Plugin.Payments.PayPalCommerce.Controllers
{
    public class PayPalCommerceWebhookController : Controller
    {
        #region Fields

        private readonly PayPalCommerceServiceManager _serviceManager;
        private readonly PayPalCommerceSettings _settings;

        #endregion

        #region Ctor

        public PayPalCommerceWebhookController(PayPalCommerceServiceManager serviceManager,
            PayPalCommerceSettings settings)
        {
            _serviceManager = serviceManager;
            _settings = settings;
        }

        #endregion

        #region Methods

        [HttpPost]
        public IActionResult WebhookHandler()
        {
            _serviceManager.HandleWebhook(_settings, Request);
            return Ok();
        }

        #endregion
    }
}