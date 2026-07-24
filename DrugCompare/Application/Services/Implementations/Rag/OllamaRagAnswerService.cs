using System.Net.Http.Json;
using System.Net.Http;
using System.Text.Json;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;
using Microsoft.Extensions.Configuration;

namespace DrugCompare.Application.Services.Implementations.Rag;

public sealed class OllamaRagAnswerService : IRagAnswerService
{
    private const int MaxSources = 8;
    private const int MaxSourceCharacters = 1100;
    private readonly HttpClient _httpClient;
    private readonly IRagAnswerValidator _validator;
    private readonly bool _enabled;
    private readonly string _model;

    public OllamaRagAnswerService(HttpClient httpClient, IConfiguration configuration, IRagAnswerValidator validator)
    {
        _httpClient = httpClient;
        _validator = validator;
        _enabled = !bool.TryParse(configuration["Rag:Ollama:Enabled"], out var enabled) || enabled;
        _model = configuration["Rag:Ollama:Model"] ?? "qwen3:8b";
        _httpClient.BaseAddress = new Uri(configuration["Rag:Ollama:Endpoint"] ?? "http://127.0.0.1:11434/", UriKind.Absolute);
        var timeoutSeconds = int.TryParse(configuration["Rag:Ollama:TimeoutSeconds"], out var configuredTimeout)
            ? configuredTimeout
            : 180;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 30, 600));
    }

    public async Task<RagAnswer> GenerateAsync(string question, IReadOnlyList<KnowledgeChunkResult> retrievedSources, CancellationToken cancellationToken = default)
    {
        if (!_enabled) throw new InvalidOperationException("Lokalny RAG jest wyłączony w appsettings.json.");
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("Question is required.", nameof(question));

        var sources = retrievedSources
            .Where(source => source.ReviewStatus is "reviewed" or "verified")
            .Take(MaxSources)
            .Select(source => new
            {
                source.Id, source.ProductName, source.ActiveSubstance, source.SectionNumber, source.SectionTitle, source.ReviewStatus,
                Text = source.ChunkText.Length <= MaxSourceCharacters ? source.ChunkText : source.ChunkText[..MaxSourceCharacters]
            })
            .ToList();

        if (sources.Count == 0)
        {
            return new RagAnswer
            {
                Summary = "Nie znaleziono zweryfikowanych źródeł w lokalnej bazie. Brak danych nie oznacza bezpieczeństwa.",
                InsufficientEvidence = true
            };
        }

        var payload = new
        {
            model = _model,
            stream = false,
            format = "json",
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Pytanie: {question}\n\nDostarczone źródła JSON:\n{JsonSerializer.Serialize(sources)}" }
            }
        };

        var answer = await RequestAnswerAsync(payload, cancellationToken);
        var validation = _validator.Validate(answer, retrievedSources);
        if (!validation.IsValid)
        {
            var allowedIds = string.Join(", ", sources.Select(source => source.Id));
            var repairPayload = new
            {
                model = _model,
                stream = false,
                format = "json",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"Napraw wyłącznie cytowania w odpowiedzi JSON. Dozwolone sourceIds to wyłącznie: [{allowedIds}]. " +
                                  "Nie używaj żadnych innych liczb jako sourceIds. Jeżeli nie możesz poprzeć wniosku dozwolonym ID, ustaw insufficientEvidence=true i findings=[]."
                    },
                    new { role = "user", content = $"Odpowiedź do poprawy: {JsonSerializer.Serialize(answer)}" }
                }
            };
            answer = await RequestAnswerAsync(repairPayload, cancellationToken);
            validation = _validator.Validate(answer, retrievedSources);
        }

        if (!validation.IsValid) throw new InvalidOperationException($"Odpowiedź modelu została odrzucona: {validation.Error}");
        return answer;
    }

    private async Task<RagAnswer> RequestAnswerAsync(object payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/chat", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama returned HTTP {(int)response.StatusCode}. Upewnij się, że model '{_model}' jest pobrany lokalnie.");

        var envelope = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned an empty response.");
        return JsonSerializer.Deserialize<RagAnswer>(envelope.Message?.Content ?? string.Empty, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Ollama did not return a valid JSON answer.");
    }

    private const string SystemPrompt = """
        Jesteś lokalnym redaktorem źródeł medycznych. Odpowiadasz WYŁĄCZNIE na podstawie dostarczonych źródeł.
        Nie korzystaj z wiedzy własnej i nie zgaduj. Zwróć wyłącznie JSON:
        {"summary":"...","findings":[{"claim":"...","sourceIds":[123]}],"insufficientEvidence":false}.
        Każdy finding musi mieć co najmniej jeden sourceId dokładnie z dostarczonych źródeł.
        Gdy źródła nie wystarczają, ustaw insufficientEvidence=true i zwróć pustą listę findings.
        """;

    private sealed class OllamaResponse { public OllamaMessage? Message { get; set; } }
    private sealed class OllamaMessage { public string? Content { get; set; } }
}
