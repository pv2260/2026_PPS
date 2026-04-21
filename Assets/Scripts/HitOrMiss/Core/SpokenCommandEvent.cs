namespace HitOrMiss
{
    /// <summary>
    /// Represents a player response event (controller button or voice).
    /// </summary>
    [System.Serializable]
    public struct ResponseEvent
    {
        public string rawSource;           // e.g. "controller_left", "keyboard_H"
        public SemanticCommand command;
        public float confidence;
        public double timestamp;           // Time.timeAsDouble when the input occurred
    }
}
