using Core.Models;
using Core.Services.Economy;
using Core.Services.Simulation;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Persistence
{
    /// <summary>
    /// Décide QUAND sauvegarder. Le "comment" appartient au service de sauvegarde, le "quoi" au
    /// GameStateGateway : ce point d'entrée ne fait qu'orchestrer les trois déclencheurs.
    ///
    ///  1. Périodique — toutes les 5 minutes.
    ///  2. Événementiel — fin de run et achat de méta-progression, les deux moments où perdre
    ///     cinq minutes de progression fait vraiment mal. La sauvegarde sur fin de run empêche
    ///     aussi d'annuler un Game Over en tuant le processus.
    ///  3. Fermeture — croix, Alt+F4, quit. Ne couvre ni un kill process, ni une coupure de
    ///     courant, ni un crash du moteur : aucun jeu ne le peut, c'est le rôle de l'autosave.
    /// </summary>
    public class SaveScheduler : IAsyncStartable, IDisposable
    {
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5d);

        private readonly ISaveService _saveService;
        private readonly ISyncSaveService _syncSaveService;
        private readonly GameStateGateway _gateway;
        private readonly GameSessionManager _sessionManager;
        private readonly PrestigeManager _prestigeManager;

        private DisposableBag _disposables;
        private CancellationTokenSource _linkedCts;
        private bool _isWriting;

        public SaveScheduler(
            ISaveService saveService,
            ISyncSaveService syncSaveService,
            GameStateGateway gateway,
            GameSessionManager sessionManager,
            PrestigeManager prestigeManager)
        {
            _saveService = saveService;
            _syncSaveService = syncSaveService;
            _gateway = gateway;
            _sessionManager = sessionManager;
            _prestigeManager = prestigeManager;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // Token lié à celui du conteneur : le scope racine annule tout à sa destruction,
            // et Dispose() peut couper la boucle indépendamment.
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            CancellationToken ct = _linkedCts.Token;

            _sessionManager.OnSessionEnded
                .Subscribe(_ => RequestSave("fin de run", ct))
                .AddTo(ref _disposables);

            _prestigeManager.OnPrestigePurchased
                .Subscribe(_ => RequestSave("achat de prestige", ct))
                .AddTo(ref _disposables);

            Application.quitting += HandleApplicationQuitting;

            await AutoSaveLoopAsync(ct);
        }

        private async UniTask AutoSaveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Delay(AutoSaveInterval, cancellationToken: ct);
                    await WriteAsync("autosave", ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Destruction du scope : sortie normale, rien à signaler.
            }
        }

        private void RequestSave(string reason, CancellationToken ct)
        {
            // Forget() explicite : sans lui, une exception dans cette tâche disparaîtrait sans
            // le moindre log.
            WriteAsync(reason, ct).Forget();
        }

        private async UniTask WriteAsync(string reason, CancellationToken ct)
        {
            if (!CanWrite()) return;

            _isWriting = true;

            try
            {
                await _saveService.SaveAsync(_gateway.Capture(), ct);
                Debug.Log($"[SAVE] Sauvegarde écrite ({reason}).");
            }
            catch (OperationCanceledException)
            {
                // Fermeture pendant l'écriture : HandleApplicationQuitting prend le relais.
            }
            catch (Exception e)
            {
                Debug.LogError($"[SAVE] Sauvegarde impossible ({reason}) : {e.Message}");
            }
            finally
            {
                _isWriting = false;
            }
        }

        private bool CanWrite()
        {
            // Verrou de démarrage : tant que la restauration n'a pas eu lieu, l'état vivant est
            // vide. Écrire maintenant remplacerait la sauvegarde du joueur par une partie neuve —
            // ce qui arriverait pour de vrai s'il ferme le jeu pendant le chargement.
            if (!_gateway.HasRestored) return false;

            // Une écriture est déjà en vol. On abandonne celle-ci plutôt que de l'empiler :
            // la prochaine capturera de toute façon un état plus récent.
            if (_isWriting) return false;

            return true;
        }

        private void HandleApplicationQuitting()
        {
            if (!_gateway.HasRestored) return;

            // Chemin synchrone obligatoire : la boucle de jeu s'arrête ici, plus rien n'est repris
            // après un await.
            _syncSaveService.SaveBlocking(_gateway.Capture());
            Debug.Log("[SAVE] Sauvegarde de fermeture écrite.");
        }

        public void Dispose()
        {
            // Se désabonner d'un événement statique de Unity est impératif : il survit au scope.
            Application.quitting -= HandleApplicationQuitting;

            _disposables.Dispose();

            _linkedCts?.Cancel();
            _linkedCts?.Dispose();
            _linkedCts = null;
        }
    }
}
