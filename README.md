# AutoCleanse

A CopilotBuddy plugin that removes Magic, Curse, Poison and Disease debuffs from your own character using the cure spell your class knows at your level.

## What it does

- Picks the highest rank of the cure you have learned, so it works at every level.
- Cures: Paladin Cleanse / Purify, Priest Dispel Magic / Abolish or Cure Disease, Druid Remove Curse / Abolish or Cure Poison, Shaman Cleanse Spirit / Cure Toxins, Mage Remove Curse.
- Dwarf Stoneform when no class spell can remove the debuff.
- Warlock Felhunter Devour Magic for Magic debuffs.
- Never dispels Unstable Affliction (the dispel hurts you).
- Druids leave Cat, Bear, Travel or Aquatic form to cure only when out of combat by default. This is a setting.

Warriors, Rogues, Hunters and Death Knights have no cure spell, so the plugin does nothing for them unless they are Dwarves.

## Install

Copy `AutoCleanse.cs` and `AutoCleanseSettings.cs` into the `Plugins` folder of CopilotBuddy, then enable AutoCleanse in the Plugins tab. Settings are saved per character in `Settings\AutoCleanseSettings_<name>.xml`.

## Status

Compiles against CopilotBuddy 1.7.6.5. Not yet tested in game.
