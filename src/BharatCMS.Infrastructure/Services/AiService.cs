namespace BharatCMS.Infrastructure.Services;

/// <summary>
/// AI service for content generation, summarization, and OCR.
/// Uses configurable AI backend (can be swapped for OpenAI, Claude, etc.)
/// </summary>
public class AiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AiService> _logger;

    public AiService(HttpClient httpClient, IConfiguration config, ILogger<AiService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Summarize a document using AI.
    /// </summary>
    public async Task<string> SummarizeAsync(string content, string language = "en", int maxLength = 500)
    {
        // Integration point for Bhashini AI or other AI service
        var prompt = $"Summarize the following text in {language} language, keeping it under {maxLength} characters:\n\n{content}";
        return await CallAiAsync(prompt, "summarize");
    }

    /// <summary>
    /// Generate SEO metadata for content.
    /// </summary>
    public async Task<SeoMetadata> GenerateSeoMetadataAsync(string title, string content)
    {
        var prompt = $"Generate SEO metadata for the following content:\n\nTitle: {title}\n\nContent: {content.Substring(0, Math.Min(content.Length, 1000))}";
        var response = await CallAiAsync(prompt, "seo");
        
        // Parse JSON response
        return new SeoMetadata
        {
            MetaTitle = ExtractJsonValue(response, "meta_title") ?? title,
            MetaDescription = ExtractJsonValue(response, "meta_description") ?? "",
            Keywords = ExtractJsonValue(response, "keywords") ?? ""
        };
    }

    /// <summary>
    /// Draft a circular using AI helper.
    /// </summary>
    public async Task<string> DraftCircularAsync(string subject, string department, string details)
    {
        var prompt = $"Draft an official government circular from {department} regarding: {subject}\n\nDetails: {details}";
        return await CallAiAsync(prompt, "draft_circular");
    }

    /// <summary>
    /// Translate content to target language.
    /// </summary>
    public async Task<string> TranslateAsync(string content, string fromLang, string toLang)
    {
        var prompt = $"Translate the following from {fromLang} to {toLang}:\n\n{content}";
        return await CallAiAsync(prompt, "translate");
    }

    /// <summary>
    /// Compress image using AI optimization.
    /// </summary>
    public async Task<byte[]> CompressImageAsync(byte[] imageData, int quality = 80)
    {
        // ImageSharp handles compression - this is for AI-assisted optimization
        using var input = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(imageData);
        using var ms = new MemoryStream();
        await input.SaveAsync(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality });
        return ms.ToArray();
    }

    private async Task<string> CallAiAsync(string prompt, string operation)
    {
        var endpoint = _config[$"Ai:{operation}Endpoint"] ?? _config["Ai:DefaultEndpoint"];
        
        try
        {
            var request = new
            {
                prompt,
                max_tokens = 1000,
                temperature = 0.3 // Low temp for factual responses
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AiResponse>();
            return result?.Content ?? "AI response unavailable";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service call failed for operation {Operation}", operation);
            return $"AI service error: {operation}";
        }
    }

    private static string? ExtractJsonValue(string json, string key)
    {
        try
        {
            var index = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;
            var start = json.IndexOf(':', index) + 1;
            var end = json.IndexOf(',', start);
            if (end < 0) end = json.IndexOf('}', start);
            return json.Substring(start, end - start).Trim().Trim('"', ' ', '\n');
        }
        catch { return null; }
    }
}

public class SeoMetadata
{
    public string MetaTitle { get; set; } = "";
    public string MetaDescription { get; set; } = "";
    public string Keywords { get; set; } = "";
}

public class AiResponse
{
    public string Content { get; set; } = "";
    public float Confidence { get; set; }
}

/// <summary>
/// Bhashini AI integration for multilingual support.
/// </summary>
public class BhashiniService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<BhashiniService> _logger;

    // Language codes
    public static readonly Dictionary<string, string> LanguageCodes = new()
    {
        { "en", "eng-Latn" }, { "hi", "hin-Deva" }, { "mr", "mar-Deva" },
        { "te", "tel-Telu" }, { "ta", "tam-Taml" }, { "bn", "ben-Beng" }
    };

    public BhashiniService(HttpClient httpClient, IConfiguration config, ILogger<BhashiniService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Speech to text using Bhashini STT API.
    /// </summary>
    public async Task<SttResult> SpeechToTextAsync(byte[] audioData, string language = "hi")
    {
        var endpoint = _config["Bhashini:SttEndpoint"] ?? "https://api.bhashini.gov.in/v1/stt";
        var apiKey = _config["Bhashini:ApiKey"];

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(audioData), "audio", "recording.wav");
        content.Add(new StringContent(LanguageCodes.GetValueOrDefault(language, "hin-Deva")), "language_code");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.PostAsync(endpoint, content);
            var result = await response.Content.ReadFromJsonAsync<SttResponse>();

            return new SttResult(result?.Text ?? "", language, result?.Confidence ?? 0.9f);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STT failed for language {Language}", language);
            return new SttResult("", language, 0f);
        }
    }

    /// <summary>
    /// Transliterate text from one script to another.
    /// </summary>
    public async Task<string> TransliterateAsync(string text, string fromLang, string toLang)
    {
        // Integration point for Bhashini transliteration API
        var endpoint = _config["Bhashini:TransliterationEndpoint"] ?? "https://api.bhashini.gov.in/v1/transliterate";
        
        var request = new
        {
            text,
            source_language = LanguageCodes.GetValueOrDefault(fromLang, "hin-Deva"),
            target_language = LanguageCodes.GetValueOrDefault(toLang, "eng-Latn")
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            var result = await response.Content.ReadFromJsonAsync<TransliterationResponse>();
            return result?.Output ?? text;
        }
        catch
        {
            return text; // Fallback to original
        }
    }

    /// <summary>
    /// Auto-translate to English for search indexing.
    /// </summary>
    public async Task<string> TranslateToEnglishAsync(string text, string sourceLang)
    {
        if (sourceLang == "en") return text;
        return await TranslateAsync(text, sourceLang, "en");
    }

    private async Task<string> TranslateAsync(string text, string fromLang, string toLang)
    {
        var endpoint = _config["Bhashini:TranslateEndpoint"] ?? "https://api.bhashini.gov.in/v1/translate";
        
        var request = new
        {
            source_language = LanguageCodes.GetValueOrDefault(fromLang, "hin-Deva"),
            target_language = LanguageCodes.GetValueOrDefault(toLang, "eng-Latn"),
            text
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            var result = await response.Content.ReadFromJsonAsync<TranslationResponse>();
            return result?.Output ?? text;
        }
        catch
        {
            return text;
        }
    }
}

public record SttResult(string Text, string Language, float Confidence);
public record SttResponse(string Text, float Confidence);
public record TransliterationResponse(string Output);
public record TranslationResponse(string Output);
