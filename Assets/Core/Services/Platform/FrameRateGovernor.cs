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
    /// La VSync est coupée, et ce n'est PAS une négligence : tant que
    /// <see cref="QualitySettings.vSyncCount"/> est non nul, <see cref="Application.targetFrameRate"/>
    /// est purement ignoré sur desktop, et la cadence reste collée au taux de rafraîchissement
    /// de l'écran. Les deux réglages s'excluent — la documentation de Unity l'impose dans son
    /// propre exemple. Sur une interface 2D, l'absence de VSync ne produit aucun déchirement
    /// perceptible : il n'y a pas de mouvement de caméra pour le révéler.
    ///
    /// Hors focus, la SIMULATION continue de tourner — c'est <c>runInBackground</c>, réglé côté
    /// Player Settings, qui le garantit, et la Trace continue donc de monter pendant qu'on
    /// consulte autre chose. Seul le RENDU se calme. La distinction est le cœur du réglage :
    /// on ne met pas la partie en pause, on cesse juste de la dessiner pour rien.
    /// </summary>
    public class FrameRateGovernor : IStartable, IDisposable
    {
        /// <summary>Cadence fenêtre active. 60 suffit très largement pour de l'interface.</summary>
        private const int FocusedFrameRate = 60;

        /// <summary>
        /// Cadence fenêtre inactive. Assez bas pour rendre la main au système, assez haut pour
        /// que le retour à la fenêtre soit instantané plutôt que saccadé.
        /// </summary>
        private const int UnfocusedFrameRate = 10;

        /// <summary>Aucune limite : la valeur de repos de Unity, restaurée à la destruction.</summary>
        private const int UnlimitedFrameRate = -1;

        private int _previousVSyncCount;
        private bool _isHooked;

        public void Start()
        {
            // Mémorisé AVANT d'écraser : sans ça, quitter le Play Mode laisserait l'éditeur
            // avec la VSync coupée jusqu'au prochain redémarrage de Unity. Ces deux propriétés
            // sont statiques et survivent au scope, elles ne s'oublient pas toutes seules.
            _previousVSyncCount = QualitySettings.vSyncCount;

            // Forcé ici plutôt que laissé au seul QualitySettings : le niveau de qualité peut
            // être changé au runtime, et il réimposerait alors sa propre VSync.
            QualitySettings.vSyncCount = 0;

            // Cadence pleine au démarrage, SANS consulter Application.isFocused — et c'est
            // délibéré. Au premier frame le focus n'est pas encore établi et isFocused peut
            // répondre false alors que la fenêtre s'apprête à le prendre : partir sur la valeur
            // hors-focus figerait le jeu à 10 images par seconde, et aucun focusChanged ne
            // viendrait le rattraper puisqu'il n'y aurait, du point de vue de Unity, aucun
            // CHANGEMENT à signaler. Le défaut symétrique — un jeu lancé en arrière-plan qui
            // tourne à 60 jusqu'au premier basculement — ne coûte que quelques secondes de
            // rendu inutile. On préfère ce défaut-là.
            Application.targetFrameRate = FocusedFrameRate;

            Application.focusChanged += HandleFocusChanged;
            _isHooked = true;
        }

        /// <summary>
        /// Une simple affectation d'entier statique : ni allocation, ni boxing, ni closure.
        /// L'abonnement lui-même n'alloue son délégué qu'une fois, au démarrage.
        /// </summary>
        private void HandleFocusChanged(bool hasFocus)
        {
            Application.targetFrameRate = hasFocus ? FocusedFrameRate : UnfocusedFrameRate;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PERF] Fenêtre {(hasFocus ? "active" : "inactive")} — cadence plafonnée à {Application.targetFrameRate} FPS.");
#endif
        }

        public void Dispose()
        {
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
