using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Security;
using Core.Utils;
using R3;
using System;
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

        private DisposableBag _disposables;

        public HeaderPresenter(UserCurrencies userCurrencies, HeaderView view, UpgradeManager upgradeManager,
                               TraceReadout traceReadout, ILocalizationService loc)
        {
            _userCurrencies = userCurrencies;
            _view = view;
            _upgradeManager = upgradeManager;
            _traceReadout = traceReadout;
            _loc = loc;

            _disposables = new DisposableBag();

            _upgradeManager.TotalMoneyYieldPerSecond.Subscribe(yield =>
            {
                string formattedYield = CurrencyFormatter.Format(yield);
                _view.UpdateMoneyYieldDisplay(formattedYield);
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
            BindEconomyToView();
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