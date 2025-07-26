using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JagexAccountImporter.oauth;

public static class TokenExchange
    {
        private const string SessionUri = "https://auth.jagex.com/game-session/v1/sessions";
        private const string AccountUri = "https://auth.jagex.com/game-session/v1/accounts";

        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task<Dictionary<string, JsonElement>?> RequestJxSessionInformationAsync(string jwt)
        {
            try
            {
                var requestBody = new { idToken = jwt };
                string json = JsonSerializer.Serialize(requestBody);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await HttpClient.PostAsync(SessionUri, content);

                if (!response.IsSuccessStatusCode)
                    return null;

                string responseContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(responseContent))
                    return null;

                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<Character[]?> RequestJxAccountInformationAsync(string jwt)
        {
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, AccountUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

                HttpResponseMessage response = await HttpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return null;

                string responseContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(responseContent))
                    return null;

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<Character[]>(responseContent, options);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }