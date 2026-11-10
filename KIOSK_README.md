# Bentley's Restaurant Kiosk (3.14.0): what is in this pack

## NOT built, NOT tested
Nothing here has been compiled or run. The author's environment had no .NET SDK, no NuGet access and no Stardew Valley or SMAPI assemblies. The C# is written against the Stardew 1.6 / SMAPI 4.1 API from memory and may need small fixes when you first compile. Do not install the mod without the compiled DLL: the Content part now REQUIRES the Kiosk part, so a missing DLL stops the whole mod from loading.

## How the third DLL is loaded
SMAPI loads exactly one EntryDll per manifest.json, and does not load other DLLs in a folder. Your existing `Code/manifest.json` loads only `BentleyPlayz.FastFoodUsefulStuff.Smapi.dll`. No loader for `BusStopGuard.dll` was found in the files (no manifest points at it and `Smapi.dll` contains no assembly-loading code), so it may not be loaded at all today. Please check your SMAPI log for "BusStopGuard".
The kiosk therefore gets its own folder and manifest, exactly like `Content/` and `Code/` already do:

    BentleyPlayz FastFoodUsefulStuff/
      Content/   (Content Patcher pack, manifest.json)
      Code/      (Smapi.dll + BusStopGuard.dll, unchanged)
      Kiosk/     (NEW: manifest.json + BentleyPlayz.FastFoodUsefulStuff.Kiosk.dll)

It still ships in one ZIP and one install folder. SMAPI's log will show it as a third entry. The Content manifest lists it as a required dependency, so players cannot install half of it.

## How the kiosk opens the existing shops
Right-click the placed kiosk -> "Desserts or Food?" -> `Utility.TryOpenShopMenu` with the existing shop IDs `BentleyPlayz.FastFoodUsefulStuff_DessertKiosk` and `..._BentleyShop`. No stock, price or portrait is duplicated: the shop owner entries already use "AnyOrNone" with Bentley's portrait. The kiosk-only deal then lowers prices by 10% in that menu only (3 purchased units per day per player, stored in the player's save data), and restores normal prices when the deals run out. The counters are never discounted.

## Files
- `Content/data/kiosk.json` (new): texture, big craftable (1 tile, indoors and outdoors), crafting recipe (50 Wood, 5 Iron Bar, 3 Refined Quartz), letter, and a 2-heart unlock.
- `Content/assets/kiosk.png` (new, 16x32 pixel art).
- `Content/content.json` and `Content/manifest.json`: patched copies (one Include added; Kiosk dependency added; version 3.14.0). Nothing else changed.
- `Kiosk/` (new): source, csproj, manifest.
- `.github/workflows/build.yml` (new).

## GitHub Actions (official Pathoschild build environment)
The workflow uses `Pathoschild/SMAPI-ModBuildWorkflow/add-build-environment@v1`, which supplies the Stardew Valley and SMAPI reference assemblies. You do not upload any game or SMAPI DLLs. The kiosk project references `Pathoschild.Stardew.ModBuildConfig` 4.3.2 (this version number is from memory: if restore complains, tell me or bump it). `Directory.Build.props` (repo root) sets `ModZipPath` to `_releases`.
Result: artifact `BentleysFastFood-Complete` = the full install ZIP (Content, Code with your two original DLLs byte-for-byte, and Kiosk with the new DLL). The official `upload-release-artifacts@v1` step also runs, last, for the small kiosk-only ZIP; I could not check its inputs, so if that step errors it does not affect the complete artifact.
Your two original DLLs must already be committed in `Code/` in the repo: the workflow stops with a clear error if either is missing. I could not see your repo, so `MOD_DIR` at the top of the workflow assumes the mod folder sits at the repo root; edit it if yours differs. Put `Directory.Build.props` at the repo root.

## Still to test in game
Craft, place, save/reload, choose Desserts, Food and Cancel, check Bentley's portrait, check the 10% prices and the 3-per-day limit, and check the 2-heart letter and recipe.
