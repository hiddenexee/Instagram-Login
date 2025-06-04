using System.Text;
using System.Security.Cryptography;
using Sodium;
using RestSharp;
using System.Net;

public class InstagramPasswordEncryptor
{
    private readonly IRestClient _client;
    private readonly Random _random = new();

    public InstagramPasswordEncryptor(IRestClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public static async Task Main(string[] args)
    {
        string host = ""; // IP address here
        string port = ""; // Port here
        string proxyUsername = ""; // Proxy username here
        string proxyPassword = ""; // Proxy password here

        var proxy = new WebProxy($"http://{host}:{port}")
        {
            Credentials = new NetworkCredential(proxyUsername, proxyPassword)
        };
        var options = new RestClientOptions("https://www.instagram.com")
        {
            Proxy = proxy,
            ThrowOnAnyError = true
        };

        var client = new RestClient(options);
        var encryptor = new InstagramPasswordEncryptor(client);

        string username = "asdasda";
        string password = "sdasdasd";
        string publicKeyHex = "977121591a9450dbb37e2f0d0cd35a3278f615b2b3c301da811dfd6b939df223"; // https://www.instagram.com/data/shared_data/
        string keyId = "231";
        string version = "10";

        string encPassword = GenerateEncPassword(password, publicKeyHex, keyId, version);
        // source: https://gist.github.com/huoshan12345/44b1b4927b21d4ec21d1cbd61ea659da, https://stackoverflow.com/questions/62076725/instagram-enc-password-generation

        await encryptor.InstagramLogin(username, encPassword);
        Console.ReadKey();
    }

    private string GenerateToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";

        string RandomPart(int length) =>
            new([.. Enumerable.Repeat(chars, length).Select(s => s[_random.Next(s.Length)])]);

        return $"{RandomPart(6)}:{RandomPart(6)}:{RandomPart(6)}";
    }

