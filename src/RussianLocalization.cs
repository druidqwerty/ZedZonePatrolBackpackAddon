using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ZedZone.PatrolBackpackAddon;

internal static class RussianLocalization
{
    private const string CyrillicGlyphSet =
        "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
        "0123456789.,:;!?()[]{}«»—-+×/\\<>…%#*_· ";

    private static int _langCacheFrame = -1;
    private static string _langCacheCode = "en";

    private static readonly Dictionary<string, string> ExactMap = new(StringComparer.Ordinal)
    {
        // Search & basic labels
        ["Search items"] = "Поиск предметов",
        ["Tab"] = "Вкладка",
        ["Main"] = "Основной",
        ["+ New"] = "+ Новая",
        ["Recovered"] = "Восстановлено",

        // Guide
        ["? Guide"] = "? Справка",
        ["[Expedition Backpack Guide]"] = "[Справка по рюкзаку]",
        [
            "1. Left-click a tab to switch; click '+ New' to add a tab.\n" +
            "2. Right-click 'Main' to sort its items into tabs by rules.\n" +
            "3. Right-click a tab: rename, presets, reorder, delete, bulk move, rules.\n" +
            "4. Auto rules: putting an item into a tab remembers it; later pickups, crafts and transfers of that item go there.\n" +
            "5. Drag items (including a marquee selection) onto a tab to move them.\n" +
            "6. Tabs also work when the backpack is opened from a box, shelf or the ground."
        ] =
            "1. ЛКМ по вкладке — переключить; «+ Новая» — создать вкладку.\n" +
            "2. ПКМ по «Основной» — разложить вещи по правилам или создать набор вкладок.\n" +
            "3. ПКМ по вкладке — имя, пресеты, порядок, удаление, перенос, правила.\n" +
            "4. Автосортировка: положите предмет во вкладку — далее такой же лут пойдёт туда сам.\n" +
            "5. Перетаскивайте вещи (или выделение рамкой) прямо на вкладку.\n" +
            "6. Вкладки работают и в ящиках/на земле. PageUp / PageDown — листать вкладки.",

        // Original popup
        ["[Main · Sort to Tabs]"] = "[Основной · Сортировка]",
        ["Sort to Tabs by Rules"] = "Разложить по правилам",
        ["No items match tab rules"] = "Нет вещей под правила",
        ["Only sorts items with active rules"] = "Только по активным правилам",

        // Manage popup buttons & status
        ["Rename"] = "Имя",
        ["Renamed"] = "Переименовано",
        ["Delete"] = "Удал.",
        ["Store Same"] = "Сюда схож.",
        ["Store All"] = "Сюда всё",
        ["Take Same"] = "Взять схож.",
        ["Take All"] = "Всё (сброс)",
        ["Rule removed"] = "Правило удалено",
        ["Custom name"] = "Своё имя",
        ["Took all & cleared rules"] = "Всё выгружено, правила сброшены",
        ["Nothing moved"] = "Нечего переносить / нет места",
        ["Rename or pick a preset"] = "Введите имя или выберите пресет",
        ["Drop items in to learn rules"] = "Положите вещь для автоправила",

        // Quick name presets (short buttons 16-18px wide & full tab names)
        ["Wpn"] = "Ор",
        ["Amo"] = "Пт",
        ["Med"] = "Ме",
        ["Fod"] = "Ед",
        ["Sup"] = "Пр",
        ["Mat"] = "Мт",
        ["Tol"] = "Ин",
        ["Mod"] = "Об",

        ["Weapon"] = "Оружие",
        ["Ammo"] = "Патроны",
        ["Medical"] = "Медицина",
        ["Food"] = "Еда",
        ["Supply"] = "Припасы",
        ["Material"] = "Материалы",
        ["Tool"] = "Инструменты",
        ["Part"] = "Обвесы",

        // Item context menu
        ["Move to [Main]"] = "В [Основной]",
        ["Remove automatic classification"] = "Убрать автосортировку",

        // Errors & notices
        ["Backpack unavailable"] = "Рюкзак недоступен",
        ["Tab limit reached"] = "Достигнут лимит вкладок",
        ["Tab not found"] = "Вкладка не найдена",
        ["The tab still holds items; empty it before deleting."] = "Во вкладке есть вещи — сначала очистите её.",
        ["Enter a tab name"] = "Введите название вкладки",
        ["Tab names are limited to 16 characters"] = "Максимум 16 символов",
        ["Tab names cannot contain control characters"] = "Недопустимые символы в имени",
        ["A tab with that name already exists"] = "Вкладка с таким именем уже есть",
        ["Legacy tabs migrated into the backpack"] = "Старые вкладки перенесены в рюкзак",
        ["Online: put items in the backpack first, then drag them onto a tab"] = "В сети: сначала положите вещь в рюкзак, затем тяните на вкладку",
        ["Target tab is full"] = "В выбранной вкладке нет места",

        // Recovery notices
        ["Backpack recovery: an unfinished transfer was found. Backups are retained; automatic retry is blocked to avoid duplicates."] =
            "Защита рюкзака: найден незавершённый перенос. Бэкап сохранён, автоповтор отключён во избежание дублей.",
        ["Backpack recovery: moving contents to the ground."] =
            "Защита рюкзака: переносим содержимое на землю рядом.",
        ["Recovery paused: remaining contents preserved; backup retained."] =
            "Защита рюкзака на паузе: оставшиеся вещи и бэкап сохранены."
    };

