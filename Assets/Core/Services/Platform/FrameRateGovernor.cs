using Core.Services.Settings;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Platform
{
    /// <summary>
    /// Plafonne la cadence d'affichage, et la réduit quand la fenêtre perd le focus.
    ///
    /// Ares est un jeu d'interface : l'écran bouge peu, et rien ne justifie de le redessiner au
    /// rythme du moniteur. Laissé libre, le jeu monte à 144 ou 165 images par seconde pour
    /// afficher des compteurs qui changent lentement — donc des ventilateurs qui tournent, une
    /// batterie qui fond, et sur Steam Deck une autonomie divisée. C'est un motif d'évaluation
    /// négative classique sur les jeux incrémentaux.
    ///
    /// La V-Sync et le plafond viennent des options du joueur (<see cref="ISettingsService"/>),
    /// et CE service est le seul à les écrire dans Unity. Les deux s'excluent : tant que
    /// <see cref="QualitySettings.vSyncCount"/> est non nul, <see cref="Application.targetFrameRate"/>
    /// est purement ignoré sur desktop et la cadence reste collée au taux de rafraîchissement de
    /// l'écran — la documentation de Unity l'impose dans son propre exemple. Par défaut la V-Sync
    /// est coupée et le plafond à 60 : sur une interface 2D, l'absence de V-Sync ne produit aucun
    /// déchirement perceptible, il n'y a pas de mouvement de caméra pour le révéler.
    ///
    /// Hors focus, la SIMULATION continue de tourner — c'est <c>runInBackground</c>, réglé côté
    /// Player Settings, qui le garantit, et la Trace continue donc de monter pendant qu'on
    /// consulte autre chose. Seul le RENDU se calme. La distinction est le cœur du réglage :
    /// on ne met pas la partie en pause, on cesse juste de la dessiner pour rien.
    /// </summary>
    public class FrameRateGovernor : IStartable, IDisposable
    {
        /// <summary>
        /// Cadence fenêtre inactive. Assez bas pour rendre la main au système, assez haut pour
        /// que le retour à la fenêtre soit instantané plutôt que saccadé.
        /// </summary>
        private const int UnfocusedFrameRate = 10;

        /// <summary>Aucune limite : la valeur de repos de Unity, restaurée à la destruction.</summary>
        private const int UnlimitedFrameRate = -1;

        private readonly ISettingsService _settings;

        private int _previousVSyncCount;
        private bool _isHooked;
        private bool _hasFocus = true;
        private DisposableBag _disposables;

        public FrameRateGovernor(ISettingsService settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            // Mémorisé AVANT d'écraser : sans ça, quitter le Play Mode laisserait l'éditeur
            // avec la VSync coupée jusqu'au prochain redémarrage de Unity. Ces deux propriétés
            // sont statiques et survivent au scope, elles ne s'oublient pas toutes seules.
            _previousVSyncCount = QualitySettings.vSyncCount;

            // Cadence pleine au démarrage, SANS consulter Application.isFocused — et c'est
            // délibéré. Au premier frame le focus n'est pas encore établi et isFocused peut
            // répondre false alors que la fenêtre s'apprête à le prendre : partir sur la valeur
            // hors-focus figerait le jeu à 10 images par seconde, et aucun focusChanged ne
            // viendrait le rattraper puisqu'il n'y aurait, du point de vue de Unity, aucun
            // CHANGEMENT à signaler. Le défaut symétrique — un jeu lancé en arrière-plan qui
            // tourne à 60 jusqu'au premier basculement — ne coûte que quelques secondes de
            // rendu inutile. On préfère ce défaut-là. (_hasFocus démarre donc à true.)
            //
            // Les deux abonnements émettent leur valeur courante dès la souscription : c'est ce
            // premier passage qui applique les réglages du joueur au démarrage. Écrire dans
            // QualitySettings ici plutôt que s'en remettre au niveau de qualité : celui-ci peut
            // changer au runtime et réimposerait sa propre V-Sync.
            _disposables = new DisposableBag();
            _settings.VSync.Subscribe(_ => Apply()).AddTo(ref _disposables);
            _settings.FrameRateCap.Subscribe(_ => Apply()).AddTo(ref _disposables);

            Application.focusChanged += HandleFocusChanged;
            _isHooked = true;
        }

        /// <summary>
        /// Deux affectations d'entiers statiques : ni allocation, ni boxing, ni closure.
        /// L'abonnement lui-même n'alloue son délégué qu'une fois, au démarrage.
        /// </summary>
        private void HandleFocusChanged(bool hasFocus)
        {
            _hasFocus = hasFocus;
            Apply();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PERF] Fenêtre {(hasFocus ? "active" : "inactive")} — cadence plafonnée à {Application.targetFrameRate} FPS.");
#endif
        }

        /// <summary>
        /// Hors focus, la V-Sync est coupée même si le joueur l'a demandée : elle rendrait le
        /// plafond de 10 FPS inopérant, et c'est lui qui rend la main au système. Elle revient
        /// au retour du focus.
        /// </summary>
        private void Apply()
        {
            if (!_hasFocus)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = UnfocusedFrameRate;
                return;
            }

            bool vSync = _settings.VSync.CurrentValue;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = vSync ? UnlimitedFrameRate : _settings.FrameRateCap.CurrentValue;
        }

        public void Dispose()
        {
            _disposables.Dispose();

            // Se désabonner d'un événement statique de Unity est impératif : il survit au scope,
            // et un handler oublié maintiendrait en vie l'instance entière. Même règle que le
            // Application.quitting du SaveScheduler.
            if (_isHooked)
            {
                Application.focusChanged -= HandleFocusChanged;
                _isHooked = false;
            }

            // Restauration de l'état global. Sans elle, la sortie du Play Mode laisserait
            // l'éditeur bridé à 60 — ou pire, à 10 s'il a perdu le focus juste avant.
            Application.targetFrameRate = UnlimitedFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }
    }
}
