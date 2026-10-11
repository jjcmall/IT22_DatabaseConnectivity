namespace CodeEscape.Game
{
    /// <summary>The high-level part of the game the player is currently in.</summary>
    public enum GameState
    {
        Boot,
        MainMenu,
        LanguageSelect,
        Intro,
        Playing,
        Paused,
        Challenge,
        Ending,
        Results,
        Cutscene,
        LevelMap
    }
}
