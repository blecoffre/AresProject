using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Core.Services.Scene
{
    public interface ISceneLoader
    {
        UniTask LoadGameSceneAsync(CancellationToken ct);
    }

    public class SceneLoader : ISceneLoader
    {
        private const string GameSceneName = "GameScene";

        // On va conserver une référence au scope racine
        private readonly LifetimeScope _parentScope;

        // Magie VContainer : En demandant un LifetimeScope dans le constructeur, 
        // VContainer injecte automatiquement le scope dans lequel cette classe a été créée (le Root).
        public SceneLoader(LifetimeScope parentScope)
        {
            _parentScope = parentScope;
        }

        public async UniTask LoadGameSceneAsync(CancellationToken ct)
        {
            // 1. Décharger la scène existante si elle est déjà chargée (pour le restart)
            UnityEngine.SceneManagement.Scene existingScene = SceneManager.GetSceneByName(GameSceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(existingScene).ToUniTask(cancellationToken: ct);
            }

            // 2. On "met en file d'attente" le parent. Le prochain LifetimeScope 
            // qui s'éveillera (celui de la GameScene) s'attachera automatiquement à _parentScope.
            using (LifetimeScope.EnqueueParent(_parentScope))
            {
                // 3. On charge la scène additivement
                await SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Additive).ToUniTask(cancellationToken: ct);
            }

            // 4. On définit la GameScene comme active
            UnityEngine.SceneManagement.Scene gameScene = SceneManager.GetSceneByName(GameSceneName);
            if (gameScene.IsValid())
            {
                SceneManager.SetActiveScene(gameScene);
            }
        }
    }
}