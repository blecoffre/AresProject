using UnityEngine;

namespace Core.Services.Platform
{
    public interface IApplicationQuitter
    {
        void Quit();
    }

    /// <summary>
    /// Ferme le jeu. La sauvegarde n'est PAS faite ici : le <c>SaveScheduler</c> écoute
    /// <c>Application.quitting</c> et écrit de façon synchrone, quel que soit le chemin de sortie
    /// (bouton, Alt+F4, fermeture Steam). Dupliquer l'écriture ici créerait deux chemins à garder
    /// cohérents.
    /// </summary>
    public sealed class ApplicationQuitter : IApplicationQuitter
    {
        public void Quit()
        {
#if UNITY_EDITOR
            // Application.Quit est ignoré dans l'éditeur : on sort du Play Mode à la place, ce qui
            // déclenche aussi Application.quitting, donc la sauvegarde.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
