using FluentMigrator;
using Nop.Data.Migrations;
using Nop.Plugin.Payments.PayPalCommerce.Domain;

namespace Nop.Plugin.Payments.PayPalCommerce.Data
{
    [SkipMigrationOnUpdate]
    [NopMigration("2024-06-06 00:00:00", "Payments.PayPalCommerce base schema")]
    public class SchemaMigration : AutoReversingMigration
    {
        #region Fields

        protected IMigrationManager _migrationManager;

        #endregion

        #region Ctor

        public SchemaMigration(IMigrationManager migrationManager)
        {
            _migrationManager = migrationManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            _migrationManager.BuildTable<PayPalToken>(Create);
        }

        #endregion
    }
}