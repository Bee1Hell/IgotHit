using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IgotHit
{
    /// <summary>
    /// 模组设置：存储所有可配置的参数，提供 UI 绘制和持久化
    /// </summary>
    public class IgotHitSettings : ModSettings
    {
        // ========== 基础设置 ==========
        public int rushDurationHours = 2;        // 肾上腺素持续时间（小时）
        public int cooldownDurationHours = 24;   // 冷却持续时间（小时）
        public bool colonyOnly;                  // 是否仅在殖民地图格触发

        // ========== 阶段参数 ==========
        // 索引 0~4 对应 5个阶段
        public float[] painFactor = new[] { 0.90f, 0.80f, 0.60f, 0.50f, 0.20f };
        public float[] moveSpeed = new[] { 1.05f, 1.10f, 1.20f, 1.25f, 1.25f };
        public float[] manipulation = new[] { 1.00f, 0.95f, 0.90f, 0.80f, 0.60f };
        public float[] sight = new[] { 1.00f, 1.00f, 0.90f, 0.80f, 0.70f };
        public float[] restFall = new[] { 1.00f, 1.00f, 0.50f, 0.25f, 0.00f };

        // ========== 阶段阈值（严重度区间） ==========
        public float[] minSeverity = new[] { 0f, 0.25f, 0.50f, 0.75f, 0.90f };

        // 只读的乘数字段标签
        private static readonly string[] StageLabels = { "I Minor / 轻微", "II Mild / 轻度", "III Moderate / 中度", "IV Severe / 重度", "V Extreme / 极重" };

        /// <summary>
        /// 将设置中的阶段数值应用到 HediffDef 上，覆盖 XML 默认值
        /// </summary>
        public void ApplyToDefs()
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("AdrenalineRush");
            if (def?.stages == null || def.stages.Count < 5)
                return;

            for (int i = 0; i < 5; i++)
            {
                var stage = def.stages[i];
                stage.minSeverity = minSeverity[i];
                stage.painFactor = painFactor[i];
                stage.restFallFactor = restFall[i];

                // 替换 statFactors 中的 MoveSpeed
                if (stage.statFactors == null)
                    stage.statFactors = new List<StatModifier>();
                SetStatFactor(stage.statFactors, StatDefOf.MoveSpeed, moveSpeed[i]);

                // 替换 capMods 中的 Manipulation 和 Sight
                if (stage.capMods == null)
                    stage.capMods = new List<PawnCapacityModifier>();
                SetCapMod(stage.capMods, PawnCapacityDefOf.Manipulation, manipulation[i]);
                SetCapMod(stage.capMods, PawnCapacityDefOf.Sight, sight[i]);
            }

            // 更新冷却状态的 severityPerDay
            var cooldownDef = DefDatabase<HediffDef>.GetNamedSilentFail("AdrenalineCooldown");
            if (cooldownDef?.comps != null)
            {
                foreach (var comp in cooldownDef.comps)
                {
                    if (comp is HediffCompProperties_SeverityPerDay sevPerDay)
                    {
                        // severityPerDay = -24h / 用户设定小时数
                        sevPerDay.severityPerDay = -24f / cooldownDurationHours;
                    }
                }
            }
        }

        private static void SetStatFactor(List<StatModifier> list, StatDef stat, float val)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].stat == stat)
                {
                    list[i].value = val;
                    return;
                }
            }
            list.Add(new StatModifier { stat = stat, value = val });
        }

        private static void SetCapMod(List<PawnCapacityModifier> list, PawnCapacityDef cap, float val)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].capacity == cap)
                {
                    list[i].postFactor = val;
                    return;
                }
            }
            list.Add(new PawnCapacityModifier { capacity = cap, postFactor = val });
        }

        // ========== 内置转换为 ticks ==========
        public int RushDurationTicks => rushDurationHours * 2500;

        // ========== UI 设置窗口 ==========
        private Vector2 _scrollPos;

        public void DoSettingsWindowContents(Rect inRect)
        {
            // 计算全部内容的总高度
            float totalHeight = TotalContentHeight();

            // 整个设置面板放在一个 ScrollView 中（Dialog_ModSettings 不自带滚动）
            Rect viewRect = new Rect(0f, 0f, inRect.width - 30f, totalHeight);
            Widgets.BeginScrollView(inRect, ref _scrollPos, viewRect);

            var ls = new Listing_Standard();
            ls.Begin(viewRect);

            // ---- 基本设置 ----
            Text.Font = GameFont.Medium;
            ls.Label("Basic Settings / 基本设置");
            Text.Font = GameFont.Small;

            // 持续时间
            ls.Label("Rush Duration / 肾上腺素时长:  " + rushDurationHours + "h (0.5~8h)");
            float rushF = rushDurationHours;
            rushF = ls.Slider(rushF, 0.5f, 8f);
            rushDurationHours = Mathf.RoundToInt(rushF);
            if (rushDurationHours < 1) rushDurationHours = 1;

            // 冷却时间
            ls.Label("Cooldown Duration / 冷却时长:  " + cooldownDurationHours + "h (1~72h)");
            float coolF = cooldownDurationHours;
            coolF = ls.Slider(coolF, 1f, 72f);
            cooldownDurationHours = Mathf.RoundToInt(coolF);

            // 仅殖民地触发
            ls.CheckboxLabeled("Colony pawns only / 仅在殖民地内触发", ref colonyOnly);

            ls.GapLine();

            // ---- 阶段参数 ----
            Text.Font = GameFont.Medium;
            ls.Label("Stage Parameters / 阶段参数");
            Text.Font = GameFont.Small;

            for (int i = 0; i < 5; i++)
            {
                Text.Font = GameFont.Medium;
                ls.Label(StageLabels[i]);
                Text.Font = GameFont.Small;

                // 严重度区间提示
                string sevStr = (i < 4)
                    ? $"Severity / 严重度: {minSeverity[i]:F2} ~ {minSeverity[i + 1]:F2}"
                    : $"Severity / 严重度: {minSeverity[i]:F2} ~ 1.00";
                ls.Label(sevStr);

                // 疼痛系数
                ls.Label($"Pain / 疼痛 × {painFactor[i]:F2}");
                painFactor[i] = ls.Slider(painFactor[i], 0.05f, 1.0f);

                // 移速
                ls.Label($"Move Speed / 移动速度 × {moveSpeed[i]:F2}");
                moveSpeed[i] = ls.Slider(moveSpeed[i], 0.5f, 2.0f);

                // 操作
                ls.Label($"Manipulation / 操作能力 × {manipulation[i]:F2}");
                manipulation[i] = ls.Slider(manipulation[i], 0.1f, 1.5f);

                // 视觉
                ls.Label($"Sight / 视觉 × {sight[i]:F2}");
                sight[i] = ls.Slider(sight[i], 0.1f, 1.5f);

                // 休息
                ls.Label($"Rest Fall / 休息速率 × {restFall[i]:F2}");
                restFall[i] = ls.Slider(restFall[i], 0.0f, 1.0f);

                ls.Gap(4f);
            }

            // 底部间距
            ls.Gap(8f);

            ls.End();
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 计算所有 UI 内容的总高度（使用宽松估计确保全部显示）
        /// </summary>
        private float TotalContentHeight()
        {
            // 每阶段保守估计 300px (Medium标题30 + Severity22 + 5对标签滑块各48 + Gap4 + 余量)
            float perStage = 310f;
            // 基本设置区域
            float basic = 200f;
            // 5个阶段 + 基本设置 + 底部提示 + 余量
            return basic + 5f * perStage + 40f + 50f;
        }

        // ========== 持久化 ==========

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref rushDurationHours, "rushDurationHours", 2);
            Scribe_Values.Look(ref cooldownDurationHours, "cooldownDurationHours", 24);
            Scribe_Values.Look(ref colonyOnly, "colonyOnly", false);

            // 逐个保存数组元素（Scribe_Collections 不支持 float[]）
            for (int i = 0; i < 5; i++)
            {
                Scribe_Values.Look(ref painFactor[i], "painFactor_" + i, DefaultPain[i]);
                Scribe_Values.Look(ref moveSpeed[i], "moveSpeed_" + i, DefaultSpeed[i]);
                Scribe_Values.Look(ref manipulation[i], "manipulation_" + i, DefaultManip[i]);
                Scribe_Values.Look(ref sight[i], "sight_" + i, DefaultSight[i]);
                Scribe_Values.Look(ref restFall[i], "restFall_" + i, DefaultRest[i]);
                Scribe_Values.Look(ref minSeverity[i], "minSeverity_" + i, DefaultMinSev[i]);
            }
        }

        // 默认值常量（用于反序列化回退）
        private static readonly float[] DefaultPain = { 0.90f, 0.80f, 0.60f, 0.50f, 0.20f };
        private static readonly float[] DefaultSpeed = { 1.05f, 1.10f, 1.20f, 1.25f, 1.25f };
        private static readonly float[] DefaultManip = { 1.00f, 0.95f, 0.90f, 0.80f, 0.60f };
        private static readonly float[] DefaultSight = { 1.00f, 1.00f, 0.90f, 0.80f, 0.70f };
        private static readonly float[] DefaultRest = { 1.00f, 1.00f, 0.50f, 0.25f, 0.00f };
        private static readonly float[] DefaultMinSev = { 0f, 0.25f, 0.50f, 0.75f, 0.90f };
    }
}
