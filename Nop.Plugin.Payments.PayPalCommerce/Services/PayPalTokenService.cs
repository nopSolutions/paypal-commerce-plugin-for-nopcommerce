using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Data;
using Nop.Plugin.Payments.PayPalCommerce.Domain;

namespace Nop.Plugin.Payments.PayPalCommerce.Services
{
    /// <summary>
    /// Represents the payment token service
    /// </summary>
    public class PayPalTokenService
    {
        #region Fields

        private readonly IRepository<PayPalToken> _tokenRepository;

        #endregion

        #region Ctor

        public PayPalTokenService(IRepository<PayPalToken> tokenRepository)
        {
            _tokenRepository = tokenRepository;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get all payment tokens
        /// </summary>
        /// <param name="clientId">Client identifier</param>
        /// <param name="customerId">Customer identifier; pass 0 to load all tokens</param>
        /// <param name="vaultId">Vault identifier; pass null to load all tokens</param>
        /// <param name="vaultCustomerId">Vault customer identifier; pass null to load all tokens</param>
        /// <param name="type">Token type; pass null to load all tokens</param>
        /// <returns>The list of payment tokens</returns>
        public IList<PayPalToken> GetAllTokens(string clientId, int customerId = 0,
            string vaultId = null, string vaultCustomerId = null, string type = null)
        {
            var query = _tokenRepository.Table.Where(token => token.ClientId == clientId);

            if (customerId > 0)
                query = query.Where(token => token.CustomerId == customerId);

            if (!string.IsNullOrEmpty(vaultId))
                query = query.Where(token => token.VaultId == vaultId);

            if (!string.IsNullOrEmpty(vaultCustomerId))
                query = query.Where(token => token.VaultCustomerId == vaultCustomerId);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(token => token.Type == type);

            return query.ToList();
        }

        /// <summary>
        /// Get a payment token by identifier
        /// </summary>
        /// <param name="id">Token identifier</param>
        /// <returns>The payment token</returns>
        public PayPalToken GetById(int id)
        {
            return _tokenRepository.GetById(id);
        }

        /// <summary>
        /// Insert the payment token
        /// </summary>
        /// <param name="token">Payment token</param>
        public void Insert(PayPalToken token)
        {
            //whether a token with such parameters already exists
            var tokens = GetAllTokens(token.ClientId, token.CustomerId);
            var existingToken = tokens
                .FirstOrDefault(existing => existing.VaultId == token.VaultId && existing.VaultCustomerId == token.VaultCustomerId);
            if (existingToken != null)
            {
                //then just update transaction id
                if (!string.Equals(existingToken.TransactionId, token.TransactionId, StringComparison.InvariantCultureIgnoreCase))
                {
                    existingToken.TransactionId = token.TransactionId;
                    Update(existingToken);
                }

                return;
            }

            if (!tokens.Any())
                token.IsPrimaryMethod = true;

            //or insert a new one
            _tokenRepository.Insert(token);
        }

        /// <summary>
        /// Update the payment token
        /// </summary>
        /// <param name="token">Payment token</param>
        public void Update(PayPalToken token)
        {
            _tokenRepository.Update(token);
        }

        /// <summary>
        /// Delete the payment token
        /// </summary>
        /// <param name="token">Payment token</param>
        public void Delete(PayPalToken token)
        {
            _tokenRepository.Delete(token);

            //mark one of the remaining tokens as default
            var tokens = GetAllTokens(token.ClientId, token.CustomerId);
            if (tokens.Any(existing => existing.IsPrimaryMethod))
                return;

            var existingToken = tokens.FirstOrDefault();
            if (existingToken is null)
                return;

            existingToken.IsPrimaryMethod = true;
            Update(existingToken);
        }

        /// <summary>
        /// Delete payment tokens
        /// </summary>
        /// <param name="token">Payment tokens</param>
        public void Delete(IList<PayPalToken> tokens)
        {
            _tokenRepository.Delete(tokens);
        }

        #endregion
    }
}