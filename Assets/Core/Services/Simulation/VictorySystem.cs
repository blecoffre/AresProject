using Core.Models.Economy;
using Core.Services.Economy;
using R3;
using System;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// La condition de victoire : atteindre <see cref="BalancingConfigSO.VictoryTFlops"/>.
    ///
    /// <b>Pourquoi la capacité de calcul et pas le dernier Hardware.</b> Le critère s'est d'abord
    /// jugé sur « le dernier Hardware est débloqué », ce qui tombait au premier exemplaire acheté —
    /// au moment où le joueur DÉCOUVRE la machine, pas au moment où il en a fait quelque chose. Le
    /// lore est le hack d'une clé USB surpuissante : la fin se mesure donc en TFlops.
    ///
    /// <b>La victoire n'arrête pas la partie.</b> Elle se déclenche une fois, déroule son moment,
    /// et le joueur reprend où il en était. C'est un incrémental : il lui reste presque toujours de
    /// l'arbre de prestige à finir, et figer la partie au meilleur moment de sa courbe lui
    /// retirerait ce qu'il vient tout juste de gagner.
    ///
    /// <b>Service du scope RACINE.</b> La victoire appartient à la campagne, pas à l'affichage :
    /// elle survit au rechargement de la GameScene, et elle est sauvegardée.
    /// </summary>
    public sealed class VictorySystem : IStartable, IDisposable
    {
        private readonly UpgradeManager _upgrades;
        private readonly BalancingConfigSO _balancing;

        private readonly ReactiveProperty<bool> _hasWon = new(false);
        private readonly Subject<Unit> _onVictory = new();

        private DisposableBag _disposables;

        /// <summary>
        /// Vrai dès que le seuil a été atteint une fois, pour toute la durée de la sauvegarde.
        /// Monotone : rien ne la fait redescendre, pas même un wipe — c'est de la
        /// méta-progression.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> HasWon => _hasWon;

        /// <summary>
        /// Émis UNE seule fois, à l'instant où le seuil est franchi. Les presenters s'y abonnent
        /// pour dérouler la séquence ; l'état durable, lui, se lit dans <see cref="HasWon"/>.
        /// </summary>
        public Observable<Unit> OnVictory => _onVictory;

        public VictorySystem(UpgradeManager upgrades, BalancingConfigSO balancing)
        {
            _upgrades = upgrades;
            _balancing = balancing;
        }

        /// <summary>
        /// L'abonnement se prend ici et non dans le constructeur : un constructeur appelé par le
        /// conteneur ne doit pas avoir d'effets de bord.
        ///
        /// <b>Aucun coût par frame.</b> <c>TotalTFlops</c> ne bouge qu'au recalcul des totaux —
        /// achat de Hardware, bonus de prestige, tranche immobilisée par le Data Wiper — donc
        /// jamais dans la boucle de jeu. Scruter le seuil dans un Tick aurait coûté une comparaison
        /// par frame pour un événement qui survient une fois par partie.
        /// </summary>
        public void Start()
        {
            _upgrades.TotalTFlops
                     .Subscribe(Evaluate)
                     .AddTo(ref _disposables);
        }

        private void Evaluate(double totalTFlops)
        {
            if (_hasWon.CurrentValue) return;

            double target = _balancing.VictoryTFlops;
            if (target <= 0d || totalTFlops < target) return;

            _hasWon.Value = true;
            _onVictory.OnNext(Unit.Default);

            UnityEngine.Debug.Log(
                $"[Victory] Seuil atteint : {totalTFlops:0.###e+0} TFlops (cible {target:0.###e+0}).");
        }

        /// <summary>
        /// Restauré depuis la sauvegarde.
        ///
        /// ⚠️ <b>Doit être appelé AVANT <c>UpgradeManager.InitializeFromSave</c>.</b> Celui-ci
        /// recalcule les totaux, donc pousse une valeur dans <c>TotalTFlops</c> : sur une partie
        /// déjà gagnée, l'abonnement ci-dessus rejouerait la victoire à chaque chargement si le
        /// drapeau n'était pas déjà posé. Voir l'ordre dans <c>GameStateGateway.Restore</c>.
        ///
        /// Ne prend jamais la valeur FAUX : une victoire acquise ne se reprend pas, et un fichier
        /// abîmé ne doit pas pouvoir l'effacer.
        /// </summary>
        public void Restore(bool hasWon)
        {
            if (hasWon) _hasWon.Value = true;
        }

        /// <summary>Lu par le gateway à la capture. Voir la règle d'or de <c>persistence.md</c>.</summary>
        public bool Capture() => _hasWon.CurrentValue;

        public void Dispose()
        {
            _disposables.Dispose();
            _onVictory.Dispose();
            _hasWon.Dispose();
        }
    }
}
