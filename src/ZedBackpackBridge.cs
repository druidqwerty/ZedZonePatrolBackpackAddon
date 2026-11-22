using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ZedZone.PatrolBackpackAddon;

internal static class ZedBackpackBridge
{
    internal static bool Resolved { get; private set; }

    internal static Type PluginType;
    internal static Type BackpackItemType;
    internal static Type LocalizedType;
    internal static Type TabUIType;
    internal static Type TabUIHostType;
    internal static Type BagsType;
    internal static Type BagsBagType;
    internal static Type BagsMetaType;
    internal static Type BagsTabType;
    internal static Type BackpackSearchType;
    internal static Type ProtectionType;
    internal static Type VirtualGridType;

    // Key members cached for fast runtime use
    internal static FieldInfo BackpackItemAttrField;
    internal static FieldInfo BackpackItemLegacyAttrField;
    internal static FieldInfo TabUILastLanguageField;
    internal static FieldInfo TabUIHostsField;
    internal static FieldInfo HostPanelField;
    internal static FieldInfo HostBagField;
    internal static FieldInfo HostFontField;
    internal static FieldInfo HostOriginalPopupField;
    internal static FieldInfo HostOriginalStatusLabelField;
    internal static FieldInfo HostStripRootField;
    internal static FieldInfo BagMetaField;
    internal static PropertyInfo MetaTabsProp;
    internal static PropertyInfo TabIdProp;
    internal static PropertyInfo TabNameProp;
    internal static PropertyInfo SearchIsTypingProp;
    internal static FieldInfo SearchStatesField;
    internal static FieldInfo StateLastLanguageField;
    internal static FieldInfo StateInputField;

    internal static MethodInfo BagsCurrentMethod;
    internal static MethodInfo BagsSwitchToMethod;
    internal static MethodInfo BagsCreateTabMethod;
    internal static MethodInfo VirtualGridSetScrollMethod;
    internal static PropertyInfo VirtualGridHasNativeDragProp;
    internal static MethodInfo TabUIMakeButtonMethod;
    internal static MethodInfo TabUIPlaceTopLeftMethod;

