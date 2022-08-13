# A QoL mod for Caves of Qud

## Quick Summary

This mod keeps track of merchant inventories in Caves of Qud, letting you check what you've seen without having to manually keep notes or take screenshots. The recorded info does respect existing (vanilla) game mechanics, such as unidentified artifacts; the goal is a quality-of-life improvement, while leaving game balance the same.

## Installing

Download the code, [install it](https://wiki.cavesofqud.com/wiki/Modding:Installing_a_mod#Manual_Download), and load a new character or existing save file. Then you're good to go!

## How it works

Every time you ask to trade with someone, the contents of their inventory will be saved for later searching, based on what your character sees on the trade screen. For instance, if you talk to all of the Six Day Stilt merchants and ask to trade, you can then search for "grenade" to check which merchants had identified grenades (or grenade data disks) for sale, as well as where they're located and when you last talked to them.

This isn't telepathy, by design; what your character sees is what the mod records. To update your in-game memory of a merchant's inventory (after they restock, or you gain techscanning, etc.), talk to them and ask to trade again!

Basic item searches, based on name, are done with the "Remember Items" activated ability. This lets you search for a case-insensitive substring, such as "slug" for lead slugs, or "item mod" for item mod data disks.

Additional filtering, such as min or max water cost, is available via the "Configure Item Search" activated ability. I'm aware the UI is a bit clunky; I'll probably fix it up after the next major Qud release, because I'm hoping there'll be some modding-friendly UI improvements.

Not yet tested with Snake Oiler (it hooks into the same game event, so remembered prices might be a little inaccurate), or Sifrah trading (which afaik changes the trade interaction, and I have no idea how that will interact).

## Savegame compatibility (tldr: yes)

This mod DOES work on existing save games :). I'm also planning to maintain savegame compatibility between mod versions, so that you can upgrade from one version to future ones (which I'll probably be doing on my own saves, after all!).

Note that if the newer version tracks more info than your existing version, you may need to re-interact with merchants to update their records with the new info.

## Feedback & Contributions

I'm happy to get feedback or contributions! Feel free to use the "[issues](https://github.com/HeatherSoron/QudMerchantMemory/issues)" tab here on Github for suggestions and bug reports, or ping me on the official Caves of Qud discord if you're there (I'm "Soron" on there).
