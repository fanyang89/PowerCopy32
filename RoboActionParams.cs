namespace PowerCopy32
{
    class RoboActionParams
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public RoboAction RoboAction { get; set; }
    }

    enum RoboAction
    {
        Copy,
        Move
    }
}