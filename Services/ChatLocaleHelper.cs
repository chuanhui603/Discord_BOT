using System.Globalization;
using Discord_BOT.Options;

namespace Discord_BOT.Services;

public static class ChatLocaleHelper
{
    private const string DefaultLocale = "zh-TW";
    private const string DefaultGuidanceMessage = "目前沒有足夠依據可以可靠回答，請提供更具體的文件名、條文或關鍵字。";
    private const string DefaultUnavailableMessage = "主要服務暫時無法使用，請稍後再試。";
    private const string DefaultDegradedWarningMessage = "警告：目前使用備援模式，以下回覆可能較簡化，且不一定依據知識庫內容。";

    public static string NormalizeLocale(string? userLocale)
    {
        if (string.IsNullOrWhiteSpace(userLocale))
        {
            return DefaultLocale;
        }

        var candidate = userLocale.Trim().Replace('_', '-');

        try
        {
            return CultureInfo.GetCultureInfo(candidate).Name;
        }
        catch (CultureNotFoundException)
        {
            return candidate.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? DefaultLocale
                : DefaultLocale;
        }
    }

    public static string GetResponseLanguage(string? userLocale)
    {
        var normalizedLocale = NormalizeLocale(userLocale);
        var localeGroup = GetLocaleGroup(normalizedLocale);

        return localeGroup switch
        {
            "zh-Hans" => "Simplified Chinese",
            "zh-Hant" => "Traditional Chinese",
            _ => CultureInfo.GetCultureInfo(normalizedLocale).EnglishName
        };
    }

    public static string GetGuidanceMessage(string? userLocale, FallbackOptions options)
    {
        return GetFallbackText(userLocale, options).GuidanceMessage;
    }

    public static string GetUnavailableMessage(string? userLocale, FallbackOptions options)
    {
        return GetFallbackText(userLocale, options).UnavailableMessage;
    }

    public static string GetDegradedWarningMessage(string? userLocale, FallbackOptions options)
    {
        return GetFallbackText(userLocale, options).DegradedWarningMessage;
    }

    private static LocalizedFallbackText GetFallbackText(string? userLocale, FallbackOptions options)
    {
        return GetLocaleGroup(NormalizeLocale(userLocale)) switch
        {
            "zh-Hans" => new LocalizedFallbackText(
                "目前没有足够依据可以可靠回答，请提供更具体的文件名、条文或关键词。",
                "主要服务暂时无法使用，请稍后再试。",
                "警告：目前使用备用模式，以下回复可能较简化，且不一定依据知识库内容。"),
            "en" => new LocalizedFallbackText(
                "I do not have enough reliable context to answer that yet. Please provide a more specific document name, clause, or keyword.",
                "The primary service is temporarily unavailable. Please try again later.",
                "Warning: fallback mode is active, so the reply may be simplified and may not reflect the knowledge base."),
            "ja" => new LocalizedFallbackText(
                "信頼できる回答をするための十分な根拠がありません。より具体的な文書名、条文、またはキーワードを入力してください。",
                "主要サービスは一時的に利用できません。しばらくしてから再試行してください。",
                "警告: 現在はフォールバックモードを使用しているため、回答が簡略化され、知識ベースに基づかない場合があります。"),
            "ko" => new LocalizedFallbackText(
                "신뢰할 만한 답변을 하기 위한 근거가 아직 충분하지 않습니다. 더 구체적인 문서명, 조항 또는 키워드를 알려 주세요.",
                "기본 서비스가 일시적으로 사용할 수 없습니다. 잠시 후 다시 시도해 주세요.",
                "경고: 현재 대체 모드가 활성화되어 있어 답변이 단순화될 수 있으며 지식 베이스를 반영하지 않을 수 있습니다."),
            "es" => new LocalizedFallbackText(
                "Todavía no tengo suficiente contexto fiable para responder. Proporciona un nombre de documento, cláusula o palabra clave más específico.",
                "El servicio principal no está disponible temporalmente. Inténtalo de nuevo más tarde.",
                "Advertencia: el modo de respaldo está activo, por lo que la respuesta puede ser más simple y no reflejar la base de conocimiento."),
            "fr" => new LocalizedFallbackText(
                "Je n'ai pas encore assez de contexte fiable pour répondre. Fournissez un nom de document, une clause ou un mot-clé plus précis.",
                "Le service principal est temporairement indisponible. Réessayez plus tard.",
                "Avertissement : le mode de secours est actif, la réponse peut donc être simplifiée et ne pas refléter la base de connaissances."),
            "de" => new LocalizedFallbackText(
                "Ich habe noch nicht genug verlässlichen Kontext, um zu antworten. Bitte gib einen genaueren Dokumentnamen, Abschnitt oder ein Stichwort an.",
                "Der Hauptdienst ist vorübergehend nicht verfügbar. Bitte versuche es später erneut.",
                "Warnung: Der Fallback-Modus ist aktiv. Die Antwort kann daher vereinfacht sein und die Wissensdatenbank möglicherweise nicht widerspiegeln."),
            "pt" => new LocalizedFallbackText(
                "Ainda não tenho contexto confiável suficiente para responder. Informe um nome de documento, cláusula ou palavra-chave mais específico.",
                "O serviço principal está temporariamente indisponível. Tente novamente mais tarde.",
                "Aviso: o modo de contingência está ativo, então a resposta pode ser simplificada e talvez não reflita a base de conhecimento."),
            _ => new LocalizedFallbackText(
                Coalesce(options.GuidanceMessage, DefaultGuidanceMessage),
                Coalesce(options.UnavailableMessage, DefaultUnavailableMessage),
                Coalesce(options.DegradedWarningMessage, DefaultDegradedWarningMessage))
        };
    }

    private static string GetLocaleGroup(string normalizedLocale)
    {
        if (normalizedLocale.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) ||
            normalizedLocale.StartsWith("zh-SG", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-Hans";
        }

        if (normalizedLocale.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-Hant";
        }

        return CultureInfo.GetCultureInfo(normalizedLocale).TwoLetterISOLanguageName;
    }

    private static string Coalesce(string? configuredValue, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(configuredValue) ? defaultValue : configuredValue;
    }

    private sealed record LocalizedFallbackText(
        string GuidanceMessage,
        string UnavailableMessage,
        string DegradedWarningMessage);
}