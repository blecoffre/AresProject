namespace Core.Services.Audio
{
    /// <summary>
    /// L'ambiance musicale demandée par une scène. Une valeur par contexte, et non par morceau :
    /// la scène dit OÙ l'on est, la bibliothèque audio décide de ce qui s'y joue.
    /// </summary>
    public enum MusicTrack
    {
        /// <summary>Aucune musique : fondu vers le silence.</summary>
        None = 0,
        MainMenu = 1,
        Game = 2,
    }
}
