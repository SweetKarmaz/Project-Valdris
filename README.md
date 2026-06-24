# Project Valdris

A first-person fantasy action-RPG built in **Unity 6 (6000.4.11f1)** using the
**High Definition Render Pipeline (HDRP)**.

> **Heads-up:** large, re-importable Asset Store packages are intentionally
> **not** committed (to stay within GitHub's Git LFS limits). After cloning you
> must import them before the project will compile — see
> [Setup after cloning](#setup-after-cloning).

---

## Requirements

- **Unity 6000.4.11f1** (HDRP).
- **Git** + **Git LFS** (`git lfs install` once per machine). Binary assets
  (textures, models, audio, fonts) are stored via LFS — see `.gitattributes`.

### Required Asset Store packages (NOT in the repo — import these)

These are referenced by the project but excluded from version control. Import
the same versions you own from the Asset Store before opening the project, or
the corresponding folders/GUIDs will be missing:

| Package | Imports to | Notes |
|---|---|---|
| **Synty – POLYGON Fantasy Hero Characters** | `Assets/Synty/` | The modular player & NPC characters (`Chr_Npc_Base`, etc.). |
| **Synty – POLYGON Fantasy Kingdom** | `Assets/Synty/` | Props + loot containers (chests/sacks/bags), `PolygonFantasyKingdom_Mat_*`. |
| **Synty – POLYGON Nature** | `Assets/Synty/` | Terrain layers, trees, plants, rocks, river/water for the outdoor world. |

### Included in the repo

- `Assets/_Project/` — all of the game's own code, prefabs, scenes, and data.
- `Assets/ThirdParty/` — Mixamo (X Bot) animation clips.
- `Assets/TextMesh Pro/`, `Assets/Settings/`, `ProjectSettings/`, `Packages/`.

---

## Setup after cloning

1. **Clone with LFS:**
   ```bash
   git clone https://github.com/SweetKarmaz/Project-Valdris.git
   cd Project-Valdris
   git lfs pull            # if your git wasn't LFS-enabled at clone time
   ```
2. **Import the Asset Store packages** listed above (Synty Fantasy Hero
   Characters, Synty Fantasy Kingdom, Synty Nature) into the matching folders.
3. **Open in Unity 6000.4.11f1.** Let it rebuild the `Library/` (ignored) on first open.
4. If Synty container/weapon materials show pink, run
   **Tools → Valdris → Loot → Materials → Fix Loot Container Materials (HDRP)** (and
   **Fix Missing Container Materials** if needed).

---

## Project layout (`Assets/_Project`)

- `Scripts/` — gameplay systems: `Core/` (save/scene-state, combat, damage,
  buffs, dialogue data, quests), `Systems/` (inventory, quests, skills, spells,
  XP/leveling, dialogue, input, quick-use, world flags), `Player/`, `NPC/`,
  `Items/`, `UI/`, `World/`, `Levels/` (scene managers, zones), `Events/`
  (scripted-event framework), `Camera/`, `Characters/`, `Enemies/`.
- `Editor/` — custom tools under **Tools → Valdris** (see below).
- `Scenes/`, `ScriptableObjects/`, `Prefabs/`, `Data/`.

### Key gameplay systems

- **Save / scene-state** — per-save snapshots; revisiting a scene restores its
  NPCs/loot/doors to that save's point in time.
- **Leveling & skills** — polynomial XP curve (cap 999), level-ups grant skill
  points (and an attribute point every 5th level); ranked skill tree with
  per-rank caps, weapon masteries, and effects like lifesteal / mana-cost /
  XP & gold find / loot rarity. Stats, regen, crit, and resistances on the
  Character tab.
- **Quests** — branching objectives (kill / talk / reach-zone / interact /
  deliver), accept & complete popups, auto-completing tutorial quests, rewards
  (XP/gold/items/spells/skills/attribute points).
- **Dialogue** — branching node-graph conversations with world-flag gating;
  forced and overheard modes.
- **Scripted events** — trigger (proximity / volume / door / talk / manual) +
  flag gate + a sequence of steps (lock player, dialogue, give quest, move/escort,
  doors, make NPC hostile, etc.); plus `GuardAlertZone`.
- **Zones** — editor-only trigger volumes for named areas, first-entry XP,
  quest triggers, flags, safe/rest zones, aura buffs, and flag-gated blockers.
- **World** — 60-minute day/night cycle with a saved Day/Hour clock, corruption
  meter, HDRP grass/rain.
- **Controls** — Input System with in-game key rebinding (Settings → Controls).

### Notable Editor tools (`Tools → Valdris`)

- **Loot** — Build Loot Containers, Build Armor Loot Prefabs, Rebuild Loot
  Registry, HDRP material fixers, Inventory Viewer (edit runtime inventory in play).
- **Build Default Skills** — generates/registers the ranked skill assets.
- **Build Keys Ring Item** — builds the "Keys" keyring token.
- **Scene** — Setup Scene For Save System, Create Default Scene State, and the
  Greyspire builders (blockout, dressing, bake NavMesh) plus the VaelCrossing
  world builders (terrain/water/foliage/grass/sky/etc.).

---

## Notes

- Source line endings are normalized via `.gitattributes`; Unity YAML assets are
  kept as text for diffing.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated
  `.csproj`/`.sln`, builds, the large Asset Store packs above, and local tool
  data (`.claude/`).
