using Autofac;
using Autofac.Core;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Data;
using Nop.Plugin.Payments.PayPalCommerce.Data;
using Nop.Plugin.Payments.PayPalCommerce.Domain;
using Nop.Plugin.Payments.PayPalCommerce.Factories;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Web.Framework.Infrastructure.Extensions;

namespace Nop.Plugin.Payments.PayPalCommerce.Infrastructure
{
    /// <summary>
    /// Represents a plugin dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<PayPalCommerceHttpClient>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<PayPalCommerceModelFactory>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<PayPalCommerceServiceManager>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<PayPalTokenService>().AsSelf().InstancePerLifetimeScope();

            builder.RegisterPluginDataContext<PayPalTokenObjectContext>("nop_object_context_paypal_token");
            builder.RegisterType<EfRepository<PayPalToken>>().As<IRepository<PayPalToken>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>("nop_object_context_paypal_token"))
                .InstancePerLifetimeScope();
        }

        /// <summary>
        /// Order of this dependency registrar implementation
        /// </summary>
        public int Order => 1;
    }
}