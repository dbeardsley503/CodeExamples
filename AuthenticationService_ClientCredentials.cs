using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AuthenticationService
{
    public interface IAuthenticationService
    {
        /// <summary>
        /// Retrieves an access token using OAuth client credentials flow
        /// </summary>
        /// <returns>An AuthenticationToken containing the access token and related information</returns>
        /// <exception cref="AuthenticationException">Thrown when authentication fails</exception>
        Task<AuthenticationToken> GetAccessTokenAsync();
    }


    public class AuthenticationToken
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        public DateTime ExpiresAt { get; set; }
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _tokenEndpoint;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _scope;

        public AuthenticationService(
            string tokenEndpoint,
            string clientId,
            string clientSecret,
            string scope = "",
            HttpClient httpClient = null)
        {
            _tokenEndpoint = tokenEndpoint ?? throw new ArgumentNullException(nameof(tokenEndpoint));
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            _scope = scope;
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<AuthenticationToken> GetAccessTokenAsync()
        {
            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["scope"] = _scope
            });

            try
            {
                var response = await _httpClient.PostAsync(_tokenEndpoint, requestContent);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<AuthenticationToken>(content);

                // Calculate token expiration time
                token.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);

                return token;
            }
            catch (HttpRequestException ex)
            {
                throw new AuthenticationException("Failed to obtain access token", ex);
            }
            catch (JsonException ex)
            {
                throw new AuthenticationException("Failed to parse authentication response", ex);
            }
        }
    }

    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
