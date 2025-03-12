using System.Text;
using System.Text.Json;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private const string ApiBase = "https://api.moonshot.cn/v1/";
    private const string ApiKey = "YOUR_MOONSHOT_API_KEY"; // Ganti dengan API key Anda

    static async Task Main(string[] args)
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

        if (!await CheckApiConnection())
        {
            Console.WriteLine("Gagal terhubung ke API Moonshot.");
            return;
        }

        Console.WriteLine("Selamat datang di Moonshot LLM Chat! Ketik pesan atau 'exit' untuk keluar.");
        await RunChatLoop();
    }

    static async Task<bool> CheckApiConnection()
    {
        try
        {
            var response = await client.GetAsync($"{ApiBase}models");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    static async Task RunChatLoop()
    {
        while (true)
        {
            Console.Write("> ");
            string input = await Task.Run(() => Console.ReadLine() ?? string.Empty);

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Input tidak boleh kosong.");
                continue;
            }

            if (input.ToLower() == "exit") break;

            await StreamChatResponse(input);
        }
    }

    static async Task StreamChatResponse(string message)
    {
        var requestBody = new
        {
            model = "moonshot-v1-8k",
            messages = new[] { new { role = "user", content = message } },
            temperature = 0.3,
            stream = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            using var response = await client.PostAsync($"{ApiBase}chat/completions", content);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null) break;
                if (line.StartsWith("data: ") && !line.Contains("[DONE]"))
                {
                    var json = JsonDocument.Parse(line.Substring(6));
                    var delta = json.RootElement.GetProperty("choices")[0].GetProperty("delta");

                    if (delta.TryGetProperty("content", out JsonElement contentElement))
                    {
                        string? contentText = contentElement.GetString();
                        if (!string.IsNullOrEmpty(contentText))
                        {
                            Console.Write(contentText);
                        }
                    }
                }
            }
            Console.WriteLine();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error parsing JSON: {ex.Message}");
        }
    }
}