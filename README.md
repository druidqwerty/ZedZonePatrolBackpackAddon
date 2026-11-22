# ZED ZONE: Tactical Patrol Backpack Addon (v1.0.0)

**Specially made by request for user: `6VaN4!K96`**

[![Support development](docs/banner.png)](https://boosty.to/lrgpp)

Addon for **[Expedition Fresh Backpack (Workshop 3837676256)](https://steamcommunity.com/sharedfiles/filedetails/?id=3837676256)** that brings its full 20×100 independent tab system, smart auto-sorting, zero carried weight, food freshness & hot meal preservation, cross-tab search, and container mode directly to the vanilla **Tactical Patrol Backpack (`itemId = 513`)** — plus a **complete 100% Russian translation** and extra quality-of-life features.

## Features

- **Tactical Patrol Backpack (`ID 513`) Support:**
  - Full `20×100` (2,000 slots per tab) independent tabs, smart auto-sorting rules, zero carried weight while equipped, food freshness & hot meal preservation, cross-tab search, and container mode (ground / chests / shelves).
  - Preserves the vanilla Tactical Patrol Backpack icon, worn model, and vanilla world loot pools.
  - **Safe Existing-Save Upgrade:** existing Tactical Patrol Backpacks (equipped or stored in containers) automatically expand from vanilla size to `20×100` without losing or shifting any items.
  - Original Expedition Fresh Backpack (`925722`/`925721`) keeps working if you already have one; automatic free gifting of `925722` is disabled by default (`SuppressOriginalFreeGift = true`).
- **Complete Russian Localization (`RU`):**
  - Translates 100% of `ZedBackpack` UI elements when the game language is Russian: tabs, all 8 short & full tab presets, search bar placeholder, right-click menus, dynamic status/rule counters, recovery notifications, in-game guide (`? Справка`), and item descriptions.
  - Preloads Cyrillic glyphs into the UI font atlas and rebuilds UI cleanly on live language switches (`RU / EN / ZH`).
- **Extra Quality-of-Life Features:**
  - **`+ Starter Tab Pack` button** in the right-click menu of the `Main` tab — creates a starter tactical tab set (`Weapons`, `Ammo`, `Medical`, `Food`, `Materials`, `Tools` — localized) in one click without duplicating existing tabs. Customizable via `CustomPresetPack` in config.
  - **`PageUp` / `PageDown` quick tab switching** while the backpack inventory is open (automatically disabled while typing in chat, search, rename, or dragging items).
  - **Configurable (`BepInEx/config/com.druid.zedzone.patrolbackpackaddon.cfg`):** toggle patrol support, Russian localization, free-gift suppression, quick tab hotkeys, starter pack button, custom tab pack list, or optional handcraft recipe for `513`.

## Requirements & Installation

1. Install third-party **[ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)** (run its `install.cmd` once).
2. Subscribe to / install the base mod **[Expedition Fresh Backpack (3837676256)](https://steamcommunity.com/sharedfiles/filedetails/?id=3837676256)**.
3. Subscribe to this addon in Steam Workshop (or extract `PatrolBackpackAddon/` from the GitHub Release ZIP into `ZED ZONE/Mods/`).

## Verification

- Verified on **ZED ZONE Patch 1.0.20** in a live graphical `Gaming` world (`25/25` checks passed, `14/14` Harmony patches applied, `0` errors, `8,386/8,386` save files unchanged).

---

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
