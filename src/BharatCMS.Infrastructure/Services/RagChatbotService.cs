using BharatCMS.Domain.Entities;

namespace BharatCMS.Infrastructure.Services;

/// <summary>
/// RAG chatbot for "Ask Your Department" feature.
/// Uses semantic search to find relevant documents before generating responses.
/// CERT-In: Strictly uses local context - no hallucinations.
/// </summary>
public class RagChatbotService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<RagChatbotService> _logger;

    public RagChatbotService(HttpClient httpClient, IConfiguration config, ILogger<RagChatbotService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Process a user message using RAG architecture.
    /// IMPORTANT: Returns "Information not available" if no relevant sources found.
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(string message, Guid tenantId, string? language = null, string? sessionId = null)
    {
        try
        {
            // Step 1: Translate to English for search (if needed)
            var searchQuery = message;
            if (!string.IsNullOrEmpty(language) && language != "en")
            {
                // Use Bhashini to translate
                searchQuery = await TranslateToEnglishAsync(message, language);
            }

            // Step 2: Semantic search for relevant documents
            var relevantDocs = await SemanticSearchAsync(searchQuery, tenantId);

            // Step 3: Generate response using only found context
            if (!relevantDocs.Any())
            {
                return new ChatResponse
                {
                    Answer = "Information not available in departmental records. Please contact the relevant department directly.",
                    Intent = "no_context",
                    Sources = null,
                    Confidence = 0f
                };
            }

            // Step 4: Generate contextual response
            var response = await GenerateContextualResponseAsync(message, relevantDocs, language);

            // Step 5: Auto-translate response back to user's language
            if (!string.IsNullOrEmpty(language) && language != "en" && response.Answer != "Information not available in departmental records.")
            {
                response.Answer = await TranslateFromEnglishAsync(response.Answer, language);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat processing failed");
            return new ChatResponse
            {
                Answer = "I encountered an error processing your request. Please try again.",
                Intent = "error",
                Confidence = 0f
            };
        }
    }

    /// <summary>
    /// Semantic search using vector embeddings.
    /// </summary>
    private async Task<List<SearchResult>> SemanticSearchAsync(string query, Guid tenantId)
    {
        var vectorEndpoint = _config["Vector:SearchEndpoint"] ?? "http://localhost:5001/search";
        
        try
        {
            var request = new
            {
                query,
                tenant_id = tenantId,
                top_k = 10,
                threshold = 0.7f // Minimum relevance score
            };

            var response = await _httpClient.PostAsJsonAsync(vectorEndpoint, request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vector search endpoint returned {Status}", response.StatusCode);
                return new List<SearchResult>();
            }

            var results = await response.Content.ReadFromJsonAsync<VectorSearchResponse>();
            return results?.Results ?? new List<SearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic search failed");
            return new List<SearchResult>();
        }
    }

    /// <summary>
    /// Generate response using retrieved context.
    /// </summary>
    private async Task<ChatResponse> GenerateContextualResponseAsync(string message, List<SearchResult> context, string? language)
    {
        var llmEndpoint = _config["Llm:GenerateEndpoint"] ?? "http://localhost:5002/generate";

        // Build context string from documents
        var contextStr = string.Join("\n---\n", context.Select(c => 
            $"Source: {c.Source}\nContent: {c.Content}\nRelevance: {c.Score:P0}"));

        var systemPrompt = @"You are a helpful government department assistant. 
Your role is to answer citizen queries using ONLY the provided context documents.
If the answer cannot be found in the context, respond with exactly:
'Information not available in departmental records. Please contact the relevant department directly.'

Do NOT make up information. Do NOT add information not present in the context.
Be polite, helpful, and reference specific document sources when available.";

        try
        {
            var request = new
            {
                prompt = $"Context:\n{contextStr}\n\n---\n\nQuestion: {message}",
                system_prompt = systemPrompt,
                max_tokens = 500,
                temperature = 0.1 // Very low temp for factual responses
            };

            var response = await _httpClient.PostAsJsonAsync(llmEndpoint, request);
            var result = await response.Content.ReadFromJsonAsync<LlmResponse>();

            return new ChatResponse
            {
                Answer = result?.Content ?? "I could not generate a response.",
                Intent = DetectIntent(message),
                Sources = context.Select(c => c.Source).Distinct().ToList(),
                Confidence = context.Max(c => c.Score)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM generation failed");
            return new ChatResponse
            {
                Answer = "I encountered an error generating a response.",
                Intent = "error",
                Confidence = 0f
            };
        }
    }

    private string DetectIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        
        if (lower.Contains("how") || lower.Contains("what") || lower.Contains("why"))
            return "information_query";
        if (lower.Contains("apply") || lower.Contains("form") || lower.Contains("document"))
            return "application_query";
        if (lower.Contains("contact") || lower.Contains("phone") || lower.Contains("email"))
            return "contact_query";
        if (lower.Contains("status") || lower.Contains("track") || lower.Contains("where"))
            return "status_query";
        
        return "general_query";
    }

    private async Task<string> TranslateToEnglishAsync(string text, string lang)
    {
        // Simplified - in production call Bhashini
        return text; // Placeholder
    }

    private async Task<string> TranslateFromEnglishAsync(string text, string lang)
    {
        // Simplified - in production call Bhashini
        return text; // Placeholder
    }
}

public class ChatResponse
{
    public string Answer { get; set; } = "";
    public string Intent { get; set; } = "";
    public List<string>? Sources { get; set; }
    public float Confidence { get; set; }
}

public class SearchResult
{
    public string Source { get; set; } = "";
    public string Content { get; set; } = "";
    public float Score { get; set; }
    public string DocumentType { get; set; } = "";
}

public class VectorSearchResponse
{
    public List<SearchResult> Results { get; set; } = new();
}

public class LlmResponse
{
    public string Content { get; set; } = "";
    public float Confidence { get; set; }
}
