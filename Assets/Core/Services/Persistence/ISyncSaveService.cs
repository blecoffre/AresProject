using Core.Models;

namespace Core.Services.Persistence
{
    /// <summary>
    /// Écriture synchrone, réservée au chemin de fermeture de l'application.
    ///
    /// Application.quitting et OnApplicationQuit arrêtent la boucle de jeu : tout ce qui est
    /// awaité à partir de là n'est jamais repris. Une sauvegarde de fermeture doit donc être
    /// bloquante, et elle ne peut viser que le disque local (une écriture réseau ou Steam Cloud
    /// n'a aucune garantie d'aboutir pendant l'arrêt du processus).
    /// </summary>
    public interface ISyncSaveService
    {
        void SaveBlocking(SaveData data);
    }
}
