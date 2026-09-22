using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Security;
using Core.Utils;
using R3;
using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Core.UI.Header
{
    public class HeaderPresenter : IStartable, IDisposable
    {
        private readonly UserCurrencies _userCurrencies;
        private readonly HeaderView _view;
        private readonly UpgradeManager _upgradeManager;
        private readonly TraceReadout _traceReadout;
        private readonly ILocalizationService _loc;
        private readonly BalancingConfigSO _balancing;
        private readonly PrestigeManager _prestige;

        private DisposableBag _disposables;

        // Tampons réutilisés : le nombre de paliers est un réglage, il ne bouge pas en cours de
        // partie. Les recomposer à chaque achat de prestige suffit largement.
        private float[] _tierThresholds;
        private string[] _tierLabels;
        private bool[] _tierLocked;

        public HeaderPresenter(UserCurrencies userCurrencies, HeaderView view, UpgradeManager upgradeManager,
                               TraceReadout traceReadout, ILocalizationService loc,
                               BalancingConfigSO balancing, PrestigeManager prestige)
        {
            _userCurrencies = userCurrencies;
            _view = view;
            _upgradeManager = upgradeManager;
            _traceReadout = traceReadout;
            _loc = loc;
            _balancing = balancing;
            _prestige = prestige;

            _disposables = new DisposableBag();

            // Le « + … /s » se compose ICI, pas dans la vue : c'est le presenter qui tient la
            // localisation. La vue n'a plus qu'à poser la chaîne.
            _upgradeManager.TotalMoneyYieldPerSecond.Subscribe(yield =>
            {
                string formattedYield = CurrencyFormatter.Format(yield);
                _view.UpdateMoneyYieldDisplay(_loc.GetText("UI_YIELD_PER_SECOND", formattedYield));
            })
            .AddTo(ref _disposables);

            // Capacité de calcul dérivée, et non plus une monnaie accumulée.
            _upgradeManager.TotalTFlops.Subscribe(val =>
            {
                _view.UpdateComputerPowerDisplay(CurrencyFormatter.Format(val));
            })
            .AddTo(ref _disposables);

            // L'aiguille suit le RELEVÉ, pas la vérité. Le ThreatManager n'est plus lu ici : la
            // logique du jeu garde sa jauge exacte, l'affichage n'a droit qu'à un capteur qui
            // retarde. C'est toute la frontière du modèle de risque.
            //
            // Deux abonnements et non un : le libellé ne se recompose qu'aux relevés — rares, et
            // seuls à allouer une chaîne — tandis que le bord de la fourchette bouge à chaque
            // frame et ne coûte qu'une largeur.
            _traceReadout.LastKnownFraction.Subscribe(RenderReadout)
                .AddTo(ref _disposables);

            _traceReadout.EstimatedMaxFraction.Subscribe(_view.UpdateTraceBand)
                .AddTo(ref _disposables);
        }

        public void Start()
        {
            // L'unité est un texte statique : une seule écriture, pas un LocalizedText de plus
            // à ne pas oublier dans autoInjectGameObjects.
            _view.SetComputerPowerUnit(_loc.GetText("UI_UNIT_TFLOPS"));
            _view.SetCpuCyclesUnit(_loc.GetText("CYCLES"));

            BindEconomyToView();

            // Les paliers se recomposent à l'achat d'un nœud de prestige, jamais par frame : leurs
            // libellés allouent des chaînes, et ni le multiplicateur d'Extraction ni le déblocage
            // du palier haut ne bougent pendant une run.
            _prestige.CleanExitBonusMultiplier
                     .Subscribe(_ => RebuildTierDisplay())
                     .AddTo(ref _disposables);

            _prestige.IsHighRiskExtractionUnlocked
                     .Subscribe(_ => RebuildTierDisplay())
                     .AddTo(ref _disposables);
        }

        /// <summary>
        /// Compose les paliers affichés sur la jauge.
        ///
        /// <b>Le bonus annoncé est l'EFFECTIF, pas le nominal.</b> La branche Extraction multiplie
        /// les paliers jusqu'à ×7 : afficher « +15 % » à un joueur qui en touche 105 lui cacherait
        /// sa propre progression, et c'est précisément ce multiplicateur qui rend la branche
        /// désirable. Il doit donc se lire sur la jauge.
        /// </summary>
        private void RebuildTierDisplay()
        {
            IReadOnlyList<CleanExitTier> tiers = _balancing.CleanExitTiers;
            if (tiers == null || tiers.Count == 0) return;

            if (_tierThresholds == null || _tierThresholds.Length != tiers.Count)
            {
                _tierThresholds = new float[tiers.Count];
                _tierLabels = new string[tiers.Count];
                _tierLocked = new bool[tiers.Count];
            }

            float multiplier = _prestige.CleanExitBonusMultiplier.CurrentValue;
            bool highRiskUnlocked = _prestige.IsHighRiskExtractionUnlocked.CurrentValue;

            for (int i = 0; i < tiers.Count; i++)
            {
                CleanExitTier tier = tiers[i];
                bool locked = tier.RequiresUnlock && !highRiskUnlocked;

                _tierThresholds[i] = tier.TraceThreshold;
                _tierLocked[i] = locked;

                // Le SEUIL n'est plus écrit : il est dit par la position du trait sur la jauge,
                // et le répéter sous chaque trait faisait déborder les libellés les uns sur les
                // autres — 75 % et 90 % ne sont séparés que d'une centaine de pixels. Ne reste
                // que ce que la position ne peut pas dire : ce que le palier rapporte.
                //
                // Un palier VERROUILLÉ n'annonce aucun gain : il n'en verse aucun tant que son
                // nœud n'est pas installé, et afficher le montant — même grisé — se lirait comme
                // une promesse. Il porte le marqueur d'état du jeu, « [VERROUILLÉ] », le même que
                // le Ghost Cache, le Data Wiper et l'Exfiltration.
                //
                // Le nœud qui l'ouvre est nommé dans l'ARBRE, pas ici : ces libellés-là le font
                // en toutes lettres, mais un libellé de trait dispose de 130 px entre son voisin
                // de 75 % et le bout de la jauge — « [VERROUILLÉ] Extraction Haut Risque » en
                // demande 250.
                _tierLabels[i] = locked
                    ? _loc.GetText("UI_TRACE_TIER_LOCKED")
                    : _loc.GetText("UI_TRACE_TIER", tier.Bonus * multiplier * 100d);
            }

            _view.ConfigureTraceTiers(_tierThresholds, _tierLabels, _tierLocked);
        }

        private void BindEconomyToView()
        {
            // Dès que Datas.Amount change, on le formate et on l'envoie à la vue.
            _userCurrencies.Money.Amount
                .Subscribe(val => _view.UpdateMoneyDisplay(CurrencyFormatter.Format(val)))
                .AddTo(ref _disposables);


            // Et les Cycles CPU
            _userCurrencies.CpuCycles.Amount
                .Subscribe(val => _view.UpdateCpuCyclesDisplay(CurrencyFormatter.Format(val)))
                .AddTo(ref _disposables);
        }

        /// <summary>
        /// Compose le libellé de la jauge. Sous une seconde d'âge le relevé est tenu pour vivant
        /// et l'âge est masqué : afficher « il y a 0 s » en permanence n'apprendrait rien et
        /// banaliserait la mention, qui doit rester le signal que l'information a vieilli.
        /// </summary>
        private void RenderReadout(float lastKnownFraction)
        {
            float age = _traceReadout.SecondsSinceSample;

            string label = age < 1f
                ? _loc.GetText("UI_TRACE_READOUT_LIVE", lastKnownFraction * 100f)
                : _loc.GetText("UI_TRACE_READOUT", lastKnownFraction * 100f, age);

            _view.UpdateTraceDisplay(label, lastKnownFraction);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}