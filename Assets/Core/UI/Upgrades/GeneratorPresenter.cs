using Core.Models.Console;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.UI.Game;
using Core.Utils;
using R3;
using System;
using UnityEngine;

namespace Core.UI.Upgrades
{
    public class GeneratorPresenter : IDisposable
    {
        private readonly UpgradeModel _model;
        private readonly GeneratorView _view;
        private readonly UpgradeManager _upgradeManager;
        private readonly ScriptCycleRunner _cycleRunner;
        private readonly UserCurrencies _currencies;
        private readonly BuyQuantitySelector _buyQuantity;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;

        private readonly bool _isScript;

        /// <summary>
        /// Le dernier état d'achat effectivement poussé à la vue : le lot payable, le mode et le
        /// niveau du générateur.
        ///
        /// Ces trois valeurs sont la clé de déduplication du rafraîchissement, et elles sont
        /// toutes les trois nécessaires. Le lot payable seul ne suffit pas : après un achat en
        /// x1, il vaut souvent 1 avant comme après, alors que le PRIX, lui, a changé — la carte
        /// serait restée sur l'ancien montant. Initialisées à des valeurs impossibles pour que le
        /// tout premier rafraîchissement passe.
        /// </summary>
        private int _lastAffordableLevels = -1;
        private int _lastModelLevel = -1;
        private BuyQuantity _lastQuantity = (BuyQuantity)(-1);

        public GeneratorPresenter(
            UpgradeModel model,
            GeneratorView view,
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            UserCurrencies currencies,
            BuyQuantitySelector buyQuantity,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _model = model;
            _view = view;
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _currencies = currencies;
            _buyQuantity = buyQuantity;
            _console = console;
            _loc = loc;

            _isScript = model.Config.Type == UpgradeType.Script;
            _disposables = new CompositeDisposable();

            _view.InitializeStaticData(
                _loc.GetText(_model.Config.DisplayNameKey),
                _loc.GetText(_model.Config.DisplayDescriptionKey),
                BuildStatsText());

            _view.OnBuyClicked += HandleBuyRequest;
            _view.OnRunClicked += HandleRunRequest;

            BindLevel();
            BindPurchaseState();
            BindCycle();
        }

        private void BindLevel()
        {
            _model.CurrentLevel
                .Subscribe(level =>
                {
                    _view.UpdateLevel(_loc.GetText("UI_GENERATOR_LEVEL", level));
                    _view.UpdateStats(BuildStatsText());

                    // Le prix du prochain lot vient de changer : il ne s'écrit plus ici mais dans
                    // le rafraîchissement d'achat, seul endroit qui connaisse le mode courant.
                    RefreshPurchaseState();
                    RefreshRunButton();
                })
                .AddTo(_disposables);
        }

        /// <summary>
        /// Le lot achetable dépend de trois entrées : le solde, le mode d'achat et le niveau du
        /// générateur. La troisième est déjà couverte par BindLevel.
        ///
        /// Trois abonnements vers un point de rafraîchissement unique plutôt qu'un CombineLatest :
        /// celui-ci serait plus court à écrire mais allouerait un tuple à chaque émission, sur une
        /// source réveillée à chaque versement de cycle. Les lambdas, elles, ne sont allouées
        /// qu'une fois, à l'abonnement.
        /// </summary>
        private void BindPurchaseState()
        {
            _currencies.Money.Amount
                .Subscribe(_ => RefreshPurchaseState())
                .AddTo(_disposables);

            _buyQuantity.Current
                .Subscribe(_ => RefreshPurchaseState())
                .AddTo(_disposables);
        }

        /// <summary>
        /// Recalcule le lot que le mode courant permet, et ne réveille la vue que si quelque
        /// chose a réellement bougé.
        ///
        /// Ce filtre remplace le DistinctUntilChanged d'origine et joue le même rôle : sans lui,
        /// chaque versement de cycle reformaterait deux chaînes par générateur affiché, soit
        /// quatre-vingt-dix allocations par tick d'argent. Le calcul en amont, lui, est gratuit
        /// pour les modes fixes — une comparaison contre une somme fermée — et ne paie un
        /// logarithme qu'en MAX.
        /// </summary>
        private void RefreshPurchaseState()
        {
            double money = _currencies.Money.Amount.CurrentValue;
            BuyQuantity quantity = _buyQuantity.Current.CurrentValue;
            int level = _model.CurrentLevel.CurrentValue;

            int affordable = _upgradeManager.GetAffordableLevels(_model.Config.Id, quantity, money);

            if (affordable == _lastAffordableLevels
                && quantity == _lastQuantity
                && level == _lastModelLevel)
            {
                return;
            }

            _lastAffordableLevels = affordable;
            _lastQuantity = quantity;
            _lastModelLevel = level;

            PurchaseQuote quote = _upgradeManager.GetQuote(_model.Config.Id, quantity, money);

            _view.UpdateCost(
                _loc.GetText("UI_GENERATOR_COST", CurrencyFormatter.FormatCost(quote.TotalCost)));

            // Clé DISTINCTE pour le lot, et non la même avec un argument de plus : GetText passe
            // par string.Format, donc un gabarit portant {0} lèverait une FormatException sur
            // l'appel sans argument de l'achat simple.
            _view.UpdateBuyLabel(quote.Levels > 1
                ? _loc.GetText("UI_BUY_BULK", quote.Levels)
                : _loc.GetText("UI_BUY_ONE"));

            _view.SetBuyButtonInteractable(quote.IsAffordable);
        }

