using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Core.Services.Scene
{
    public interface ISceneLoader
    {
        /// <summary>Charge (ou recharge) la GameScene, en déchargeant le menu s'il est ouvert.</summary>
        UniTask LoadGameSceneAsync(CancellationToken ct);

        /// <summary>Charge le menu principal, en déchargeant la partie si elle est ouverte.</summary>
        UniTask LoadMainMenuSceneAsync(CancellationToken ct);
    }

    /// <summary>
    /// Les deux scènes de contenu (menu principal et partie) sont mutuellement exclusives : une
    /// seule est chargée à la fois, en additif par-dessus la RootScene qui porte le métier.
    ///
    /// ⚠️ Un appelant qui vit DANS la scène qu'il quitte (bouton du menu, écran de fin de run)
    /// doit passer <see cref="CancellationToken.None"/> : son propre jeton est annulé par le
    /// déchargement même qu'il demande, ce qui interromprait la transition à mi-chemin et
    /// laisserait le joueur sans aucune scène.
    /// </summary>
    public class SceneLoader : ISceneLoader
    {
        private const string GameSceneName = "GameScene";
        private const string MainMenuSceneName = "MainMenuScene";

        // On va conserver une référence au scope racine
        private readonly LifetimeScope _parentScope;

        // Magie VContainer : En demandant un LifetimeScope dans le constructeur,
        // VContainer injecte automatiquement le scope dans lequel cette classe a été créée (le Root).
        public SceneLoader(LifetimeScope parentScope)
        {
            _parentScope = parentScope;
        }

        public UniTask LoadGameSceneAsync(CancellationToken ct) =>
            SwitchToAsync(GameSceneName, MainMenuSceneName, ct);

        public UniTask LoadMainMenuSceneAsync(CancellationToken ct) =>
            SwitchToAsync(MainMenuSceneName, GameSceneName, ct);

        private async UniTask SwitchToAsync(string targetScene, string otherScene, CancellationToken ct)
        {
            // 1. Décharger l'autre scène de contenu, puis la cible elle-même si elle est déjà
            //    chargée (cas du restart de fin de run, qui recharge la GameScene).
            await UnloadIfLoadedAsync(otherScene, ct);
            await UnloadIfLoadedAsync(targetScene, ct);

            // 2. On "met en file d'attente" le parent. Le prochain LifetimeScope
            // qui s'éveillera (celui de la scène chargée) s'attachera automatiquement à _parentScope.
            using (LifetimeScope.EnqueueParent(_parentScope))
            {
                // 3. On charge la scène additivement
                await SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive).ToUniTask(cancellationToken: ct);
            }

            // 4. On définit la scène chargée comme active
            UnityEngine.SceneManagement.Scene loaded = SceneManager.GetSceneByName(targetScene);
            if (loaded.IsValid())
            {
                SceneManager.SetActiveScene(loaded);
            }
        }

        private static async UniTask UnloadIfLoadedAsync(string sceneName, CancellationToken ct)
        {
            UnityEngine.SceneManagement.Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(existing).ToUniTask(cancellationToken: ct);
            }
        }
    }
}
