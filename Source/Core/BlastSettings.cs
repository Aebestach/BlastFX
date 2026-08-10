using KSP.Localization;

namespace BlastFX
{
    public class BlastParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#BFX_ParamTitle");
        public override string DisplaySection => "BlastFX";
        public override string Section => "BlastFX";
        public override int SectionOrder => 1;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI(
            "#BFX_ParamReplaceStock",
            toolTip = "#BFX_ParamReplaceStock_tip")]
        public bool replaceStockExplosions = false;

        public static BlastParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<BlastParameters>();
    }

    public static class BlastSettings
    {
        public static bool ReplaceStockExplosions =>
            BlastParameters.Instance?.replaceStockExplosions ?? false;
    }
}