        private void BindCycle()
        {
            if (!_isScript) return;

            ReadOnlyReactiveProperty<float> progress = _cycleRunner.GetProgress(_model.Config.Id);
            if (progress == null) return;

            progress
                .Subscribe(_view.UpdateSweepProgress)
                .AddTo(_disposables);

            // Un cycle manuel s'arrête de lui-même à la livraison : c'est ce flux qui rend le
            // bouton de relance cliquable, sans interroger le runner à chaque frame.
            ReadOnlyReactiveProperty<bool> running = _cycleRunner.GetRunning(_model.Config.Id);
            if (running != null)
            {
                running
                    .Subscribe(_ => RefreshRunButton())
                    .AddTo(_disposables);
            }

            if (!_view.HasRunButton)
            {
                Debug.LogWarning(
                    $"[GENERATOR] Le prefab de '{_model.Config.Id}' n'a pas de bouton de lancement câblé : " +
                    "ce Script sera injouable tant qu'il n'est pas automatisé.");
            }

            RefreshRunButton();
        }

        /// <summary>
        /// Le bouton de lancement n'existe que pour les Scripts non encore automatisés, et n'est
        /// cliquable que si le générateur est possédé et qu'aucun cycle ne tourne.
        /// </summary>
        private void RefreshRunButton()
        {
            if (!_isScript) return;

            bool isAutomated = _model.IsAutomated;
            bool isOwned = _model.IsOwned;

            _view.SetRunState(
                isVisible: isOwned && !isAutomated,
                isInteractable: isOwned && !isAutomated && !_cycleRunner.IsRunning(_model.Config.Id));
        }

        private string BuildStatsText()
        {
            if (_isScript)
            {
                // Clé DISTINCTE, et non la même avec un argument de plus : GetText passe par
                // string.Format, donc un gabarit portant {1} lèverait une FormatException sur
                // l'appel à un seul argument des onglets Hardware et Proxy.
                // Pour un Script, la donnée utile est le versement et la cadence, pas un débit abstrait.
                return _loc.GetText(
                    "UI_GENERATES_DATAS_CYCLE",
                    CurrencyFormatter.Format(_model.GetCurrentYield()),
                    _model.GetCurrentCycleDuration().ToString("0.##"));
            }

            return _loc.GetText("UI_GENERATES_DATAS", CurrencyFormatter.Format(_model.GetCurrentYield()));
        }

        private void HandleBuyRequest()
        {
            // Relevé AVANT l'achat : c'est la seule façon de connaître le nombre de niveaux
            // réellement obtenus. Le manager recalcule son lot depuis le solde de l'instant, donc
            // le devis affiché ne fait pas foi — un cycle a pu verser entre les deux.
            int levelBefore = _model.CurrentLevel.CurrentValue;

            if (!_upgradeManager.TryPurchaseUpgrade(_model.Config.Id, _buyQuantity.Current.CurrentValue)) return;

            int levelAfter = _model.CurrentLevel.CurrentValue;
            int gained = levelAfter - levelBefore;

            _console.Log(
                gained > 1
                    ? _loc.GetText(
                        "LOG_UPGRADE_PURCHASED_BULK",
                        _loc.GetText(_model.Config.DisplayNameKey),
                        gained,
                        levelAfter)
                    : _loc.GetText(
                        "LOG_UPGRADE_PURCHASED",
                        _loc.GetText(_model.Config.DisplayNameKey),
                        levelAfter),
                ConsoleLogType.Standard);
        }

        private void HandleRunRequest()
        {
            if (_cycleRunner.TryStartCycle(_model.Config.Id))
            {
                RefreshRunButton();
            }
        }

        public void Dispose()
        {
            _view.OnBuyClicked -= HandleBuyRequest;
            _view.OnRunClicked -= HandleRunRequest;
            _disposables.Dispose();
        }
    }
}
