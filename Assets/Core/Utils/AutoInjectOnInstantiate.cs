using UnityEngine;
using VContainer.Unity;

namespace Core.Utils
{
    /// <summary>
    /// À placer sur la racine d'un Prefab instancié au runtime.
    /// Récupère automatiquement le LifetimeScope de la scène et injecte 
    /// toutes les dépendances dans le GameObject et ses enfants.
    /// </summary>
    public class AutoInjectOnInstantiate : MonoBehaviour
    {
        private void Awake()
        {
            // 1. Trouver le LifetimeScope actif pour ce GameObject dans la hiérarchie
            var scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);

            if (scope != null)
            {
                // 2. Injecter les dépendances (dont ILocalizationService) dans tout le Prefab
                scope.Container.InjectGameObject(gameObject);
            }
            else
            {
                Debug.LogWarning($"[AutoInjectOnSpawn] Aucun LifetimeScope trouvé pour {gameObject.name}");
            }
        }
    }
}