namespace AreWeThereYet
{
    public class AWTYSettings : GameParameters.CustomParameterNode
    {
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => false;
        public override string Section => "Are We There Yet?";
        public override int SectionOrder => 1;
        public override string Title => string.Empty;
        public override string DisplaySection => "Are We There Yet?";

        [GameParameters.CustomParameterUI("Show destination body indicators",
            toolTip = "Shows colored circles before each task to indicate the target body")]
        public bool showBodyIndicators { get; set; } = true;
    }
}
