using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Platform;
using Core.Services.Security;
using Core.Services.Simulation;
using R3;
using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Core.Services.Audio
{
    /// <summary>
    /// Les sons qui ne viennent pas d'un clic : la Saisie Fédérale, la victoire, l'alerte quand
    /// la jauge franchit un palier d'extraction, et l'Overclock critique (2026-10-16) — il suit
    /// bien un clic, mais c'est le hasard de l'arbre qu'il annonce, pas le bouton.
    ///
    /// Vit dans le Root, à côté des systèmes qu'il écoute : il les observe en lecture seule et
    /// ne décide de rien. Ses sources sont distinctes de celles de l'interface (une rafale de
    /// clics ne coupe pas une alerte) et passent devant elles sous la limite de voix.
    ///
    /// <b>Les paliers s'écoutent sur la jauge RELEVÉE (<see cref="TraceReadout"/>), jamais sur la
    /// vérité du <see cref="ThreatManager"/>.</b> Le brouillard du capteur est une mécanique de
    /// jeu : une alerte réglée sur la vraie valeur annoncerait le franchissement avant que
    /// l'aiguille ne le montre, et rendrait au joueur l'information que le capteur lui retire.
    /// L'alerte sonne donc au même instant que le palier s'allume à l'écran.
    /// </summary>
    public sealed class GameEventSounds : IStartable, IDisposable
    {
        private const int VoiceCount = 2;

        // Au-dessus des clics (128), sous la musique (0).
        private const int Priority = 64;

        private readonly AudioLibrarySO _library;
        private readonly AudioHost _host;
        private readonly ThreatManager _threat;
        private readonly VictorySystem _victory;
        private readonly TraceReadout _readout;
        private readonly BalancingConfigSO _balancing;
        private readonly PrestigeManager _prestige;
        private readonly OverclockSystem _overclock;
        private readonly ITimeSource _time;

        private SoundSetVoices _voices;
        private DisposableBag _disposables;

        // Nombre de paliers DÉJÀ franchis par la jauge relevée. Une alerte ne sonne que quand il
        // augmente ; il redescend avec la jauge (Data Wiper, nouvelle run), si bien qu'un palier
        // franchi à nouveau après un wipe se signale à nouveau.
        private int _tiersReached;

        public GameEventSounds(AudioLibrarySO library, AudioHost host, ThreatManager threat,
                               VictorySystem victory, TraceReadout readout,
                               BalancingConfigSO balancing, PrestigeManager prestige,
                               OverclockSystem overclock, ITimeSource time)
        {
            _library = library;
            _host = host;
            _threat = threat;
            _victory = victory;
            _readout = readout;
            _balancing = balancing;
            _prestige = prestige;
            _overclock = overclock;
            _time = time;
        }

        public void Start()
        {
            _voices = new SoundSetVoices(_host, "Event", _library.SfxGroup, VoiceCount, Priority);
            _disposables = new DisposableBag();

            _threat.OnCriticalLockdown.Subscribe(this, (_, self) => self._voices.Play(self._library.SeizureSet))
                   .AddTo(ref _disposables);
            _victory.OnVictory.Subscribe(this, (_, self) => self._voices.Play(self._library.VictorySet))
                    .AddTo(ref _disposables);

            _overclock.OnApplied.Subscribe(this, (result, self) =>
                      {
                          if (result.IsCritical) self._voices.Play(self._library.OverclockCriticalSet);
                      })
                      .AddTo(ref _disposables);

            // Émet la valeur courante tout de suite : sert de point de départ, sans alerte.
            _readout.LastKnownFraction.Subscribe(this, (fraction, self) => self.HandleTraceReading(fraction))
                    .AddTo(ref _disposables);
        }

        /// <summary>
        /// Appelé à chaque nouveau relevé, donc pendant la boucle de frame : aucune allocation,
        /// les paliers sont parcourus par index.
        /// </summary>
        private void HandleTraceReading(float fraction)
        {
            int reached = CountTiersReached(fraction);
            int previous = _tiersReached;
            _tiersReached = reached;

            if (reached <= previous) return;

            // Un relevé ne fait pas qu'avancer pendant la partie : il est aussi recalé d'un coup à
            // la restauration d'une sauvegarde, pendant que la simulation est retenue (menu,
            // démarrage). Ce saut n'est pas un franchissement — il ne doit pas sonner.
            if (_time.DeltaTime <= 0f) return;

            // À 100 %, c'est la saisie qui parle : pas d'alerte de palier par-dessus.
            if (fraction >= 1f) return;

            // Plusieurs paliers franchis d'un seul relevé (capteur qui bégaie quand la jauge
            // s'emballe) : on ne joue que le plus haut, le plus grave.
            _voices.Play(_library.GetTraceTierSet(reached - 1));
        }

        /// <summary>
        /// Le nombre de paliers libres que la fraction a atteints. Un palier verrouillé
        /// (« Extraction Haut Risque » non achetée) ne verse rien et s'affiche comme tel : il ne
        /// s'annonce pas non plus. L'index rendu reste celui du palier dans la liste, pour que
        /// chaque palier garde son propre son.
        /// </summary>
        private int CountTiersReached(float fraction)
        {
            IReadOnlyList<CleanExitTier> tiers = _balancing.CleanExitTiers;
            if (tiers == null) return 0;

            bool highRiskUnlocked = _prestige.IsHighRiskExtractionUnlocked.CurrentValue;
            int reached = 0;

            for (int i = 0; i < tiers.Count; i++)
            {
                CleanExitTier tier = tiers[i];
                if (fraction < tier.TraceThreshold) break;
                if (tier.RequiresUnlock && !highRiskUnlocked) continue;
                reached = i + 1;
            }

            return reached;
        }

        public void Dispose() => _disposables.Dispose();
    }
}
