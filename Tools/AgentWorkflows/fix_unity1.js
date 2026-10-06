export const meta = { name: 'harekat-fix-unity-1', description: 'Gerçek Unity bulguları: BotLod hysteresis testi, DrivableVehicle.IsOccupant, tuş ipucu metinleri (1 Sonnet ajanı)', phases: [{ title: 'Unity düzeltme' }] }
const ROOT = '/Users/f2gomac/Desktop/FPS-GAME'
const out = await agent(`Project HAREKÂT, Unity 6000.6 C# at ${ROOT}. Verify with: zsh ${ROOT}/Tools/UnityVerify/verify.sh ${ROOT}/Tools/UnityVerify/out_fix1 --player --tests (keep 0 errors, 489+ tests).
Real Unity (run by Cursor) reported, see ${ROOT}/Docs/DURUM.md lines around "2026-10-05 23:55":
1) EditMode test BotLodTests.Tier_HasHysteresis FAILS in Unity (it is #if UNITY_EDITOR so our shim does not run it). Read Assets/_Project/Tests/EditMode/BotLodTests.cs and Infrastructure/AI/BotLod.cs, find whether the test or the hysteresis logic is wrong, fix the real bug (prefer fixing logic if the test expresses the intended 10 m margin behaviour).
2) Add DrivableVehicle.IsOccupant(PlayerId) (driver or any passenger/gunner) in Infrastructure/Vehicles/DrivableVehicle.cs, then simplify Cursor's temporary occupant check in Infrastructure/Combat/BallisticsSystem.cs to use it (keep behaviour).
3) Prompt strings that hardcode default keys (PlayerInteraction "[F] ..." prompts, HudWeaponView "[R]" hint) should show the CURRENT binding via InputBindings (display name of bound key), keeping Turkish text.
TOKEN-EFFICIENT, minimal edits, no deletes/.meta/git writes. Append a dated line to ${ROOT}/Docs/DURUM.md "Günlük". Return a short summary.`, { label: 'unity-fix', phase: 'Unity düzeltme', model: 'sonnet', effort: 'medium' })
return out
