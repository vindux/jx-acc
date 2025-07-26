using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Web;

namespace JagexAccountImporter.oauth;

public class HttpServer
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<OAuth2Response?>> _futures = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Timer? _shutdownTimer;

    public bool IsOnline => _listener is { IsListening: true };

    public async Task StartAsync()
    {
        if (_listener != null)
            return;

        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:80/");

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                "Failed to start HTTP server. Make sure port 80 is available (run as admin).",
                ex);
        }

        _cancellationTokenSource = new CancellationTokenSource();

        _ = Task.Run(async () => await ListenForRequestsAsync(_cancellationTokenSource.Token));

        _shutdownTimer = new Timer(_ => Stop(), null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Close();
            _listener = null;
        }

        _cancellationTokenSource?.Cancel();
        _shutdownTimer?.Dispose();

        foreach (TaskCompletionSource<OAuth2Response?> future in _futures.Values)
        {
            future.TrySetException(new InvalidOperationException("Server stopped"));
        }

        _futures.Clear();
    }

    public Task<OAuth2Response?> WaitForResponseAsync(int state)
    {
        TaskCompletionSource<OAuth2Response?> tcs = new TaskCompletionSource<OAuth2Response?>();
        _futures[state] = tcs;
        return tcs.Task;
    }

    private async Task ListenForRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HTTP server: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.HttpMethod != "GET")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            string path = request.Url?.LocalPath ?? "/";

            switch (path)
            {
                case "/" or "":
                    await HandleRootRequestAsync(response);
                    break;
                case "/capture":
                    await HandleCaptureRequestAsync(request, response);
                    break;
                default:
                    response.StatusCode = 404;
                    response.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // ignored
            }
        }
    }

    private async Task HandleRootRequestAsync(HttpListenerResponse response)
    {
        string htmlContent = CreateBasicHtmlPage(
            """
            const url = window.location.href;
            if (url.includes('localhost/#')) {
                window.location.href = url.replace('localhost/#', 'localhost/capture?');
            } else {
                alert('Something went wrong');
            }
            """,
            "Redirecting.."
        );

        await SendHtmlResponseAsync(response, htmlContent);
    }

    private async Task HandleCaptureRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.Url?.Query != null)
        {
            Dictionary<string, string> queryParams = ParseQueryParams(request.Url.Query);

            if (queryParams.TryGetValue("error", out string? error))
            {
                string description = queryParams.GetValueOrDefault("error_description", "");
                Console.WriteLine($"Received error from OAuth2 server: {error}: {description}");
                await SendHtmlResponseAsync(response, $"Error: {error}");
                return;
            }

            if (!queryParams.TryGetValue("code", out string? code)
                || !queryParams.TryGetValue("id_token", out string? idToken)
                || !queryParams.TryGetValue("state", out string? stateStr))
            {
                response.StatusCode = 400;
                response.Close();
                return;
            }

            if (int.TryParse(stateStr, out int state)
                && _futures.TryRemove(state, out TaskCompletionSource<OAuth2Response?>? future))
            {
                OAuth2Response oauthResponse = new OAuth2Response(code, idToken);
                future.TrySetResult(oauthResponse);
            }
        }

        string htmlResponse = CreateBasicHtmlPage("", "Everything is complete. You can close the window now.");
        await SendHtmlResponseAsync(response, htmlResponse);
    }

    private static async Task SendHtmlResponseAsync(HttpListenerResponse response, string content)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        response.ContentType = "text/html; charset=UTF-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }

    private static string CreateBasicHtmlPage(string js, string body)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("<html><head><script>");
        builder.Append(js);
        builder.Append("</script></head><body>");
        builder.Append(body);
        builder.Append("</body></html>");
        return builder.ToString();
    }

    private static Dictionary<string, string> ParseQueryParams(string query)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(query))
            return parameters;

        if (query.StartsWith($"?"))
        {
            query = query[1..];
        }

        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;

            string key = HttpUtility.UrlDecode(parts[0]);
            string value = HttpUtility.UrlDecode(parts[1]);
            parameters[key] = value;
        }

        return parameters;
    }
}