using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Boot
{
    /// <summary>
    /// Garde des scopes de scène de contenu (menu principal, partie) : ils ne se construisent que
    /// rattachés au <c>RootLifetimeScope</c>, que <c>SceneLoader</c> leur passe en parent.
    ///
    /// Lancer le Play depuis la GameScene ou la MainMenuScene laisse le scope sans parent.
    /// VContainer échoue alors sur la première dépendance du Root qu'il croise — souvent celle
    /// d'un <c>LocalizedText</c> (« No such registration of type: ILocalizationService »), ce qui
    /// se lit comme un bug de localisation. Cette garde remplace ce diagnostic trompeur par le
    /// vrai (backlog, « Pièges à retenir », jusqu'au 2026-10-15).
    /// </summary>
    public static class RootSceneGuard
    {
        private const string RootSceneName = "RootScene";

        /// <summary>
        /// Vrai si la RootScene est chargée. Sinon, le dit en une erreur explicite : l'appelant
        /// renonce alors à construire son conteneur plutôt que d'en lever une trompeuse.
        /// </summary>
        public static bool IsRootSceneLoaded(Object context)
        {
            if (SceneManager.GetSceneByName(RootSceneName).isLoaded) return true;

            Debug.LogError(
                $"[BOOT] {context.name} : la RootScene n'est pas chargée, aucun service n'est " +
                "résoluble et la scène reste inerte. Lancer le Play depuis " +
                "Assets/Data/Scenes/RootScene.unity.",
                context);

            return false;
        }
    }
}
