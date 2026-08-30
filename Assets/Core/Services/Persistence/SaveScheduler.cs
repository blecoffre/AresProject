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

        /// <summary>Une écriture est en vol. Interdit d'en lancer une seconde en parallèle.</summary>
        private bool _isWriting;

        /// <summary>
        /// Motif de la demande arrivée PENDANT une écriture, null si rien n'attend.
        ///
        /// Un seul emplacement, et c'est délibéré : entre deux demandes en attente, seule la
        /// dernière a un intérêt — l'état intermédiaire n'a jamais besoin d'atteindre le disque.
        /// Une file serait même FAUSSE ici : <see cref="GameStateGateway.Capture"/> retourne un
        /// tampon partagé qui continue de muter, donc empiler des demandes empilerait N
        /// références au même objet.
        /// </summary>
        private string _pendingReason;

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

        /// <summary>
        /// Écrit l'état courant, et rattrape ce qui a été demandé pendant l'écriture.
        ///
        /// Une demande refusée n'est plus ABANDONNÉE. L'ancienne version se justifiait par « la
        /// prochaine capturera un état plus récent », ce qui ne tient que s'il y en a une
        /// prochaine : trois achats de prestige rapprochés suivis d'un Game Over avaient fait
        /// perdre l'écriture de fin de run. La demande arme désormais un tour de rattrapage, si
        /// bien qu'une rafale de N demandes coûte AU PLUS une écriture de plus — et que le
        /// dernier état atteint toujours le disque.
        /// </summary>
        private async UniTask WriteAsync(string reason, CancellationToken ct)
        {
            // Verrou de démarrage : tant que la restauration n'a pas eu lieu, l'état vivant est
            // vide. Écrire maintenant remplacerait la sauvegarde du joueur par une partie neuve —
            // ce qui arriverait pour de vrai s'il ferme le jeu pendant le chargement.
            if (!_gateway.HasRestored) return;

            if (_isWriting)
            {
                _pendingReason = reason;
                return;
            }

            _isWriting = true;

            try
            {
                string current = reason;

                // Le rattrapage porte son propre message : sans lui, deux écritures consécutives
                // produisent deux lignes identiques que la console de Unity replie en une seule,
                // et le mécanisme devient invisible — y compris pour qui le débogue.
                bool isCatchUp = false;

                do
                {
                    // Remis à null AVANT l'écriture, jamais après : une demande qui arrive
                    // pendant celle-ci repositionne le drapeau, et déclenche donc un tour de
                    // plus. L'effacer ensuite l'écraserait au lieu de la servir.
                    _pendingReason = null;

                    try
                    {
                        await _saveService.SaveAsync(_gateway.Capture(), ct);

                        Debug.Log(isCatchUp
                            ? $"[SAVE] Sauvegarde écrite ({current}, rattrapage)."
                            : $"[SAVE] Sauvegarde écrite ({current}).");
                    }
                    catch (OperationCanceledException)
                    {
                        // Fermeture pendant l'écriture : HandleApplicationQuitting prend le relais.
                        return;
                    }
                    catch (Exception e)
                    {
                        // Capture DANS la boucle : un échec ne doit pas emporter avec lui la
                        // demande qui attend son tour.
                        Debug.LogError($"[SAVE] Sauvegarde impossible ({current}) : {e.Message}");
                    }

                    current = _pendingReason;
                    isCatchUp = true;
                }
                while (current != null);
            }
            finally
            {
                _isWriting = false;
            }
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
