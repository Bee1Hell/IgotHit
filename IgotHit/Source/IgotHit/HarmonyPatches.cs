using HarmonyLib;
using Verse;

namespace IgotHit
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("Bee1Hell.IgotHit");

            // 手动注册 Patch，避免 HarmonyPatch 属性自动匹配参数失败
            var targetMethod = AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
                new[]
                {
                    typeof(Hediff),
                    typeof(BodyPartRecord),
                    typeof(DamageInfo?),
                    typeof(DamageWorker.DamageResult)
                });

            if (targetMethod == null)
            {
                Log.Error("[IgotHit] Failed to find Pawn_HealthTracker.AddHediff method for patching!");
                return;
            }

            harmony.Patch(targetMethod,
                postfix: new HarmonyMethod(typeof(Patch_PawnHealthTracker_AddHediff), nameof(Patch_PawnHealthTracker_AddHediff.Postfix)));
        }
    }
}
