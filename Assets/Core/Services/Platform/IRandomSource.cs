namespace Core.Services.Platform
{
    /// <summary>
    /// D'où les systèmes de jeu tirent leur hasard.
    ///
    /// Même raison d'être que <see cref="ITimeSource"/> : un système qui appelle
    /// <c>UnityEngine.Random</c> en dur ne se rejoue pas. Le harnais d'équilibrage (session
    /// « chiffres ») doit pouvoir dérouler deux fois la même campagne et obtenir deux fois le
    /// même résultat ; il substitue une source graine. Premier client : le coup critique de
    /// l'Overclock (2026-10-16).
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Un tirage uniforme dans [0 ; 1).</summary>
        float NextFloat01();
    }
}
