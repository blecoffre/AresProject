namespace Core.Services.Platform
{
    /// <summary>
    /// L'heure RÉELLE, celle d'une main qui martèle une touche — par opposition à
    /// <see cref="ITimeSource"/>, le pas de la simulation, qui s'arrête en pause.
    ///
    /// Premier client : l'anti-rafale de l'Overclock (0,1 s entre deux appuis), qui protège le
    /// joueur d'un double clic et non la partie. Injectée depuis le 2026-10-20 pour le harnais
    /// d'équilibrage : une campagne simulée déroule des heures de jeu en quelques secondes
    /// réelles, et une horloge réelle y aurait jugé « trop rapprochés » tous les appuis d'une run.
    /// </summary>
    public interface IRealtimeClock
    {
        /// <summary>Secondes écoulées depuis une origine fixe, croissantes.</summary>
        double Now { get; }
    }
}
