# AutoCleanse

A CopilotBuddy plugin that removes Magic, Curse, Poison and Disease debuffs from you, and optionally your party or raid, using the cure spell your class knows at your level.

## What it does

- Cures you first. Two separate settings add party members (in a raid: your subgroup) and the whole raid, lowest health first.
- Picks the highest rank of the cure you have learned, so it works at every level.
- Cures: Paladin Cleanse / Purify, Priest Dispel Magic / Abolish or Cure Disease, Druid Remove Curse / Abolish or Cure Poison, Shaman Cleanse Spirit / Cure Toxins, Mage Remove Curse.
- Dwarf Stoneform (self only) when no class spell can remove the debuff.
- Warlock Felhunter Devour Magic for Magic debuffs.
- Never dispels Unstable Affliction (the dispel hurts you).
- Druids leave Cat, Bear, Travel or Aquatic form to cure only when out of combat by default. This is a setting.

Warriors, Rogues, Hunters and Death Knights have no cure spell, so the plugin does nothing for them unless they are Dwarves.

## Install

Copy `AutoCleanse.cs` and `AutoCleanseSettings.cs` into the `Plugins` folder of CopilotBuddy, then enable AutoCleanse in the Plugins tab. Settings are saved per character in `Settings\AutoCleanseSettings_<name>.xml`.

## Status

Compiles against CopilotBuddy 1.7.6.5. 
Tested as Paladin only so far. Issues / PRs appreciated. 
