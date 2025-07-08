namespace PowerCopy32 {
    class RoboActionParams {
        public RoboAction RoboAction { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
    }

    enum RoboAction {
        Copy,
        Move
    }
}
