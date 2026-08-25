using Core.Models;
using Core.Services.Localization;
using Core.Services.Persistence;
using Core.Services.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace Core.Boot
{
    /// <summary>
    /// Séquence de démarrage. Le bootstrapper ne connaît plus le détail de ce qu'il faut hydrater :
    /// il délègue au GameStateGateway, qui est le seul endroit où l'on décide quels systèmes
    /// entrent dans la sauvegarde.
    /// </summary>
    public class GameBootstrapper : IAsyncStartable
    {
        private readonly ISaveService _saveService;
        private readonly ISceneLoader _sceneLoader;
        private readonly GameStateGateway _gateway;
        private readonly JsonLocalizationService _localizationService;

        public GameBootstrapper(
            ISaveService saveService,
            ISceneLoader sceneLoader,
            GameStateGateway gateway,
            JsonLocalizationService localizationService)
        {
            _saveService = saveService;
            _sceneLoader = sceneLoader;
            _gateway = gateway;
            _localizationService = localizationService;
        }

        public async UniTask StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log("[BOOT] Initialisation du système en cours...");

                // 1. Localisation — dans le try et avec le token : une annulation pendant le
                //    chargement ne doit pas laisser la séquence continuer sur un service vide.
                await _localizationService.LoadLanguageAsync("fr", cancellationToken);

                // 2. Récupération de la sauvegarde
                SaveData save = await _saveService.FetchSaveAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // 3. Hydratation de TOUS les systèmes persistés (monnaies, générateurs, prestige,
                //    Trace, bouton d'urgence) en un seul point.
                _gateway.Restore(save);

                Debug.Log("[BOOT] État restauré. Transition vers la GameScene...");

                // 4. Passage à la scène de jeu
                await _sceneLoader.LoadGameSceneAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[BOOT] Démarrage interrompu.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BOOT] Erreur critique : {ex}");
            }
        }
    }
}