    private static void AddCommonHeaders(RestRequest request)
    {
        var headers = new Dictionary<string, string>
        {
            { "accept", "*/*" },
            { "accept-language", "tr-TR,tr;q=0.9" },
            { "origin", "https://www.instagram.com" },
            { "referer", "https://www.instagram.com/" },
            { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36" },
            { "x-ig-app-id", "936619743392459" },
            { "sec-ch-ua", "\"Google Chrome\";v=\"137\", \"Chromium\";v=\"137\", \"Not/A)Brand\";v=\"24\"" },
            { "sec-ch-ua-full-version-list", "\"Google Chrome\";v=\"137.0.7151.56\", \"Chromium\";v=\"137.0.7151.56\", \"Not/A)Brand\";v=\"24.0.0.0\"" },
            { "sec-ch-ua-mobile", "?0" },
            { "sec-ch-ua-platform", "\"Windows\"" },
            { "sec-ch-ua-platform-version", "\"19.0.0\"" },
            { "sec-fetch-dest", "empty" },
            { "sec-fetch-mode", "cors" },
            { "sec-fetch-site", "same-origin" }
        };

        foreach (var header in headers)
        {
            request.AddHeader(header.Key, header.Value);
        }
    }

    private async Task<string> GetCookie()
    {
        var request = new RestRequest("", Method.Get);
        request.AddHeader("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        request.AddHeader("accept-language", "tr-TR,tr;q=0.9");
        request.AddHeader("cache-control", "max-age=0");
        request.AddHeader("dpr", "1");
        request.AddHeader("priority", "u=0, i");
        request.AddHeader("sec-ch-prefers-color-scheme", "light");
        request.AddHeader("sec-ch-ua", "\"Google Chrome\";v=\"137\", \"Chromium\";v=\"137\", \"Not/A)Brand\";v=\"24\"");
        request.AddHeader("sec-ch-ua-full-version-list", "\"Google Chrome\";v=\"137.0.7151.56\", \"Chromium\";v=\"137.0.7151.56\", \"Not/A)Brand\";v=\"24.0.0.0\"");
        request.AddHeader("sec-ch-ua-mobile", "?0");
        request.AddHeader("sec-ch-ua-model", "\"\"");

        request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
        request.AddHeader("sec-ch-ua-platform-version", "\"19.0.0\"");
        request.AddHeader("sec-fetch-dest", "document");
        request.AddHeader("sec-fetch-mode", "navigate");
        request.AddHeader("sec-fetch-site", "same-origin");
        request.AddHeader("sec-fetch-user", "?1");
        request.AddHeader("upgrade-insecure-requests", "1");
        request.AddHeader("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
        request.AddHeader("viewport-width", "2784");

        try
        {
            var response = await _client.ExecuteAsync(request);
            if (response?.Headers != null && response.IsSuccessful)
            {
                var cookies = response.Headers
                    .Where(header => header.Name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                    .Select(header => header.Value?.ToString())
                    .ToList();

                return cookies.FirstOrDefault(cookie => cookie?.StartsWith("csrftoken=") == true)?
                    .Split(';')[0].Split('=')[1] ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Beklenmeyen bir hata oluştu: {ex.Message}");
        }

        return string.Empty;
    }

    private string GetPostData(string username, string encPassword)
    {
        return $"enc_password={encPassword}&username={username}&caaF2DebugGroup=0&isPrivacyPortalReq=false&loginAttemptSubmissionCount=0&optIntoOneTap=false&queryParams=%7B%7D&trustedDeviceRecords=%7B%7D&jazoest={_random.Next(20000, 22000)}";
    }

    public async Task InstagramLogin(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Auth error");
            return;
        }

        string csrfToken = await GetCookie();
        string webSessionId = GenerateToken();
        string encPassword = password;

        var request = new RestRequest("api/v1/web/accounts/login/ajax/", Method.Post);
        AddCommonHeaders(request);

        request.AddHeader("x-csrftoken", csrfToken);
        request.AddHeader("x-web-session-id", webSessionId);

        var postData = GetPostData(username, encPassword);
        request.AddParameter("application/x-www-form-urlencoded", postData, ParameterType.RequestBody);

        try
        {
            var response = await _client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                Console.WriteLine(response.Content);
            }
            else
            {
                Console.WriteLine($"Hata: {response.StatusCode} - {response.ErrorMessage ?? response.Content}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Beklenmeyen bir hata oluştu: {ex.Message}");
        }
    }

    public static string GenerateEncPassword(string password, string publicKey, string keyId, string version)
    {
        var time = DateTime.UtcNow.ToTimestamp();
        var keyBytes = publicKey.HexToBytes();
        var key = new byte[32];
        new Random().NextBytes(key);
        var iv = new byte[12];
        var tag = new byte[16];
        var plainText = Encoding.UTF8.GetBytes(password);
        var cipherText = new byte[plainText.Length];

        using (var cipher = new AesGcm(key, tag.Length))
        {
            cipher.Encrypt(iv, plainText, cipherText, tag, Encoding.UTF8.GetBytes(time.ToString()));
        }

        var encryptedKey = SealedPublicKeyBox.Create(key, keyBytes);
        var bytesOfLen = BitConverter.GetBytes((short)encryptedKey.Length);
        var info = new byte[] { 1, byte.Parse(keyId) };
        var bytes = info.Concat(bytesOfLen).Concat(encryptedKey).Concat(tag).Concat(cipherText);

        return $"#PWD_INSTAGRAM_BROWSER:{version}:{time}:{Convert.ToBase64String(bytes)}";
    }
}

public static class Extensions
{
    public static byte[] HexToBytes(this string hex)
    {
        return [.. Enumerable.Range(0, hex.Length / 2).Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))];
    }

    public static T[] Concat<T>(this T[] x, T[] y)
    {
        var z = new T[x.Length + y.Length];
        x.CopyTo(z, 0);
        y.CopyTo(z, x.Length);
        return z;
    }

    private static readonly DateTime _jan1St1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static long ToTimestamp(this DateTime d)
    {
        return (long)(d.ToUniversalTime() - _jan1St1970).TotalSeconds;
    }
}