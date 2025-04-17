namespace CVexplorer.Services.Implementation;

using CVexplorer.Models.DTO;
using CVexplorer.Services.Interface;
using System.Text.Json;
using AutoMapper;
using CVexplorer.Models.Domain;
using System.Text.Json.Serialization;

public class CvEvaluationService(HttpClient http, IMapper mapper) : ICvEvaluationService
{

    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions CaseIns = new() { PropertyNameCaseInsensitive = true };


    public async Task<CvEvaluationResultDTO> EvaluateAsync(string cvText, Position position,CancellationToken ct = default)
    {
        var payload = new CvEvaluationRequestDTO
        {
            CvText = cvText,
            Position = mapper.Map<PositionDTO>(position)
        };

        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,                 // leave property names alone
            Converters = { new JsonStringEnumConverter() }    // leave C# names as-is
        };


        using var response = await http.PostAsJsonAsync("/evaluate-cv", payload, opts, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CvEvaluationResultDTO>(
            opts,    // this handles camelCase → PascalCase + enum parsing
            ct);

        if (result is null)
            throw new InvalidOperationException("FastAPI returned an empty body.");

        return result;
    }
}
