using UnityEngine;
using Verse;

namespace IgotHit
{
    public class ModMain : Mod
    {
        public static IgotHitSettings Settings;

        public ModMain(ModContentPack content) : base(content)
        {
            Settings = GetSettings<IgotHitSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "I got hit！";
        }

        /// <summary>
        /// 当设置被写入时，将设置应用到 Def 上
        /// </summary>
        public override void WriteSettings()
        {
            base.WriteSettings();
            Settings.ApplyToDefs();
        }
    }
}
