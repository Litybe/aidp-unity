namespace Aidp.Playtest
{
    /// <summary>Valeurs invariantes du package (chemins du pont fichier, noms des peripheriques virtuels).</summary>
    public static class PlaytestConstants
    {
        public const string AidpDirectory = ".aidp";
        public const string PlaytestDirectory = "playtest";
        public const string CompileDirectory = "compile";
        public const string RequestsDirectory = "requests";
        public const string ResultsDirectory = "results";
        public const string TestResultsDirectory = "test-results";
        public const string RecordingsDirectory = "recordings";
        public const string EditorDirectory = "editor";
        public const string HeartbeatFile = "heartbeat.json";
        public const string FramesDirectory = "frames";
        public const string JsonExtension = ".json";
        public const string PngExtension = ".png";
        public const string GifExtension = ".gif";
        public const string TempExtension = ".tmp";
        public const string DefaultScenarioDirectory = "Assets/Tests/Playtest/Scenarios";

        public const string VirtualKeyboardName = "AidpKeyboard";
        public const string VirtualGamepadName = "AidpGamepad";
        public const string GamepadPrefix = "gamepad.";

        public const string FfmpegPath = @"D:\DevTools\ffmpeg\bin\ffmpeg.exe";
        public const string FfmpegEnvironmentVariable = "AIDP_FFMPEG";

        public const string StepHold = "hold";
        public const string StepPress = "press";
        public const string StepRelease = "release";
        public const string StepTap = "tap";
        public const string StepWait = "wait";
        public const string StepCapture = "capture";
        public const string StepCommand = "cmd";
        public const string KeyMs = "ms";
        public const string KeyExpr = "expr";
        public const string KeyWithinMs = "withinMs";

        public const int DefaultTimeoutMs = 10000;
        public const int GifFrameIntervalMs = 125;
        public const int GifMaxFrames = 240;
        public const double DefaultTolerance = 0.001;
        public const int SceneSettleFrames = 2;

        /// <summary>Delai max du chargement de la scene (premier import a froid compris) ; hors timeoutMs du scenario.</summary>
        public const int SceneLoadTimeoutMs = 120000;

        /// <summary>Scene jouee par un scenario sans champ "scene" (si le fichier existe dans le projet).</summary>
        public const string DefaultArenaScene = "Assets/Tests/Playtest/Scenes/PlaytestArena.unity";
    }
}