    private static Assembly FindZedBackpackAssembly()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string name = asm.GetName().Name;
            if (string.Equals(name, "ZedBackpack", StringComparison.OrdinalIgnoreCase))
                return asm;
        }
        return null;
    }

    internal static bool Resolve(ManualLogSource log)
    {
        var asm = FindZedBackpackAssembly();
        if (asm == null)
        {
            log?.LogError("ZedBackpack assembly (Workshop 3837676256) not found. Install and enable Expedition Fresh Backpack first.");
            Resolved = false;
            return false;
        }

        PluginType = asm.GetType("ZedBackpack.Plugin");
        BackpackItemType = asm.GetType("ZedBackpack.BackpackItem");
        LocalizedType = asm.GetType("ZedBackpack.Localized");
        TabUIType = asm.GetType("ZedBackpack.TabUI");
        BagsType = asm.GetType("ZedBackpack.Bags");
        BackpackSearchType = asm.GetType("ZedBackpack.BackpackSearch");
        ProtectionType = asm.GetType("ZedBackpack.Protection");
        VirtualGridType = asm.GetType("ZedBackpack.VirtualGrid");

        if (BackpackItemType == null || LocalizedType == null || TabUIType == null || BagsType == null)
        {
            log?.LogError("ZedBackpack (Workshop 3837676256) types not found. Install and enable Expedition Fresh Backpack first.");
            Resolved = false;
            return false;
        }

        const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags AnyInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        TabUIHostType = TabUIType.GetNestedType("Host", BindingFlags.Public | BindingFlags.NonPublic);
        BagsBagType = BagsType.GetNestedType("Bag", BindingFlags.Public | BindingFlags.NonPublic);
        BagsMetaType = BagsType.GetNestedType("Meta", BindingFlags.Public | BindingFlags.NonPublic);
        BagsTabType = BagsType.GetNestedType("Tab", BindingFlags.Public | BindingFlags.NonPublic);

        BackpackItemAttrField = BackpackItemType.GetField("Attr", AnyStatic);
        BackpackItemLegacyAttrField = BackpackItemType.GetField("LegacyAttr", AnyStatic);
        TabUILastLanguageField = TabUIType.GetField("lastLanguage", AnyStatic);
        TabUIHostsField = TabUIType.GetField("hosts", AnyStatic);

        if (TabUIHostType != null)
        {
            HostPanelField = TabUIHostType.GetField("Panel", AnyInst);
            HostBagField = TabUIHostType.GetField("Bag", AnyInst);
            HostFontField = TabUIHostType.GetField("Font", AnyInst);
            HostOriginalPopupField = TabUIHostType.GetField("OriginalPopup", AnyInst);
            HostOriginalStatusLabelField = TabUIHostType.GetField("OriginalStatusLabel", AnyInst);
            HostStripRootField = TabUIHostType.GetField("StripRoot", AnyInst);
        }

        if (BagsBagType != null)
            BagMetaField = BagsBagType.GetField("Meta", AnyInst);
        if (BagsMetaType != null)
            MetaTabsProp = BagsMetaType.GetProperty("Tabs", AnyInst);
        if (BagsTabType != null)
        {
            TabIdProp = BagsTabType.GetProperty("Id", AnyInst);
            TabNameProp = BagsTabType.GetProperty("Name", AnyInst);
        }

        BagsCurrentMethod = BagsType.GetMethod("Current", AnyStatic);
        BagsSwitchToMethod = BagsType.GetMethod("SwitchTo", AnyStatic);
        BagsCreateTabMethod = BagsType.GetMethod("CreateTab", AnyStatic);

        if (VirtualGridType != null)
        {
            VirtualGridSetScrollMethod = VirtualGridType.GetMethod("SetPanelScrollNormalized", AnyStatic);
            VirtualGridHasNativeDragProp = VirtualGridType.GetProperty("HasNativeDrag", AnyStatic);
        }

        if (BackpackSearchType != null)
        {
            SearchIsTypingProp = BackpackSearchType.GetProperty("IsTyping", AnyStatic);
            SearchStatesField = BackpackSearchType.GetField("States", AnyStatic);
            var stateType = BackpackSearchType.GetNestedType("State", BindingFlags.Public | BindingFlags.NonPublic);
            if (stateType != null)
            {
                StateLastLanguageField = stateType.GetField("LastLanguage", AnyInst);
                StateInputField = stateType.GetField("Input", AnyInst);
            }
        }

        TabUIMakeButtonMethod = TabUIType.GetMethod("MakeButton", AnyStatic);
        TabUIPlaceTopLeftMethod = TabUIType.GetMethod("PlaceTopLeft", AnyStatic);

        Resolved = true;
        return true;
    }

    internal static bool IsSearchOrTabTyping()
    {
        try
        {
            if (SearchIsTypingProp != null && SearchIsTypingProp.GetValue(null) is bool b && b)
                return true;
        }
        catch { }
        return false;
    }

    internal static bool HasNativeDrag()
    {
        try
        {
            if (VirtualGridHasNativeDragProp != null && VirtualGridHasNativeDragProp.GetValue(null) is bool b)
                return b;
        }
        catch { }
        return false;
    }

    internal static void ResetTabUILanguageCache()
    {
        try
        {
            TabUILastLanguageField?.SetValue(null, "");
        }
        catch { }

        try
        {
            if (SearchStatesField?.GetValue(null) is IDictionary dict)
            {
                foreach (object state in dict.Values)
                {
                    if (state == null) continue;
                    StateLastLanguageField?.SetValue(state, "");
                    if (StateInputField?.GetValue(state) is InputField input && input != null && input.placeholder != null)
                    {
                        var txt = input.placeholder.TryCast<Text>();
                        if (txt != null)
                            txt.text = RussianLocalization.GetSearchPlaceholder();
                    }
                }
            }
        }
        catch { }
    }

    internal static bool TryCreateTab(object bag, string name, out string error)
    {
        error = "";
        if (bag == null || BagsCreateTabMethod == null) return false;
        try
        {
            object[] args = new object[] { bag, name, null, null };
            bool ok = (bool)BagsCreateTabMethod.Invoke(null, args);
            error = args[3] as string ?? "";
            return ok;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