    private static readonly Regex RxMovedTo = new(@"^Moved to \[(.+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxMoveTo = new(@"^Move to \[(.+)\]$", RegexOptions.Compiled);
    private static readonly Regex RxSortedStacks = new(@"^Sorted (\d+) stack\(s\) to tabs$", RegexOptions.Compiled);
    private static readonly Regex RxMovedStacks = new(@"^Moved (\d+) stack\(s\)$", RegexOptions.Compiled);
    private static readonly Regex RxRuleCount = new(@"^(\d+) rule\(s\)$", RegexOptions.Compiled);
    private static readonly Regex RxRulesRange = new(@"^Rules (\d+)-(\d+)/(\d+)$", RegexOptions.Compiled);
    private static readonly Regex RxRecoveryMoved = new(@"^Backpack recovery: (\d+) items moved to the ground\.$", RegexOptions.Compiled);

    internal static string GetLanguageBucket()
    {
        int frame = Time.frameCount;
        if (frame == _langCacheFrame)
            return _langCacheCode;

        _langCacheFrame = frame;
        _langCacheCode = DetectLanguageBucket();
        return _langCacheCode;
    }

    internal static bool IsRussian() => GetLanguageBucket() == "ru";
    internal static bool IsChinese() => GetLanguageBucket() == "cn";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string DetectLanguageBucket()
    {
        try
        {
            var gc = GameController.instance;
            string code = gc?.gameSettings?.languageCode;
            if (!string.IsNullOrWhiteSpace(code))
            {
                string low = code.Trim().ToLowerInvariant();
                if (low.StartsWith("ru") || low.Contains("russian"))
                    return "ru";
                if (low.StartsWith("zh") || low == "cn" || low.Contains("chinese"))
                    return "cn";
                return "en";
            }
        }
        catch { }

        try
        {
            if (LanguageRegistry.IsCurrentChinese())
                return "cn";
        }
        catch { }

        return "en";
    }

    internal static string Translate(string chinese, string english)
    {
        if (string.IsNullOrEmpty(english))
            return english ?? "";

        if (ExactMap.TryGetValue(english, out string exact))
            return exact;

        // "Renamed" with Chinese carrying the actual target name ("已改名为：X")
        if (english == "Renamed" && !string.IsNullOrEmpty(chinese) && chinese.StartsWith("已改名为："))
            return "Переименовано: " + chinese.Substring("已改名为：".Length);

        var m = RxMovedTo.Match(english);
        if (m.Success)
            return $"Перемещено в [{m.Groups[1].Value}]";

        m = RxMoveTo.Match(english);
        if (m.Success)
            return $"В [{m.Groups[1].Value}]";

        m = RxSortedStacks.Match(english);
        if (m.Success)
            return $"Разложено стопок: {m.Groups[1].Value}";

        m = RxMovedStacks.Match(english);
        if (m.Success)
            return $"Перемещено стопок: {m.Groups[1].Value}";

        m = RxRuleCount.Match(english);
        if (m.Success)
            return $"Правил: {m.Groups[1].Value}";

        m = RxRulesRange.Match(english);
        if (m.Success)
            return $"Правила {m.Groups[1].Value}-{m.Groups[2].Value}/{m.Groups[3].Value}";

        m = RxRecoveryMoved.Match(english);
        if (m.Success)
            return $"Защита рюкзака: перенесено на землю — {m.Groups[1].Value} шт.";

        return english;
    }

    internal static string GetSearchPlaceholder()
    {
        return GetLanguageBucket() switch
        {
            "ru" => "Поиск предметов",
            "cn" => "搜索物品",
            _ => "Search items"
        };
    }

    internal static string GetPresetPackButtonLabel()
    {
        return GetLanguageBucket() switch
        {
            "ru" => "+ Набор вкладок",
            "cn" => "+ 战术预设分页",
            _ => "+ Starter Tab Pack"
        };
    }

    internal static string GetPresetPackResultMessage(int created)
    {
        return GetLanguageBucket() switch
        {
            "ru" => created > 0 ? $"Создано вкладок: {created}" : "Все вкладки набора уже есть",
            "cn" => created > 0 ? $"已创建 {created} 个预设分页" : "预设分页已全部存在",
            _ => created > 0 ? $"Created {created} tab(s)" : "Starter tabs already exist"
        };
    }

    internal static string[] GetDefaultPresetPackNames()
    {
        string custom = AddonConfig.CustomPresetPack?.Value;
        if (!string.IsNullOrWhiteSpace(custom))
        {
            var list = new List<string>();
            foreach (string raw in custom.Split(','))
            {
                string trimmed = raw.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    list.Add(trimmed.Length > 16 ? trimmed.Substring(0, 16) : trimmed);
            }
            if (list.Count > 0)
                return list.ToArray();
        }

        return GetLanguageBucket() switch
        {
            "ru" => new[] { "Оружие", "Патроны", "Медицина", "Еда", "Материалы", "Инструменты" },
            "cn" => new[] { "武器", "弹药", "医疗", "食物", "材料", "工具" },
            _ => new[] { "Weapons", "Ammo", "Medical", "Food", "Materials", "Tools" }
        };
    }

    internal static string GetPatrolAddonDescriptionLine()
    {
        return GetLanguageBucket() switch
        {
            "ru" => "\n[Аддон] Вкладки 20×100, автосортировка, 0 веса содержимого, свежесть еды и тепло блюд. PageUp/PageDown — смена вкладок.",
            "cn" => "\n[扩展] 20×100独立分页、自动归类、包内零负重、食物保鲜与料理保温。PageUp/PageDown 切换分页。",
            _ => "\n[Addon] 20x100 independent tabs, smart auto-sorting, zero carried weight, food freshness & hot meal preservation. PageUp/PageDown to switch tabs."
        };
    }

    internal static void ApplyRussianToExpeditionAttr(ItemAttr_Backpack attr, bool legacy)
    {
        if (attr == null) return;
        attr.itemName = "Экспедиционный терморюкзак";
        attr.itemName_Runtime = attr.itemName;
        string desc = "Сетка 20×100 (2000 ячеек на вкладку) с независимыми вкладками и автосортировкой. " +
                      "При ношении содержимое не добавляет вес, еда не портится, а горячие блюда не остывают." +
                      (legacy ? " Старый формат 3×4 (обновится до 4×4 при наличии места)." : " Размер в инвентаре: 4×4.");
        attr.itemDescription = desc;
        attr.itemDescription_WithLanguage = desc;
        attr.runtimeItemDescription = desc;
        attr.cachedDescLang = "";
    }

    internal static void PreloadCyrillicGlyphs(Font font)
    {
        if (font == null) return;
        try
        {
            if (font.dynamic)
            {
                font.RequestCharactersInTexture(CyrillicGlyphSet, 8, FontStyle.Normal);
                font.RequestCharactersInTexture(CyrillicGlyphSet, 9, FontStyle.Normal);
                font.RequestCharactersInTexture(CyrillicGlyphSet, 10, FontStyle.Normal);
            }
        }
        catch { }
    }
}
