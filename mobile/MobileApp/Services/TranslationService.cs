using System.Text;
using System.Text.Json;

namespace MobileApp.Services;

public class TranslationService
{
    private static readonly HttpClient TranslatorClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<string?> TranslateAsync(
        string text,
        string targetLanguageCode,
        string sourceLanguageCode = "auto",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetLanguageCode))
        {
            return null;
        }

        var normalizedTarget = NormalizeLanguage(targetLanguageCode);
        var normalizedSource = string.IsNullOrWhiteSpace(sourceLanguageCode) ? "auto" : NormalizeLanguage(sourceLanguageCode);

        if (!string.Equals(normalizedSource, "auto", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var endpoint =
            $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={Uri.EscapeDataString(normalizedSource)}&tl={Uri.EscapeDataString(normalizedTarget)}&dt=t&q={Uri.EscapeDataString(text)}";

        try
        {
            using var response = await TranslatorClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            var segments = document.RootElement[0];
            if (segments.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var segment in segments.EnumerateArray())
            {
                if (segment.ValueKind != JsonValueKind.Array || segment.GetArrayLength() == 0)
                {
                    continue;
                }

                var translatedPart = segment[0].GetString();
                if (!string.IsNullOrWhiteSpace(translatedPart))
                {
                    builder.Append(translatedPart);
                }
            }

            var translated = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(translated) ? null : translated;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();
        var delimiter = normalized.IndexOfAny(new[] { '-', '_' });
        if (delimiter > 0)
        {
            normalized = normalized[..delimiter];
        }

        return normalized switch
        {
            "vn" => "vi",
            _ => normalized
        };
    }
}
