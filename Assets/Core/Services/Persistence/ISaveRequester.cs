namespace Core.Services.Persistence
{
    /// <summary>
    /// Demande une sauvegarde hors des déclencheurs automatiques — retour au menu principal, par
    /// exemple. Passe par la même file que l'autosave : jamais deux écritures en parallèle, et la
    /// dernière demande atteint toujours le disque.
    /// </summary>
    public interface ISaveRequester
    {
        /// <param name="reason">Motif écrit dans le log de sauvegarde, pour le développeur.</param>
        void RequestSave(string reason);
    }
}
