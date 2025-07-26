using System.Diagnostics;
using System.Text.Json;

namespace JagexAccountImporter.oauth;

    public class AccountService
    {
        private const string AuthEndpoint = "https://account.jagex.com/oauth2/auth";
        private const string RlClientId = "1fddee4e-b100-4f4e-b2b0-097f9088f9d2";

        private readonly HttpServer _httpServer = new HttpServer();
        private int _state;

        public async Task<LoginToken?> RequestLoginTokenAsync()
        {
            try
            {
                OAuth2Response? oAuthToken = await RequestIdTokenAsync();
                if (oAuthToken == null)
                    return null;

                Dictionary<string, JsonElement>? session =
                    await TokenExchange.RequestJxSessionInformationAsync(oAuthToken.IdToken);
                if (session == null || !session.TryGetValue("sessionId", out JsonElement value))
                {
                    return null;
                }

                string sessionId = value.ToString();
                if (string.IsNullOrEmpty(sessionId))
                    return null;

                Character[]? characters = await TokenExchange.RequestJxAccountInformationAsync(sessionId);
                if (characters != null)
                {
                    return new LoginToken(sessionId, characters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
            }

            return null;
        }

        private async Task<OAuth2Response?> RequestIdTokenAsync()
        {
            int currentState = Interlocked.Increment(ref _state) - 1;
            Task<OAuth2Response?> future = _httpServer.WaitForResponseAsync(currentState);

            string url = $"{AuthEndpoint}" +
                         $"?response_type=id_token+code" +
                         $"&client_id={RlClientId}" +
                         $"&nonce=00000000" +
                         $"&state={currentState:D8}" +
                         $"&prompt=login" +
                         $"&scope=openid+offline";

            Console.WriteLine("Opening browser for authentication...");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open browser: {ex.Message}");
                return null;
            }

            return await future;
        }

        public async Task StartServerAsync()
        {
            if (_httpServer.IsOnline)
                return;

            await _httpServer.StartAsync();
        }

        public void ShutdownServer()
        {
            if (!_httpServer.IsOnline)
                return;

            _httpServer.Stop();
        }
    }
