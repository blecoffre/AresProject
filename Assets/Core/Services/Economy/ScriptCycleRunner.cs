using Core.Models.Economy;
using Core.Services.Platform;
using Core.Services.Simulation;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Economy
{
    /// <summary>
    /// Moteur de production des Scripts. Chaque Script possède son propre cycle, qui verse
    /// son montant à échéance — et non un tick global plat comme avant.
    ///
    /// Tant que le générateur n'a pas atteint son seuil d'automatisation, le joueur doit lancer
    /// chaque cycle à la main. Au-delà, le cycle se relance seul.
    ///
    /// Les états vivent dans un tableau de structs parcouru par index : aucune allocation, aucune
    /// fermeture et aucun boxing dans Tick(), qui s'exécute à chaque frame.
    /// </summary>
    public class ScriptCycleRunner : IStartable, ITickable, IDisposable
    {
        /// <summary>Garde-fou : une durée nulle ferait boucler l'avancement à l'infini.</summary>
        private const float MinSafeDuration = 0.01f;

        private struct CycleSlot
        {
            public UpgradeModel Model;
            public ReactiveProperty<float> Progress;

            /// <summary>Réactif, pour que la vue sache quand réactiver son bouton de lancement.</summary>
            public ReactiveProperty<bool> Running;

            public float Elapsed;
            public bool IsRunning;
        }

        private readonly UserCurrencies _currencies;
        private readonly UpgradeManager _upgradeManager;
        private readonly GameSessionManager _sessionManager;
        private readonly ITimeSource _time;

        private CycleSlot[] _slots = Array.Empty<CycleSlot>();
        private int _slotCount;

        private readonly Dictionary<string, int> _slotIndexById = new Dictionary<string, int>();

        private DisposableBag _disposables;

        /// <summary>
        /// Trace par seconde des seuls Scripts dont un cycle tourne à cet instant.
        ///
        /// Champ simple et non ReactiveProperty, volontairement : la valeur change à chaque
        /// frame, et notifier ~15 abonnés soixante fois par seconde pour un unique lecteur
        /// (le SimulationTicker) serait du gaspillage pur.
        ///
        /// Elle est rafraîchie à la fin de Tick(). Si le SimulationTicker s'exécute avant ce
        /// runner sur une frame donnée, il lit la valeur de la frame précédente — un décalage
        /// d'une frame sur une jauge qui met des minutes à se remplir, sans conséquence.
        /// </summary>
        public float ActiveScriptTracePerSecond { get; private set; }

        public ScriptCycleRunner(
            UserCurrencies currencies,
            UpgradeManager upgradeManager,
            GameSessionManager sessionManager,
            ITimeSource time)
        {
            _currencies = currencies;
            _upgradeManager = upgradeManager;
            _sessionManager = sessionManager;
            _time = time;
        }

        public void Start()
        {
            // Les modèles sont recréés à chaque chargement de sauvegarde et à chaque wipe :
            // les emplacements doivent suivre, sinon on ferait tourner des modèles morts.
            _upgradeManager.OnUpgradesRebuilt
                .Subscribe(_ => RebuildSlots())
                .AddTo(ref _disposables);

            RebuildSlots();
        }

        /// <summary>
        /// Progression normalisée (0 à 1) du cycle d'un générateur, pour la barre de la vue.
        /// Retourne null si l'identifiant n'est pas un Script.
        /// </summary>
        public ReadOnlyReactiveProperty<float> GetProgress(string upgradeId)
        {
            if (!_slotIndexById.TryGetValue(upgradeId, out int index)) return null;
            return _slots[index].Progress;
        }

        public bool IsRunning(string upgradeId)
        {
            return _slotIndexById.TryGetValue(upgradeId, out int index) && _slots[index].IsRunning;
        }

        /// <summary>
        /// État "un cycle tourne" observable. Indispensable côté vue : un cycle manuel s'arrête
        /// tout seul à la livraison, et le presenter doit rendre le bouton de relance cliquable
        /// sans scruter l'état à chaque frame.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> GetRunning(string upgradeId)
        {
            if (!_slotIndexById.TryGetValue(upgradeId, out int index)) return null;
            return _slots[index].Running;
        }

        /// <summary>
        /// Démarre un cycle à la demande du joueur. Échoue si le générateur n'est pas possédé,
        /// si un cycle tourne déjà, ou si la partie est terminée.
        /// </summary>
        public bool TryStartCycle(string upgradeId)
        {
            if (!_sessionManager.IsGameActive.CurrentValue) return false;
            if (!_slotIndexById.TryGetValue(upgradeId, out int index)) return false;

            if (_slots[index].IsRunning) return false;
            if (!_slots[index].Model.IsOwned) return false;

            _slots[index].IsRunning = true;
            _slots[index].Elapsed = 0f;
            _slots[index].Running.Value = true;
            return true;
        }

        /// <summary>
        /// Avance tous les cycles actifs d'un nombre de secondes donné.
        /// C'est le rôle de l'Overclock manuel : un coup de fouet sur ce qui tourne déjà,
        /// pas un démarrage.
        /// </summary>
        public void AdvanceAllActive(float seconds)
        {
            if (seconds <= 0f) return;
            if (!_sessionManager.IsGameActive.CurrentValue) return;

            for (int i = 0; i < _slotCount; i++)
            {
                if (!_slots[i].IsRunning) continue;
                AdvanceSlot(i, seconds);
            }
        }

        /// <summary>
        /// Démarre d'un coup tous les Scripts POSSÉDÉS dont le cycle est à l'arrêt, et retourne
        /// combien l'ont été. C'est le « réveil » de l'Overclock, réservé au nœud de prestige
        /// tardif qui le débloque.
        ///
        /// Les Scripts automatisés n'y figurent jamais : ils repartent seuls au Tick suivant, et
        /// les compter donnerait un retour trompeur au joueur (« 12 réveillés » alors que rien
        /// n'aurait changé). Boucle indexée, aucune allocation.
        /// </summary>
        public int StartAllIdle()
        {
            if (!_sessionManager.IsGameActive.CurrentValue) return 0;

            int started = 0;

            for (int i = 0; i < _slotCount; i++)
            {
                if (_slots[i].IsRunning) continue;
                if (!_slots[i].Model.IsOwned) continue;
                if (_slots[i].Model.IsAutomated) continue;

                _slots[i].IsRunning = true;
                _slots[i].Elapsed = 0f;
                _slots[i].Running.Value = true;
                started++;
            }

            return started;
        }

        public void Tick()
        {
            // Les cycles se figent pendant l'écran de fin de run, et repartent de zéro ensuite
            // (RebuildSlots remet Elapsed à 0).
            if (!_sessionManager.IsGameActive.CurrentValue) return;

            float deltaTime = _time.DeltaTime;
            float activeTrace = 0f;

            for (int i = 0; i < _slotCount; i++)
            {
                if (!_slots[i].IsRunning)
                {
                    // Un Script qui vient de franchir son seuil d'automatisation — par niveau ou
                    // par nœud de prestige — repart tout seul, sans abonnement à maintenir.
                    if (_slots[i].Model.IsOwned && _slots[i].Model.IsAutomated)
                    {
                        _slots[i].IsRunning = true;
                        _slots[i].Elapsed = 0f;
                        _slots[i].Running.Value = true;
                    }
                    else
                    {
                        continue;
                    }
                }

                // Un Script ne laisse de trace que TANT QU'IL TOURNE : l'A.M.I. ne repère le
                // piratage que lorsqu'il est actif. Le cumul se fait dans la boucle qu'on
                // parcourt déjà — un simple accumulateur float, aucune allocation.
                activeTrace += _slots[i].Model.GetTraceMagnitudePerSecond();

                AdvanceSlot(i, deltaTime);
            }

            ActiveScriptTracePerSecond = activeTrace;
        }

        private void AdvanceSlot(int index, float seconds)
        {
            UpgradeModel model = _slots[index].Model;
            float duration = Mathf.Max(MinSafeDuration, model.GetCurrentCycleDuration());
            float elapsed = _slots[index].Elapsed + seconds;

            // Boucle plutôt que simple test : à haute vitesse (ou après un gros Overclock),
            // plusieurs cycles peuvent s'achever dans la même frame. Les perdre reviendrait à
            // plafonner silencieusement la production.
            while (elapsed >= duration)
            {
                elapsed -= duration;
                _currencies.AddMoney(model.GetCurrentYield());

                if (!model.IsAutomated)
                {
                    // Cycle manuel : il s'arrête à la livraison, le joueur doit le relancer.
                    _slots[index].IsRunning = false;
                    _slots[index].Elapsed = 0f;
                    _slots[index].Progress.Value = 0f;
                    _slots[index].Running.Value = false;
                    return;
                }

                duration = Mathf.Max(MinSafeDuration, model.GetCurrentCycleDuration());
            }

            _slots[index].Elapsed = elapsed;
            _slots[index].Progress.Value = elapsed / duration;
        }

        private void RebuildSlots()
        {
            ReleaseSlots();

            IReadOnlyList<UpgradeModel> scripts = _upgradeManager.GetUpgradesOfType(UpgradeType.Script);

            if (_slots.Length < scripts.Count)
            {
                _slots = new CycleSlot[scripts.Count];
            }

            _slotCount = scripts.Count;

            for (int i = 0; i < scripts.Count; i++)
            {
                UpgradeModel model = scripts[i];

                _slots[i] = new CycleSlot
                {
                    Model = model,
                    Progress = new ReactiveProperty<float>(0f),
                    // Un Script déjà automatisé au chargement repart immédiatement.
                    Running = new ReactiveProperty<bool>(model.IsOwned && model.IsAutomated),
                    Elapsed = 0f,
                    IsRunning = model.IsOwned && model.IsAutomated
                };

                _slotIndexById[model.Config.Id] = i;
            }
        }

        private void ReleaseSlots()
        {
            for (int i = 0; i < _slotCount; i++)
            {
                _slots[i].Progress?.Dispose();
                _slots[i].Running?.Dispose();
                _slots[i] = default;
            }

            _slotCount = 0;
            _slotIndexById.Clear();

            // Sans cette remise à zéro, la Trace des cycles d'AVANT le wipe continuerait à
            // remplir la jauge tant qu'aucune frame n'a recalculé l'accumulateur.
            ActiveScriptTracePerSecond = 0f;
        }

        public void Dispose()
        {
            _disposables.Dispose();
            ReleaseSlots();
        }
    }
}
