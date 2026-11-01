using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Russian display names for iptv-org categories.
/// </summary>
internal static class CategoryNames
{
    private static readonly FrozenDictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["animation"] = "Анимация",
        ["auto"] = "Авто",
        ["business"] = "Бизнес",
        ["classic"] = "Классика",
        ["comedy"] = "Комедия",
        ["cooking"] = "Кулинария",
        ["culture"] = "Культура",
        ["documentary"] = "Документальные",
        ["education"] = "Образование",
        ["entertainment"] = "Развлекательные",
        ["family"] = "Семейные",
        ["general"] = "Общие",
        ["interactive"] = "Интерактивные",
        ["kids"] = "Детские",
        ["legislative"] = "Парламентские",
        ["lifestyle"] = "Стиль жизни",
        ["movies"] = "Фильмы",
        ["music"] = "Музыка",
        ["news"] = "Новости",
        ["outdoor"] = "Активный отдых",
        ["public"] = "Общественные",
        ["relax"] = "Релакс",
        ["religious"] = "Религия",
        ["series"] = "Сериалы",
        ["science"] = "Наука",
        ["shop"] = "Магазины",
        ["sports"] = "Спорт",
        ["travel"] = "Путешествия",
        ["weather"] = "Погода",
        ["xxx"] = "18+",
        [IptvOrgConstants.UndefinedCategory] = "Без категории"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the display name for a category.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="fallback">Name to use for unknown ids (usually the English API name).</param>
    /// <returns>The Russian name, the fallback, or the id itself.</returns>
    public static string Get(string id, string? fallback = null)
    {
        if (Russian.TryGetValue(id, out var name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(fallback) ? id : fallback;
    }
}
