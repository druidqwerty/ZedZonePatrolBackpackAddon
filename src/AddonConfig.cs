using BepInEx.Configuration;

namespace ZedZone.PatrolBackpackAddon;

internal static class AddonConfig
{
    internal static ConfigEntry<bool> EnablePatrolBackpack;
    internal static ConfigEntry<bool> EnableRussianLocalization;
    internal static ConfigEntry<bool> SuppressOriginalFreeGift;
    internal static ConfigEntry<bool> EnableQuickTabHotkeys;
    internal static ConfigEntry<bool> EnablePresetPackButton;
    internal static ConfigEntry<string> CustomPresetPack;
    internal static ConfigEntry<bool> CraftablePatrolBackpack;

    internal static void Bind(ConfigFile config)
    {
        EnablePatrolBackpack = config.Bind(
            "PatrolBackpack",
            "EnablePatrolBackpack",
            true,
            "Enable all Expedition Backpack features (20x100 independent tabs, auto-sorting, zero carried weight, food freshness, hot meal preservation, search, container mode) on the Tactical Patrol Backpack (itemId 513).");

        SuppressOriginalFreeGift = config.Bind(
            "PatrolBackpack",
            "SuppressOriginalFreeGift",
            true,
            "Prevent the original Expedition Backpack mod from auto-gifting a free Expedition Fresh Backpack (925722) on first load. Existing Expedition Backpacks keep working normally.");

        CraftablePatrolBackpack = config.Bind(
            "PatrolBackpack",
            "CraftablePatrolBackpack",
            false,
            "Optional: register a handcraft recipe for Tactical Patrol Backpack (513) using 20x Cloth. Disabled by default so it stays loot/shop progression.");

        EnableRussianLocalization = config.Bind(
            "Localization",
            "EnableRussianLocalization",
            true,
            "Translate all Expedition Backpack UI, tab presets, buttons, notices, search placeholder, recovery messages and in-game guide into Russian when the game language is Russian.");

        EnableQuickTabHotkeys = config.Bind(
            "QualityOfLife",
            "EnableQuickTabHotkeys",
            true,
            "Switch backpack tabs with PageUp / PageDown while the backpack inventory is open (ignored when typing in chat or input fields).");

        EnablePresetPackButton = config.Bind(
            "QualityOfLife",
            "EnablePresetPackButton",
            true,
            "Add a '+ Tab Pack' / '+ Набор вкладок' button to the right-click menu of the Main tab to create a tactical starter set of tabs in one click without duplicating existing tabs.");

        CustomPresetPack = config.Bind(
            "QualityOfLife",
            "CustomPresetPack",
            "",
            "Optional comma-separated list of custom tab names created by the '+ Tab Pack' button. Leave empty for the default localized tactical pack (Weapons, Ammo, Medical, Food, Materials, Tools).");
    }
}
