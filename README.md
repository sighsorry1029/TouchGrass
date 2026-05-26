# TouchGrass

TouchGrass adds practical training tools for Valheim while discouraging unattended skill farming. It provides a training dummy meter, configurable dummy damage tests, dummy crowding limits, archery target tuning, per-skill gain/loss modifiers, and stationary skill fatigue.

![](https://i.ibb.co/HD6L1d27/Screenshot-2026-05-11-195741.png) <br>
![](https://i.ibb.co/ymJpCyvq/1-meter.gif) <br>
You can check dps, dph and damage taken through training dummy hud. <br>

![](https://i.ibb.co/Mky1kTd4/2-damagetype.gif) <br>
Training dummy's damage number and type and its health are configurable. <br>
So you can test different damage types and resistances. <br>
Check out this mod to make resistance additive. https://thunderstore.io/c/valheim/p/sighsorry/AdditiveDamageModifier/ <br>
![](https://i.ibb.co/zc7tSkV/4-crowded.png) <br>
Limit number of dummies that can be built within configured area. <br>
![](https://i.ibb.co/pBQdJk8J/3-discourage.gif) <br>
Maco skill farming is discouraged with smart stationary fatigue.

## Main Features

- Training dummy HUD for outgoing damage, incoming dummy damage, DPS, DPH, hit count, time, and recent skill XP.
- Configurable training dummy health, recipe, damage amount, and damage type.
- Use a training dummy to edit that dummy's damage amount and damage type in game.
- Optional crowding rule that blocks dense dummy placement with `Too crowded Bro!`.
- Stationary fatigue that gradually reduces repeated farmable skill gains inside the same X/Z radius.
- Archery target skill multiplier, recipe override, and an arrow/bolt-only skill gain gate.
- Per-skill gain and death-loss multipliers.

TouchGrass is not a hard anti-cheat system. It is meant to make common macro farms less efficient while keeping normal combat, movement, and testing usable.

## Training Dummy

Training dummy settings include:

- `Training Dummy Health`: max health for `piece_TrainingDummy`. Default: `2500`.
- `Training Dummy Crowding Radius`: X/Z radius for crowded placement checks. Default: `4`.
- `Training Dummy Crowding Max Count`: maximum existing dummies allowed in that radius. Default: `4`, so the 5th is blocked.
- `Training Dummy Recipe`: default `FineWood:5,BronzeNails:10,Ectoplasm:5`.
- `Training Dummy Damage Type`: default dummy damage type. Default: `Blunt`.
- `Training Dummy Damage`: default dummy damage amount. Default: `1`.
- `Training Meter Display`: `Detailed` or `Off`.
- `Training Meter Window Seconds`: rolling HUD window and HUD lifetime after dummy interaction. Default: `15`.

Use a training dummy to open the TouchGrass dummy settings window. The window changes only that dummy's damage amount and damage type. Health stays controlled by config.

## Training Meter

The training meter appears after interacting with a training dummy and uses a rolling time window.

`ToDummy` shows local outgoing hit attempts against training dummies:

- `Attempt`: total direct hit damage recorded in the window.
- `Status`: total status-style damage recorded separately when present.
- `DPH`: `Attempt / Hits`.
- `DPS`: `Attempt / Time`.
- `Hits`: number of recorded direct hits.
- `Time`: active window duration.

`FromDummy` shows the latest incoming dummy damage breakdown:

`Raw - Blocked - Resist - Armor = Final`

If the hit applies status damage, the HUD also shows `Status`. The breakdown is a practical readout for dummy testing, not a replacement for Valheim's internal combat log.

`Skill` shows the latest supported skill gain recorded during the dummy session. The percentage compares final XP gained against raw action base XP and includes Valheim skill gain rate, status/equipment raise-skill modifiers, TouchGrass per-skill gain rate, and stationary fatigue.

Drag the HUD with the left mouse button to move it. HUD width and scale are fixed to keep the config surface small.

## Skill Fatigue

Stationary fatigue applies to supported farmable skill gains when they keep chaining inside the same X/Z radius.

Defaults:

- Full efficiency for `120` seconds.
- Fade over the next `180` seconds.
- Minimum multiplier `10%`.
- Stationary radius `4m` on the X/Z plane.

The fatigue is global, not per skill. Rotating between weapons, movement skills, dodge, sneak, swim, or similar farmable actions in one place pushes the same multiplier down.

Waiting inside the same radius does not recover fatigue. The fatigue resets naturally when the player earns a supported skill XP tick outside the stationary radius.

When fatigue is reducing skill gain, TouchGrass can show a local status effect. `Detailed` shows the icon, current efficiency, and the compendium text `Touch the grass Bro!`; `Off` hides it.

## Archery Target

- `Archery Target Skill Multiplier`: multiplier for skill XP from `piece_ArcheryTarget`. Default: `1`.
- `Archery Target Arrow Bolt Skill Only`: when `On`, only arrow and bolt ammo can award archery target skill XP. Other projectiles can still score hits. Default: `On`.
- `Archery Target Recipe`: default `FineWood:4,LeatherScraps:10`.

## Per-Skill Modifiers

TouchGrass adds per-skill entries for:

- `Skill Gain Rate`
- `Skill Reduction Rate`

These are multiplicative modifiers. They do not replace Valheim's existing global modifiers or status/equipment modifiers.

Example:

`1.5 vanilla gain * 0.8 TouchGrass Swords gain * 0.5 fatigue = 0.6x final gain`

`Skill Reduction Rate` works the same way for death skill loss. `0` disables TouchGrass-applied loss for that skill, `1` keeps vanilla-scaled behavior, and `2` doubles it after vanilla scaling.

## Recipe Overrides

Recipe overrides use item prefab names:

`ItemPrefab:Amount,ItemPrefab:Amount`

Examples:

- Training dummy: `FineWood:5,BronzeNails:10,Ectoplasm:5`
- Archery target: `FineWood:4,LeatherScraps:10`

Leave a recipe empty to keep vanilla costs. Use `None`, `Free`, or `-` for no cost. Materials are always recovered when dismantling. Invalid recipe strings fall back to the vanilla recipe.

## Github
https://github.com/sighsorry1029/TouchGrass