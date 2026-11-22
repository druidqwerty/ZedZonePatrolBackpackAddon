using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using ZedZone.Net;

namespace ZedZone.PatrolBackpackAddon;

internal static class PatrolBackpackPatches
{
    internal const int PatrolBackpackId = 513;
    internal const int TargetWidth = 20;
    internal const int TargetHeight = 100;

    [ThreadStatic]
    private static int _bypassPatrolOwnershipDepth;

    private static string _lastObservedLangBucket = "";
    private static string _lastPatrolDescLang = "";
    private static IntPtr _lastItemManagerPtr = IntPtr.Zero;
    private static bool _patrolRecipeRegistered;
    private static float _nextUpgradeScanTime;

    private static readonly Dictionary<string, string> BasePatrolDescriptions = new(StringComparer.Ordinal);

    internal static void InstallAll(Harmony harmony, ManualLogSource log)
    {
        const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // 1. Extend BackpackItem.Owns to include Tactical Patrol Backpack (513)
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BackpackItem.Owns",
            ZedBackpackBridge.BackpackItemType.GetMethod("Owns", AnyStatic),
            postfix: typeof(PatrolBackpackPatches).GetMethod(nameof(Owns_Postfix), AnyStatic));

        // 2. Keep vanilla sprite & icon for 513 (do not recolor it as 502)
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BackpackItem.SpriteId",
            ZedBackpackBridge.BackpackItemType.GetMethod("SpriteId", AnyStatic),
            prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(BypassPatrol_Prefix), AnyStatic),
            finalizer: typeof(PatrolBackpackPatches).GetMethod(nameof(BypassPatrol_Finalizer), AnyStatic));

        // 3. Keep 513 in vanilla loot pools
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BackpackItem.RemoveFromNativeLootPools",
            ZedBackpackBridge.BackpackItemType.GetMethod("RemoveFromNativeLootPools", AnyStatic),
            prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(BypassPatrol_Prefix), AnyStatic),
            finalizer: typeof(PatrolBackpackPatches).GetMethod(nameof(BypassPatrol_Finalizer), AnyStatic));

        // 4. Upgrade ItemAttr_Backpack(513) to 20x100 and update localized descriptions
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BackpackItem.EnsureRegistered",
            ZedBackpackBridge.BackpackItemType.GetMethod("EnsureRegistered", AnyStatic),
            postfix: typeof(PatrolBackpackPatches).GetMethod(nameof(EnsureRegistered_Postfix), AnyStatic));

        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BackpackItem.RefreshNames",
            ZedBackpackBridge.BackpackItemType.GetMethod("RefreshNames", AnyStatic),
            postfix: typeof(PatrolBackpackPatches).GetMethod(nameof(RefreshNames_Postfix), AnyStatic));

        // 5. Optional suppression of free 925722 gift
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BackpackItem.GiveOnce",
            ZedBackpackBridge.BackpackItemType.GetMethod("GiveOnce", AnyStatic),
            prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(GiveOnce_Prefix), AnyStatic));

        // 6. Upgrade existing 513 container instances before Bags.SeenBagItem / Protection.Update
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "BasicItemUI.PrepareBackpackInventory",
            AccessTools.Method(typeof(BasicItemUI), "PrepareBackpackInventory", new[] { typeof(ItemData) }),
            prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(PrepareBackpackInventory_Prefix), AnyStatic));

        if (ZedBackpackBridge.ProtectionType != null)
        {
            SafeHarmonyPatcher.TryPatch(
                harmony, log, "Protection.Update",
                ZedBackpackBridge.ProtectionType.GetMethod("Update", AnyStatic),
                prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(ProtectionUpdate_Prefix), AnyStatic));

            SafeHarmonyPatcher.TryPatch(
                harmony, log, "Protection.Notify",
                ZedBackpackBridge.ProtectionType.GetMethod("Notify", AnyStatic),
                prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(ProtectionNotify_Prefix), AnyStatic));
        }

        // 7. Full Russian localization hook on ZedBackpack.Localized.T
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "Localized.T",
            ZedBackpackBridge.LocalizedType.GetMethod("T", AnyStatic),
            postfix: typeof(PatrolBackpackPatches).GetMethod(nameof(LocalizedT_Postfix), AnyStatic));

        // 8. TabUI language tracking, Cyrillic glyph preload, PageUp/PageDown hotkeys, Starter Tab Pack button
        SafeHarmonyPatcher.TryPatch(
            harmony, log, "TabUI.Tick",
            ZedBackpackBridge.TabUIType.GetMethod("Tick", AnyStatic),
            prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(TabUITick_Prefix), AnyStatic));

        SafeHarmonyPatcher.TryPatch(
            harmony, log, "TabUI.FindNativeFont",
            ZedBackpackBridge.TabUIType.GetMethod("FindNativeFont", AnyStatic),
            postfix: typeof(PatrolBackpackPatches).GetMethod(nameof(FindNativeFont_Postfix), AnyStatic));

        SafeHarmonyPatcher.TryPatch(
            harmony, log, "TabUI.BuildOriginalPopup",
            ZedBackpackBridge.TabUIType.GetMethod("BuildOriginalPopup", AnyStatic),
            postfix: typeof(PatrolBackpackPatches).GetMethod(nameof(BuildOriginalPopup_Postfix), AnyStatic));

        SafeHarmonyPatcher.TryPatch(
            harmony, log, "TabUI.PlacePopup",
            ZedBackpackBridge.TabUIType.GetMethod("PlacePopup", AnyStatic),
            prefix: typeof(PatrolBackpackPatches).GetMethod(nameof(PlacePopup_Prefix), AnyStatic));
    }

    // --- Ownership & vanilla preservation ---

    private static void Owns_Postfix(int itemId, ref bool __result)
    {
        if (__result) return;
        if (itemId != PatrolBackpackId) return;
        if (_bypassPatrolOwnershipDepth > 0) return;
        if (AddonConfig.EnablePatrolBackpack == null || !AddonConfig.EnablePatrolBackpack.Value) return;
        __result = true;
    }

    private static void BypassPatrol_Prefix()
    {
        _bypassPatrolOwnershipDepth++;
    }

    private static Exception BypassPatrol_Finalizer(Exception __exception)
    {
        if (_bypassPatrolOwnershipDepth > 0)
            _bypassPatrolOwnershipDepth--;
        return __exception;
    }

    private static bool GiveOnce_Prefix()
    {
        if (AddonConfig.SuppressOriginalFreeGift != null && AddonConfig.SuppressOriginalFreeGift.Value)
            return false;
        return true;
    }

    // --- Patrol backpack definition & live save upgrade ---

    private static void EnsureRegistered_Postfix()
    {
        try
        {
            ApplyPatrolDefinition();
            ApplyRussianItemNamesIfNeeded(force: false);
        }
        catch (Exception ex)
        {
            PatrolBackpackAddonPlugin.LogSource?.LogWarning($"[EnsureRegistered_Postfix] {ex.Message}");
        }
    }

    private static void RefreshNames_Postfix()
    {
        try
        {
            ApplyRussianItemNamesIfNeeded(force: false);
        }
        catch (Exception ex)
        {
            PatrolBackpackAddonPlugin.LogSource?.LogWarning($"[RefreshNames_Postfix] {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ApplyPatrolDefinition()
    {
        if (AddonConfig.EnablePatrolBackpack == null || !AddonConfig.EnablePatrolBackpack.Value)
            return;

        var im = ItemManager.instance;
        if (im == null || im.itemAttrDic == null || !im.itemAttrDic.ContainsKey(PatrolBackpackId))
            return;

        var rawAttr = im.GetItemAttrById(PatrolBackpackId);
        if (rawAttr == null) return;
        var patrolAttr = rawAttr.TryCast<ItemAttr_Backpack>();
        if (patrolAttr == null) return;

        bool newManager = ((Il2CppObjectBase)im).Pointer != _lastItemManagerPtr;
        if (newManager)
        {
            _lastItemManagerPtr = ((Il2CppObjectBase)im).Pointer;
            _patrolRecipeRegistered = false;
            _lastPatrolDescLang = "";
            BasePatrolDescriptions.Clear();
        }

        if (patrolAttr.inventorySize.x != TargetWidth || patrolAttr.inventorySize.y != TargetHeight)
        {
            patrolAttr.inventorySize = new Vector2Int(TargetWidth, TargetHeight);
            PatrolBackpackAddonPlugin.LogSource?.LogInfo(
                $"Upgraded Tactical Patrol Backpack (ID {PatrolBackpackId}) capacity to {TargetWidth}x{TargetHeight}.");
        }

        if (AddonConfig.CraftablePatrolBackpack != null && AddonConfig.CraftablePatrolBackpack.Value && !_patrolRecipeRegistered)
        {
            TryRegisterPatrolRecipe(im, patrolAttr);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TryRegisterPatrolRecipe(ItemManager im, ItemAttr_Backpack patrolAttr)
    {
        try
        {
            if (im.allRecipeList == null) return;
            var existing = patrolAttr.recipeDataAry;
            if (existing != null && existing.Length > 0)
            {
                _patrolRecipeRegistered = true;
                return;
            }

            var recipe = new RecipeData
            {
                itemId = PatrolBackpackId,
                outputItemNumber = 1,
                craftPlatform = (CraftPlatform)0,
                craftTime = 1f / 96f,
                toolType = (ToolType)0,
                iqReqiared = 0f,
                craftReqiared = 0f,
                craftDifficultyLevel = 0,
                fatigueCost = 0f
            };
            recipe.recipeItems = new Il2CppSystem.Collections.Generic.List<RecipeItemData>();
            recipe.recipeItems.Add(new RecipeItemData { itemId = 30, itemNumber = 20f });
            patrolAttr.recipeDataAry = new Il2CppReferenceArray<RecipeData>(new[] { recipe });
            im.RegisterModRecipes(patrolAttr);
            _patrolRecipeRegistered = true;
            PatrolBackpackAddonPlugin.LogSource?.LogInfo("Registered optional handcraft recipe for Tactical Patrol Backpack (20x Cloth).");
        }
        catch (Exception ex)
        {
            PatrolBackpackAddonPlugin.LogSource?.LogWarning($"[TryRegisterPatrolRecipe] {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ApplyRussianItemNamesIfNeeded(bool force)
    {
        string bucket = RussianLocalization.GetLanguageBucket();
        if (!force && bucket == _lastPatrolDescLang)
            return;

        var im = ItemManager.instance;
        if (im == null || im.itemAttrDic == null)
            return;

        _lastPatrolDescLang = bucket;

        // Translate Expedition Backpack 925722 / 925721 if Russian
        if (AddonConfig.EnableRussianLocalization != null && AddonConfig.EnableRussianLocalization.Value && bucket == "ru")
        {
            try
            {
                if (ZedBackpackBridge.BackpackItemAttrField?.GetValue(null) is ItemAttr_Backpack expAttr && expAttr != null)
                    RussianLocalization.ApplyRussianToExpeditionAttr(expAttr, legacy: false);
                if (ZedBackpackBridge.BackpackItemLegacyAttrField?.GetValue(null) is ItemAttr_Backpack legAttr && legAttr != null)
                    RussianLocalization.ApplyRussianToExpeditionAttr(legAttr, legacy: true);
            }
            catch { }
        }

        // Update Tactical Patrol Backpack (513) description cleanly without duplicating suffix
        if (AddonConfig.EnablePatrolBackpack != null && AddonConfig.EnablePatrolBackpack.Value && im.itemAttrDic.ContainsKey(PatrolBackpackId))
        {
            try
            {
                var patrolAttr = im.GetItemAttrById(PatrolBackpackId)?.TryCast<ItemAttr_Backpack>();
                if (patrolAttr != null)
                {
                    string addonSuffix = RussianLocalization.GetPatrolAddonDescriptionLine();
                    if (!BasePatrolDescriptions.TryGetValue(bucket, out string baseDesc))
                    {
                        string current = patrolAttr.itemDescription_WithLanguage ?? patrolAttr.runtimeItemDescription ?? patrolAttr.itemDescription ?? "";
                        int idx = current.IndexOf("\n[", StringComparison.Ordinal);
                        if (idx >= 0) current = current.Substring(0, idx);
                        baseDesc = current;
                        BasePatrolDescriptions[bucket] = baseDesc;
                    }

                    string full = baseDesc + addonSuffix;
                    patrolAttr.itemDescription = full;
                    patrolAttr.itemDescription_WithLanguage = full;
                    patrolAttr.runtimeItemDescription = full;
                    patrolAttr.cachedDescLang = "";
                }
            }
            catch { }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PrepareBackpackInventory_Prefix(ItemData __0)
    {
        try
        {
            if (AddonConfig.EnablePatrolBackpack == null || !AddonConfig.EnablePatrolBackpack.Value)
                return;
            if (__0 == null || __0.itemId != PatrolBackpackId)
                return;
            EnsureInventoryUpgraded(__0.inventoryData);
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProtectionUpdate_Prefix()
    {
        try
        {
            if (AddonConfig.EnablePatrolBackpack == null || !AddonConfig.EnablePatrolBackpack.Value)
                return;

            var gc = GameController.instance;
            var player = gc?.gameData?.playerData;
            if (player == null || player.inventoryData == null)
                return;

            var worn = player.characterEquipmentData?.backpackItemData;
            if (worn != null && worn.itemId == PatrolBackpackId)
            {
                EnsureInventoryUpgraded(player.inventoryData);
                if (worn.inventoryData != null)
                    EnsureInventoryUpgraded(worn.inventoryData);
            }

            if (Time.unscaledTime >= _nextUpgradeScanTime)
            {
                _nextUpgradeScanTime = Time.unscaledTime + 1.5f;
                ScanCarriedPatrolBags(player);
            }
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ScanCarriedPatrolBags(CharacterData player)
    {
        var carried = player.GetCarriedInventoryList();
        if (carried == null) return;
        for (int i = 0; i < carried.Count; i++)
        {
            var inv = carried[i];
            if (inv?.itemList == null) continue;
            for (int j = 0; j < inv.itemList.Count; j++)
            {
                var item = inv.itemList[j];
                if (item != null && item.itemId == PatrolBackpackId && item.inventoryData != null)
                {
                    EnsureInventoryUpgraded(item.inventoryData);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool EnsureInventoryUpgraded(InventoryData inv)
    {
        if (inv == null) return false;
        // Never shrink a bag that already has multiple 100-row tab bands
        if (inv.inventorySizeX == TargetWidth && inv.inventorySizeY >= TargetHeight && (inv.inventorySizeY % TargetHeight == 0))
            return false;

        int newY = inv.inventorySizeY < TargetHeight
            ? TargetHeight
            : ((inv.inventorySizeY + TargetHeight - 1) / TargetHeight) * TargetHeight;

        inv.inventorySize = new Vector2Int(TargetWidth, newY);
        inv.SetOccupyDataDirty();
        inv.weightMemoDirty = true;
        try
        {
            inv.onChangeAction?.Invoke(inv);
        }
        catch { }
        return true;
    }

    // --- Russian Localization hooks ---

    private static void LocalizedT_Postfix(string chinese, string english, ref string __result)
    {
        try
        {
            if (AddonConfig.EnableRussianLocalization == null || !AddonConfig.EnableRussianLocalization.Value)
                return;
            if (!RussianLocalization.IsRussian())
                return;
            __result = RussianLocalization.Translate(chinese, english);
        }
        catch { }
    }

    private static void ProtectionNotify_Prefix(string chinese, ref string english)
    {
        try
        {
            if (AddonConfig.EnableRussianLocalization == null || !AddonConfig.EnableRussianLocalization.Value)
                return;
            if (!RussianLocalization.IsRussian())
                return;
            english = RussianLocalization.Translate(chinese, english);
        }
        catch { }
    }

    // --- TabUI enhancements: live language switch, Cyrillic preload, PageUp/PageDown, Starter Tab Pack ---

    private static void FindNativeFont_Postfix(ref Font __result)
    {
        if (__result != null)
            RussianLocalization.PreloadCyrillicGlyphs(__result);
    }

    private static void TabUITick_Prefix()
    {
        try
        {
            string bucket = RussianLocalization.GetLanguageBucket();
            if (bucket != _lastObservedLangBucket)
            {
                _lastObservedLangBucket = bucket;
                ZedBackpackBridge.ResetTabUILanguageCache();
                ApplyRussianItemNamesIfNeeded(force: true);
            }

            if (AddonConfig.EnableQuickTabHotkeys != null && AddonConfig.EnableQuickTabHotkeys.Value)
            {
                HandleQuickTabHotkeys();
            }
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HandleQuickTabHotkeys()
    {
        bool prev = Input.GetKeyDown(KeyCode.PageUp);
        bool next = Input.GetKeyDown(KeyCode.PageDown);
        if (!prev && !next) return;

        if (ZedBackpackBridge.IsSearchOrTabTyping() || ZedBackpackBridge.HasNativeDrag())
            return;

        try
        {
            if (NetChatBar.isTypingChat)
                return;
        }
        catch { }

        var hostsObj = ZedBackpackBridge.TabUIHostsField?.GetValue(null) as IDictionary;
        if (hostsObj == null || hostsObj.Count == 0)
            return;

        // Prefer PlayerInventoryPanel if active, otherwise first active host
        object targetHost = null;
        var playerPanel = PlayerInventoryPanel._instance;
        IntPtr playerPtr = playerPanel != null ? ((Il2CppObjectBase)playerPanel).Pointer : IntPtr.Zero;

        foreach (DictionaryEntry entry in hostsObj)
        {
            object host = entry.Value;
            if (host == null) continue;
            var strip = ZedBackpackBridge.HostStripRootField?.GetValue(host) as RectTransform;
            if (strip == null || !strip.gameObject.activeInHierarchy) continue;

            if (entry.Key is IntPtr ptr && ptr == playerPtr)
            {
                targetHost = host;
                break;
            }
            targetHost ??= host;
        }

        if (targetHost == null) return;
        SwitchHostTab(targetHost, prev ? -1 : 1);
    }

    internal static bool SwitchHostTab(object host, int delta)
    {
        if (host == null || ZedBackpackBridge.BagsCurrentMethod == null || ZedBackpackBridge.BagsSwitchToMethod == null)
            return false;

        object bag = ZedBackpackBridge.HostBagField?.GetValue(host);
        var panel = ZedBackpackBridge.HostPanelField?.GetValue(host) as InventoryPanel;
        if (bag == null || panel == null) return false;

        object meta = ZedBackpackBridge.BagMetaField?.GetValue(bag);
        if (meta == null) return false;

        var tabs = ZedBackpackBridge.MetaTabsProp?.GetValue(meta) as IList;
        if (tabs == null || tabs.Count <= 1) return false;

        string currentId = ZedBackpackBridge.BagsCurrentMethod.Invoke(null, new[] { bag }) as string ?? "";
        int currentIndex = 0;
        for (int i = 0; i < tabs.Count; i++)
        {
            string id = ZedBackpackBridge.TabIdProp?.GetValue(tabs[i]) as string ?? "";
            if (id == currentId)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + delta) % tabs.Count;
        if (nextIndex < 0) nextIndex += tabs.Count;
        string nextId = ZedBackpackBridge.TabIdProp?.GetValue(tabs[nextIndex]) as string;
        if (string.IsNullOrEmpty(nextId) || nextId == currentId) return false;

        ZedBackpackBridge.BagsSwitchToMethod.Invoke(null, new[] { bag, nextId });
        try
        {
            ZedBackpackBridge.VirtualGridSetScrollMethod?.Invoke(null, new object[] { panel, 1f, false });
        }
        catch { }

        return true;
    }

    private static void PlacePopup_Prefix(object h, RectTransform popup, ref float hgt)
    {
        try
        {
            if (AddonConfig.EnablePresetPackButton == null || !AddonConfig.EnablePresetPackButton.Value)
                return;
            if (h == null || popup == null) return;
            var origPopup = ZedBackpackBridge.HostOriginalPopupField?.GetValue(h) as RectTransform;
            if (origPopup != null && origPopup == popup && hgt < 96f)
            {
                hgt = 98f;
            }
        }
        catch { }
    }

    private static void BuildOriginalPopup_Postfix(object h)
    {
        try
        {
            if (AddonConfig.EnablePresetPackButton == null || !AddonConfig.EnablePresetPackButton.Value)
                return;
            if (h == null || ZedBackpackBridge.TabUIMakeButtonMethod == null || ZedBackpackBridge.TabUIPlaceTopLeftMethod == null)
                return;

            var popup = ZedBackpackBridge.HostOriginalPopupField?.GetValue(h) as RectTransform;
            var statusLabel = ZedBackpackBridge.HostOriginalStatusLabelField?.GetValue(h) as Text;
            var font = ZedBackpackBridge.HostFontField?.GetValue(h) as Font;
            if (popup == null || font == null) return;

            RussianLocalization.PreloadCyrillicGlyphs(font);

            // Resize popup frame to 98px height
            ZedBackpackBridge.TabUIPlaceTopLeftMethod.Invoke(null, new object[] { popup, 62f, 0f, 166f, 98f });

            // Move status label down below the new button
            if (statusLabel != null)
            {
                ZedBackpackBridge.TabUIPlaceTopLeftMethod.Invoke(null, new object[] { statusLabel.rectTransform, 6f, -76f, 154f, 16f });
            }

            Action onClick = () =>
            {
                try
                {
                    object bag = ZedBackpackBridge.HostBagField?.GetValue(h);
                    int created = CreateStarterTabPack(bag);
                    if (statusLabel != null)
                        statusLabel.text = RussianLocalization.GetPresetPackResultMessage(created);
                }
                catch (Exception ex)
                {
                    PatrolBackpackAddonPlugin.LogSource?.LogWarning($"[CreateStarterTabPack] {ex.Message}");
                }
            };

            object[] args = new object[]
            {
                "CreateStarterTabPack",
                (Transform)popup,
                font,
                RussianLocalization.GetPresetPackButtonLabel(),
                9,
                onClick,
                null
            };

            if (ZedBackpackBridge.TabUIMakeButtonMethod.Invoke(null, args) is Button btn && btn != null)
            {
                var rt = btn.GetComponent<RectTransform>();
                ZedBackpackBridge.TabUIPlaceTopLeftMethod.Invoke(null, new object[] { rt, 5f, -50f, 156f, 22f });
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = new Color(0.32f, 0.52f, 0.66f, 0.98f);
                if (args[6] is Text lbl && lbl != null)
                    lbl.color = new Color(0.76f, 0.92f, 1f, 1f);
            }
        }
        catch (Exception ex)
        {
            PatrolBackpackAddonPlugin.LogSource?.LogWarning($"[BuildOriginalPopup_Postfix] {ex.Message}");
        }
    }

    internal static int CreateStarterTabPack(object bag)
    {
        if (bag == null) return 0;
        object meta = ZedBackpackBridge.BagMetaField?.GetValue(bag);
        var tabs = meta != null ? ZedBackpackBridge.MetaTabsProp?.GetValue(meta) as IList : null;

        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tabs != null)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                string n = ZedBackpackBridge.TabNameProp?.GetValue(tabs[i]) as string;
                if (!string.IsNullOrWhiteSpace(n))
                    existingNames.Add(n.Trim());
            }
        }

        int created = 0;
        foreach (string desired in RussianLocalization.GetDefaultPresetPackNames())
        {
            if (existingNames.Contains(desired))
                continue;
            if (ZedBackpackBridge.TryCreateTab(bag, desired, out _))
            {
                existingNames.Add(desired);
                created++;
            }
        }
        return created;
    }
}
