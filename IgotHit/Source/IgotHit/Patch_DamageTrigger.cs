using RimWorld;
using UnityEngine;
using Verse;

namespace IgotHit
{
    /// <summary>
    /// 检测小人受伤并触发肾上腺素飙升
    /// 当人形被击中（新增伤口）且原始疼痛 > 20% 时触发
    /// </summary>
    public static class Patch_PawnHealthTracker_AddHediff
    {
        // 缓存的 HediffDef，避免重复查找
        private static HediffDef _adrenalineRushDef;
        private static HediffDef _adrenalineCooldownDef;
        private static bool _defsResolved;
        private static bool _settingsApplied;

        /// <summary>
        /// 确保设置已应用到 Def 上（首次使用时触发，此时 Def 和设置均已加载完毕）
        /// </summary>
        private static void EnsureSettingsApplied()
        {
            if (_settingsApplied)
                return;
            if (ModMain.Settings != null)
            {
                ModMain.Settings.ApplyToDefs();
                _settingsApplied = true;
            }
        }

        /// <summary>
        /// 解析 HediffDef（延迟初始化，确保 Def 已加载完毕）
        /// </summary>
        private static void EnsureDefsResolved()
        {
            if (_defsResolved)
                return;

            _adrenalineRushDef = DefDatabase<HediffDef>.GetNamedSilentFail("AdrenalineRush");
            _adrenalineCooldownDef = DefDatabase<HediffDef>.GetNamedSilentFail("AdrenalineCooldown");

            if (_adrenalineRushDef == null)
                Log.Error("[IgotHit] Cannot find HediffDef 'AdrenalineRush' in DefDatabase!");
            if (_adrenalineCooldownDef == null)
                Log.Error("[IgotHit] Cannot find HediffDef 'AdrenalineCooldown' in DefDatabase!");

            _defsResolved = true;
        }

        public static void Postfix(Hediff hediff, Pawn_HealthTracker __instance)
        {
            // 首次使用时应用设置到 Def（确保持久化的设置在游戏加载后生效）
            EnsureSettingsApplied();

            // 只处理新增的伤口
            if (hediff == null || !(hediff is Hediff_Injury))
                return;

            Pawn pawn = hediff.pawn;
            if (pawn == null || !pawn.RaceProps.Humanlike)
                return;

            // 检查 colonyOnly 设置
            if (ModMain.Settings != null && ModMain.Settings.colonyOnly)
            {
                // 仅殖民地图格中的己方角色触发
                if (!pawn.IsColonist || !pawn.Spawned || pawn.Map == null || pawn.Map != Find.CurrentMap)
                    return;
            }

            // 确保 Def 引用已解析
            EnsureDefsResolved();

            // 检查是否已有冷却状态
            if (_adrenalineCooldownDef != null &&
                pawn.health.hediffSet.HasHediff(_adrenalineCooldownDef))
                return;

            // 检查是否已存在肾上腺素状态（不应重复触发）
            if (_adrenalineRushDef != null &&
                pawn.health.hediffSet.HasHediff(_adrenalineRushDef))
                return;

            // 如果 Def 未能解析，无法继续
            if (_adrenalineRushDef == null)
            {
                Log.Error("[IgotHit] AdrenalineRush def not resolved, cannot apply hediff.");
                return;
            }

            // 获取当前原始疼痛值
            // 注意：在 Postfix 执行时，当前的疼痛值就是未受肾上腺素修正的原始值
            float currentPain = pawn.health.hediffSet.PainTotal;

            // 疼痛需要高于20%才能触发
            if (currentPain < 0.2f)
                return;

            // 计算严重程度：疼痛20%~80% → 严重度0~1
            // 公式：severity = clamp((pain - 0.2) / 0.6, 0, 1)
            float severity = (currentPain - 0.2f) / 0.6f;
            severity = Mathf.Clamp01(severity);

            // 创建肾上腺素飙升状态并添加到小人
            Hediff adrenaline = HediffMaker.MakeHediff(_adrenalineRushDef, pawn);
            adrenaline.Severity = severity;
            pawn.health.AddHediff(adrenaline);
        }
    }
}
