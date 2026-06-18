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

- `Scripts/` — gameplay systems: `Core/` (save, scene state, combat, damage,
  buffs), `Systems/` (inventory, quests, skills, spells), `Player/`, `NPC/`,
  `Items/`, `UI/`, `World/`, `Camera/`, `Enemies/`.
- `Editor/` — custom tools under **Tools → Valdris** (loot/armor builders,
  container builder, HDRP material fixers, inventory viewer, NPC presets).
- `Scenes/`, `ScriptableObjects/`, `Prefabs/`, `Data/`.

### Notable Editor tools (`Tools → Valdris`)

- **Build Loot Containers** — copy chest/sack/bag prefabs into
  `Prefabs/LootContainers/` and wire up the `LootContainer` component.
- **Fix Loot Container Materials (HDRP)** / **Fix Missing Container Materials**.
- **Build Armor Loot Prefabs**, **Rebuild Loot Registry**.
- **Inventory Viewer** — inspect/edit the player's runtime inventory in play mode.

---

## Notes

- Source line endings are normalized via `.gitattributes`; Unity YAML assets are
  kept as text for diffing.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated
  `.csproj`/`.sln`, builds, the large Asset Store packs above, and local tool
  data (`.claude/`).
