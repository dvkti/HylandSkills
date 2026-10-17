# Hyland Skills

A skill tree expansion for **Schedule I** that lives on your phone. Eight categories on an octagon, each with a Cyberpunk-style perk tree: prerequisites, point gates, trade-offs, active skills with clear timers, combos between related skills and a Major Mastery at the end of every tree. It is built to feel like part of the game: the app, the fonts, the sounds and the ability timers all use the game's own style.

> Built for Schedule I **0.4.7 (IL2CPP / main branch)** with MelonLoader 0.7.x and S1API (Forked) 3.2.x.

## How it works

- **Skill points.** You earn two points every time you go up a rank tier (Street Rat I → II, and so on). That is **100 points** by Kingpin I. Points are retroactive, so an existing save already has the points for its rank.
- **Branches cost 24 points.** That gets you four full branches (half of the eight) and a few spare points. You can't learn everything; your build is your playstyle.
- **Trees.** Each skill needs its parent skill. Deeper levels open at 3, 7, 12 and 17 points spent in that tree.
- **Combos.** Two skills marked with a gold link change how each other works when you own both (e.g. Overclock + Lab Focus = every batch goes up a tier while both run). Combo partners always sit on different paths of the tree, so getting one is a real choice.
- **Major Masteries.** You earn **mastery points** at **Bagman I**, **Shot Caller I**, **Underlord I** and **Kingpin I**. A completed branch (24/24) lets you choose **one of three** masteries for it. Only one per tree: picking it locks the other two until you reset.
- **Synergies.** Categories next to each other on the octagon unlock a free bonus once both have 8+ points.
- **Trade-offs.** Nodes with a red badge give you something and take something away. They create new ways to play: organic grower, premium dealer, night operator, front-business tycoon and more.
- **Respec.** The ↺ button in the app refunds everything, paid from your **bank account**: $5,000, then $15,000, $30,000, $50,000 and $75,000 for every reset after that. Read before you learn.

## Controls

- Open the **Skills** app on your phone.
- Active skills go into **4 slots**. The ⚡ button in the app opens the active skills menu: pick a slot, click any learned active skill to put it there, **Remove** to clear it, or click the key to rebind it (defaults **Z, X, B, N**).
- **Your kit is a choice.** A skill you put in a slot starts on cooldown (game-clock skills wait at most 12 in-game hours), so swapping actives for every situation doesn't pay. Moving a skill to another slot is free, and each slot shows when its skill is ready.
- Each equipped active shows as a card on screen: **READY**, **ACTIVE** with time left, or **COOLDOWN** with time left. Place the cards in any of 9 positions and change their size and opacity.

## Branches

| Branch | Playstyle |
|---|---|
| 🌿 Cultivation | Artisan grower: yield, water, seeds, harvest quality, organic or living-soil setups |
| ⚗️ Chemistry | Precision cook: station speed, quality rolls, ingredient refunds, Overclock (with burnout) |
| 🏢 Empire | Business owner: laundering, wages, shop prices, faster/free deliveries, the pawn shop, ATM limit, front businesses |
| 🌑 Shadows | Night operator: police attention, body searches, pursuit time, Police Scanner, Vanish |
| 💬 Hustle | Brand and clientele: haggling, samples, dealers, special crowds (Hippies, Bikers, Businessmen, Party Bus), Hype Text, Rush Hour |
| 👊 Muscle | Enforcer: damage taken and dealt, accuracy, Adrenaline, Intimidate, protection from the cartel |
| 🎲 Gambling | Casino: bigger payouts at blackjack, slots and Ride the Bus, loss insurance, laundered winnings, Peek at the dealer's card |
| 🏃 Mobility | Faster on foot, double jump, skateboard speed, push and jump, Sprint Burst, Board Boost |

## Installation

**Thunderstore Mod Manager / r2modman (recommended)**

1. Install **MelonLoader** and **S1API (Forked)** in your profile.
2. Download `GetToWorkTylerPls-HylandSkills-<version>.zip` from the [Releases](https://github.com/dvkti/HylandSkills/releases) page. Use that file, not GitHub's "Source code" zip.
3. In the mod manager: **Settings → Import local mod → Select file**, pick the zip and click **Import local mod**. Close the game first.
4. Start the game and open the **Skills** app on your phone.

If you installed an older version by copying `HylandSkills.dll` into `Mods` by hand, delete that copy first so the game doesn't load the mod twice.

**Manual install:** put `HylandSkills.dll` straight into the game's (or profile's) `Mods` folder. If you put it in a subfolder, MelonLoader only loads it when the subfolder also has the package's `manifest.json`.

Skill data is saved inside your game save (`Modded/Saveables/SkillSaveable`), so every save slot has its own tree.

## Compatibility

- Mainly for single player. In co-op, skills apply to the player who learned them; effects that run on the host (deal payments, stations) only take effect for the host.
- Don't run it together with another skill tree mod, because both would change the same systems.

## Credits

Made with MelonLoader, HarmonyX, Il2CppInterop and S1API. Skill icons are [Lucide](https://lucide.dev) icons (ISC license, see LICENSE-lucide.txt). Built with the help of AI (Claude).
