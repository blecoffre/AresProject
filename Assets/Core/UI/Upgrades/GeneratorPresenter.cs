using Core.Models.Console;
using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Security;
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
        private readonly PrestigeManager _prestige;
        private readonly ThreatManager _threat;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private readonly CompositeDisposable _disposables;

        private readonly bool _isScript;
        private readonly bool _isProxy;

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

        /// <summary>
        /// Nombre de niveaux du lot affiché sur la carte — celui dont le coût est écrit. La ligne
        /// de stats annonce le gain de CE lot-là, pour que le prix et ce qu'il achète se lisent
        /// ensemble.
        /// </summary>
        private int _displayedLevels = 1;

        public GeneratorPresenter(
            UpgradeModel model,
            GeneratorView view,
            UpgradeManager upgradeManager,
            ScriptCycleRunner cycleRunner,
            UserCurrencies currencies,
            BuyQuantitySelector buyQuantity,
            PrestigeManager prestige,
            ThreatManager threat,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _model = model;
            _view = view;
            _upgradeManager = upgradeManager;
            _cycleRunner = cycleRunner;
            _currencies = currencies;
            _buyQuantity = buyQuantity;
            _prestige = prestige;
            _threat = threat;
            _console = console;
            _loc = loc;

            _isScript = model.Config.Type == UpgradeType.Script;
            _isProxy = model.Config.Type == UpgradeType.Proxy;
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

            // TFlops, Noyau ou réduction de coût changés ailleurs : versement, durée, prix et gain
            // de la carte ont pu bouger sans que son niveau change. On invalide le filtre d'achat,
            // qui ne regarde que le solde, le mode et le niveau.
            _model.ExternalFactorsChanged
                .Subscribe(_ =>
                {
                    _lastAffordableLevels = -1;
                    RefreshPurchaseState();
                })
                .AddTo(_disposables);

            // Le répit annoncé par un Proxy inclut la branche Interception. Skip(1) : la valeur
            // courante vient d'être rendue.
            if (_isProxy)
            {
                _prestige.TotalInterceptBonus
                    .Skip(1)
                    .Subscribe(_ => _view.UpdateStats(BuildStatsText()))
                    .AddTo(_disposables);
            }

            // Langue changée à chaud. Skip(1) : tout vient d'être rendu dans la langue courante.
            _loc.CurrentLanguage
                .Skip(1)
                .Subscribe(_ => RefreshLocalizedTexts())
                .AddTo(_disposables);
        }

        private void RefreshLocalizedTexts()
        {
            _view.InitializeStaticData(
                _loc.GetText(_model.Config.DisplayNameKey),
                _loc.GetText(_model.Config.DisplayDescriptionKey),
                BuildStatsText());
            _view.UpdateLevel(_loc.GetText("UI_GENERATOR_LEVEL", _model.CurrentLevel.CurrentValue));

            // Le filtre d'achat ne réécrit rien tant que rien n'a bougé : on l'invalide pour que
            // le coût et le libellé d'achat repassent par la nouvelle langue.
            _lastAffordableLevels = -1;
            RefreshPurchaseState();
        }

        private void BindLevel()
        {
            _model.CurrentLevel
                .Subscribe(level =>
                {
                    _view.UpdateLevel(_loc.GetText("UI_GENERATOR_LEVEL", level));

                    // Le prix du prochain lot et son gain viennent de changer : ils ne s'écrivent
                    // plus ici mais dans le rafraîchissement d'achat, seul endroit qui connaisse
                    // le mode courant. Le niveau ayant bougé, son filtre laisse passer.
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
        /// chaque versement de cycle reformaterait trois chaînes par générateur affiché (coût,
        /// libellé d'achat et, depuis le 2026-10-15, la ligne de stats et son gain), soit plus
        /// d'une centaine d'allocations par tick d'argent. Le calcul en amont, lui, est gratuit
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

            _displayedLevels = quote.Levels;
            _view.UpdateStats(BuildStatsText());

            _view.UpdateCost(
                _loc.GetText("UI_GENERATOR_COST", CurrencyFormatter.FormatCost(quote.TotalCost, _loc.Culture)));

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

        /// <summary>
        /// La ligne de stats : ce que le générateur rend aujourd'hui, et ce qu'y ajoute le lot
        /// affiché (<see cref="_displayedLevels"/>, celui dont le coût est écrit). Depuis le
        /// 2026-10-15 ; avant, un Script ou un Hardware au niveau 0 annonçait « 0 » et rien de ce
        /// qu'il rapporterait.
        /// </summary>
        private string BuildStatsText()
        {
            if (_isScript)
            {
                // Clé DISTINCTE, et non la même avec un argument de plus : GetText passe par
                // string.Format, donc un gabarit portant {2} lèverait une FormatException sur
                // l'appel à deux arguments de l'onglet Hardware.
                // Pour un Script, la donnée utile est le versement et la cadence, pas un débit abstrait.
                // La durée passe brute : son format (« 0.## ») vit dans la clé, et GetText
                // l'applique avec la culture de la langue — « 1,5 s » en français, « 1.5 s » en
                // anglais, quel que soit le Windows du joueur.
                return _loc.GetText(
                    "UI_GENERATES_DATAS_CYCLE",
                    CurrencyFormatter.Format(_model.GetCurrentYield(), _loc.Culture),
                    CurrencyFormatter.Format(_model.GetYieldGainForNextLevels(_displayedLevels), _loc.Culture),
                    _model.GetCurrentCycleDuration());
            }

            // Un Proxy ne produit rien : ce qu'il vaut, c'est le répit qu'il achète. Annoncé en
            // SECONDES et non en UT — c'est ce qu'il rend quelle que soit la vitesse de l'A.M.I.
            // Pour le lot affiché, et non plus un seul niveau : en ×10, la ligne annonçait le
            // répit d'un niveau quand l'achat en effaçait dix. Les paliers franchis sont compris.
            if (_isProxy)
            {
                double seconds = _model.GetWipeSecondsForNextLevels(_displayedLevels)
                               * (1d + _prestige.TotalInterceptBonus.CurrentValue);

                return _loc.GetText("UI_PROXY_WIPE", seconds);
            }

            // Un Hardware ne verse rien : son rendement EST sa capacité, en TFlops brutes. Jusqu'au
            // 2026-10-14 il s'annonçait « Génère X Datas par cycle », la phrase d'un Script.
            return _loc.GetText(
                "UI_GENERATES_TFLOPS",
                CurrencyFormatter.Format(_model.GetCurrentYield(), _loc.Culture),
                CurrencyFormatter.Format(_model.GetYieldGainForNextLevels(_displayedLevels), _loc.Culture));
        }

        private void HandleBuyRequest()
        {
            // Relevé AVANT l'achat : c'est la seule façon de connaître le nombre de niveaux
            // réellement obtenus. Le manager recalcule son lot depuis le solde de l'instant, donc
            // le devis affiché ne fait pas foi — un cycle a pu verser entre les deux.
            int levelBefore = _model.CurrentLevel.CurrentValue;
            float traceBefore = _threat.CurrentTrace.CurrentValue;

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

            // Ce que le Proxy a réellement effacé, lu sur la jauge plutôt que recalculé : la
            // jauge ne descend pas sous zéro, et un achat à vide doit s'annoncer comme tel — c'est
            // la leçon « on achète un Proxy au bon moment ».
            if (_isProxy)
            {
                float wiped = traceBefore - _threat.CurrentTrace.CurrentValue;
                if (wiped < 0f) wiped = 0f;

                _console.Log(_loc.GetText("LOG_PROXY_WIPE", wiped), ConsoleLogType.Success);
            }
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
