using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Authentication;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Onboarding;

namespace Nop.Plugin.Payments.PayPalCommerce.Services
{
    /// <summary>
    /// Represents the HTTP client to request PayPal API
    /// </summary>
    public class PayPalCommerceHttpClient
    {
        #region Fields

        private static Dictionary<string, AccessToken> _accessTokens = new Dictionary<string, AccessToken>();

        #endregion

        #region Utilities

        /// <summary>
        /// Get access token
        /// </summary>
        /// <param name="settings">Plugin settings</param>
        /// <returns>The access token</returns>
        private string GetAccessToken(PayPalCommerceSettings settings)
        {
            if (!PayPalCommerceServiceManager.IsConfigured(settings))
                throw new NopException("Plugin is not configured");

            //no need to request a token if there is already a cached one and it has not expired (lifetime is about 9 hours)
            if (!_accessTokens.TryGetValue(settings.ClientId, out var accessToken) ||
                string.IsNullOrEmpty(accessToken?.Token) ||
                accessToken.IsExpired)
            {
                //get new access token
                accessToken = Request<GetAccessTokenRequest, GetAccessTokenResponse>(new GetAccessTokenRequest
                {
                    ClientId = settings.ClientId,
                    Secret = settings.SecretKey,
                    GrantType = "client_credentials"
                }, settings);
                _accessTokens[settings.ClientId] = accessToken;
            }

            return accessToken.Token;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Request remote service
        /// </summary>
        /// <typeparam name="TRequest">Request type</typeparam>
        /// <typeparam name="TResponse">Response type</typeparam>
        /// <param name="request">Request</param>
        /// <param name="settings">Plugin settings</param>
        /// <returns>The response details</returns>
        public TResponse Request<TRequest, TResponse>(TRequest request, PayPalCommerceSettings settings)
            where TRequest : IApiRequest where TResponse : IApiResponse
        {
            //URL depends on environment
            var baseUrl = settings.UseSandbox
                ? PayPalCommerceDefaults.ServiceUrl.Sandbox
                : PayPalCommerceDefaults.ServiceUrl.Live;

            //create web request
            var webRequest = (HttpWebRequest)WebRequest.Create(new Uri(new Uri(baseUrl), request.Path));
            webRequest.Method = request.Method;

            //set timeout
            try
            {
                var timeout = TimeSpan.FromSeconds(settings.RequestTimeout ?? PayPalCommerceDefaults.RequestTimeout);
                if (webRequest.Timeout != timeout.TotalMilliseconds)
                    webRequest.Timeout = (int)timeout.TotalMilliseconds;
            }
            catch { }

            //add authorization and some custom headers
            var authorization = string.Empty;
            if (request is GetCredentialsRequest credentialsRequest)
                authorization = $"Bearer {credentialsRequest.AccessToken}";
            if (request is GetAccessTokenRequest tokenRequest)
                authorization = $"Basic {Convert.ToBase64String(Encoding.Default.GetBytes($"{tokenRequest.ClientId}:{tokenRequest.Secret}"))}";
            if (request is IAuthorizedRequest)
                authorization = $"Bearer {GetAccessToken(settings)}";
            if (!string.IsNullOrEmpty(authorization))
                webRequest.Headers.Add(HttpRequestHeader.Authorization, authorization);
            webRequest.UserAgent = PayPalCommerceDefaults.UserAgent;
            webRequest.Accept = MimeTypes.ApplicationJson;
            webRequest.Headers.Add(PayPalCommerceDefaults.PartnerHeader.Name, PayPalCommerceDefaults.PartnerHeader.Value);
            webRequest.Headers.Add("PayPal-Request-Id", Guid.NewGuid().ToString());
            webRequest.Headers.Add("Prefer", "return=representation");

            if (request.Method != WebRequestMethods.Http.Get)
            {
                //prepare request body, content is always JSON except for access token requests
                var requestString = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                var requestContent = request is GetAccessTokenRequest accessTokenRequest
                    ? new FormUrlEncodedContent(PayPalCommerceServiceManager.ObjectToDictionary(accessTokenRequest))
                    : (ByteArrayContent)new StringContent(requestString, Encoding.Default, MimeTypes.ApplicationJson);

                var postData = Encoding.UTF8.GetBytes(requestContent.ReadAsStringAsync().Result);

                webRequest.ContentType = request is GetAccessTokenRequest ? MimeTypes.ApplicationXWwwFormUrlencoded : MimeTypes.ApplicationJson;
                webRequest.ContentLength = postData.Length;

                using (var stream = webRequest.GetRequestStream())
                {
                    stream.Write(postData, 0, postData.Length);
                }
            }

            try
            {
                //execute the request and get a result
                var responseString = string.Empty;
                var httpResponse = (HttpWebResponse)webRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    responseString = streamReader.ReadToEnd();

                //successful request processing
                if (((int)httpResponse.StatusCode >= 200) && ((int)httpResponse.StatusCode <= 299))
                {
                    if (typeof(TResponse) == typeof(EmptyResponse))
                        return default;

                    return JsonConvert.DeserializeObject<TResponse>(responseString ?? string.Empty);
                }

                //failed request processing
                var error = $"Failed request ({httpResponse.StatusCode})";
                var identityErrorResponse = JsonConvert.DeserializeObject<IdentityErrorResponse>(responseString ?? string.Empty);
                if (!string.IsNullOrEmpty(identityErrorResponse?.Error))
                {
                    var description = !string.IsNullOrEmpty(identityErrorResponse.ErrorDescription)
                        ? identityErrorResponse.ErrorDescription
                        : identityErrorResponse.Error;
                    error += $": {description}";
                }

                var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseString ?? string.Empty);
                if (!string.IsNullOrEmpty(errorResponse?.Name))
                {
                    error += $": {(!string.IsNullOrEmpty(errorResponse.Message) ? errorResponse.Message : errorResponse.Name)}";
                    error += $"{Environment.NewLine}{JsonConvert.SerializeObject(errorResponse, Formatting.Indented)}";
                }

                throw new NopException("Failed request", new NopException(error));
            }
            catch (Exception exception)
            {
                //try to get error response
                if (exception is WebException webException)
                {
                    var httpResponse = (HttpWebResponse)webException.Response;
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var responseString = streamReader.ReadToEnd();

                        //failed request processing
                        var error = $"Failed request ({httpResponse.StatusCode})";
                        var identityErrorResponse = JsonConvert.DeserializeObject<IdentityErrorResponse>(responseString ?? string.Empty);
                        if (!string.IsNullOrEmpty(identityErrorResponse?.Error))
                        {
                            var description = !string.IsNullOrEmpty(identityErrorResponse.ErrorDescription)
                                ? identityErrorResponse.ErrorDescription
                                : identityErrorResponse.Error;
                            error += $": {description}";
                        }

                        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseString ?? string.Empty);
                        if (!string.IsNullOrEmpty(errorResponse?.Name))
                        {
                            error += $": {(!string.IsNullOrEmpty(errorResponse.Message) ? errorResponse.Message : errorResponse.Name)}";
                            error += $"{Environment.NewLine}{JsonConvert.SerializeObject(errorResponse, Formatting.Indented)}";
                        }

                        throw new NopException("Failed request", new NopException(error));
                    }
                }

                throw new NopException("Failed request");
            }
        }

        #endregion
    }
}