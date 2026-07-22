using UnityEngine;
using Verse;

namespace IgotHit
{
    /// <summary>
    /// 肾上腺素飙升的组件属性
    /// </summary>
    public class CompProperties_AdrenalineRush : HediffCompProperties
    {
        // 持续时长：2小时 = 5000 ticks（RimWorld 1天 = 60000 ticks）
        public int durationTicks = 5000;

        public CompProperties_AdrenalineRush()
        {
            compClass = typeof(CompAdrenalineRush);
        }
    }

    /// <summary>
    /// 肾上腺素飙升的组件逻辑：
    /// - 每个 Tick 根据当前疼痛值动态调整 severity → 自动切换阶段
    /// - 到达2小时后自动移除并添加冷却
    /// - 若疼痛降至20%以下则提前结束
    /// </summary>
    public class CompAdrenalineRush : HediffComp
    {
        private int elapsedTicks;
        private bool removalPending;

        // 缓存冷却 HediffDef
        private static HediffDef _cooldownDef;
        private static bool _cooldownDefResolved;

        public CompProperties_AdrenalineRush Props => (CompProperties_AdrenalineRush)props;

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (removalPending || parent == null)
                    return "";
                int maxDuration = (ModMain.Settings != null)
                    ? ModMain.Settings.RushDurationTicks
                    : Props.durationTicks;
                int remainingTicks = maxDuration - elapsedTicks;
                if (remainingTicks <= 0)
                    return "";
                int hours = remainingTicks / 2500;
                int mins = (remainingTicks % 2500) * 60 / 2500;
                return $"{hours}h{mins:D2}m";
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (Pawn == null || !Pawn.Spawned || removalPending)
                return;

            elapsedTicks += delta;

            // 从设置中读取持续时间
            int maxDuration = (ModMain.Settings != null)
                ? ModMain.Settings.RushDurationTicks
                : Props.durationTicks;

            // 计时优先：到达持续时间强制结束
            if (elapsedTicks >= maxDuration)
            {
                removalPending = true;
                ApplyCooldownAndRemove();
                return;
            }

            // 动态更新 severity ← 根据当前原始疼痛值重新计算
            UpdateSeverityFromPain();
        }

        /// <summary>
        /// 根据当前疼痛值重新计算 severity
        /// 反向修正本 hediff 的 painFactor 以获取"原始疼痛"的近似值
        /// </summary>
        private void UpdateSeverityFromPain()
        {
            if (Pawn == null || parent == null)
                return;

            // 获取当前 PainTotal（已被本 hediff 的 painFactor 缩小）
            float currentPain = Pawn.health.hediffSet.PainTotal;

            // 反向估算扣除本 hediff 影响后的原始疼痛
            if (parent.CurStage != null)
            {
                float myPainFactor = parent.CurStage.painFactor;
                if (myPainFactor > 0.001f)
                    currentPain /= myPainFactor;
            }

            // 疼痛低于 20% → 提前结束（伤势已好转）
            if (currentPain < 0.2f)
            {
                removalPending = true;
                ApplyCooldownAndRemove();
                return;
            }

            // severity = clamp((pain - 0.2) / 0.6, 0, 1)
            float newSeverity = (currentPain - 0.2f) / 0.6f;
            newSeverity = Mathf.Clamp01(newSeverity);

            parent.Severity = newSeverity;
        }

        /// <summary>
        /// 添加冷却状态并移除自身
        /// </summary>
        private void ApplyCooldownAndRemove()
        {
            ResolveCooldownDef();

            if (_cooldownDef != null && !Pawn.health.hediffSet.HasHediff(_cooldownDef))
            {
                Hediff cooldown = HediffMaker.MakeHediff(_cooldownDef, Pawn);
                cooldown.Severity = 1.0f;
                Pawn.health.AddHediff(cooldown);
            }

            Pawn.health.RemoveHediff(parent);
        }

        private static void ResolveCooldownDef()
        {
            if (_cooldownDefResolved)
                return;
            _cooldownDef = DefDatabase<HediffDef>.GetNamedSilentFail("AdrenalineCooldown");
            if (_cooldownDef == null)
                Log.Error("[IgotHit] Cannot find HediffDef 'AdrenalineCooldown' for cooldown application!");
            _cooldownDefResolved = true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref elapsedTicks, "elapsedTicks", 0);
            Scribe_Values.Look(ref removalPending, "removalPending", false);
        }
    }
}
