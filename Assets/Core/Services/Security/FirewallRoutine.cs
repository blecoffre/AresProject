using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Simulation;
using R3;
using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Core.Services.Security
{
    /// <summary>Ce que la Routine Pare-Feu vient d'acheter, pour que la console le raconte.</summary>
    public readonly struct FirewallPurchase
    {
        /// <summary>Clé de localisation du nom du Proxy acheté.</summary>
        public readonly string ProxyNameKey;

        /// <summary>UT réellement retirées de la jauge.</summary>
        public readonly float TraceWiped;

        public FirewallPurchase(string proxyNameKey, float traceWiped)
        {
            ProxyNameKey = proxyNameKey;
            TraceWiped = traceWiped;
        }
    }

    /// <summary>
    /// La Routine Pare-Feu (`P_FIREWALL_ROUTINE`, GD, 2026-10-16) : quand la jauge franchit 95 %,
    /// le système achète d'office le Proxy le plus cher que le joueur peut s'offrir. Une
    /// assurance-vie pour la toute fin de campagne, quand la run va plus vite que le joueur.
    ///
    /// <b>Parmi les Proxies VISIBLES seulement</b> — ceux déjà possédés, et le suivant que leur
    /// achat a révélé. Le système ne prend pas, à la place du joueur, une machine que celui-ci
    /// n'a jamais vue dans sa liste.
    ///
    /// <b>Un achat par passage au-dessus de 95 %</b> : la routine se désarme en achetant, et ne se
    /// réarme qu'une fois la jauge repassée dessous. Un Proxy trop faible pour la redescendre ne
    /// déclenche donc pas une rafale d'achats qui viderait le portefeuille, frame après frame.
    /// Faute d'argent, elle reste armée et réessaie à chaque montée : des Datas versées une
    /// seconde plus tard peuvent encore sauver la run. Une partie rechargée au-dessus du seuil
    /// est couverte de la même façon.
    ///
    /// ⚠️ Elle joue CONTRE l'État d'Alerte et le palier de sortie des 90 % : purger à 95 % fait
    /// perdre les deux. C'est le prix de l'assurance, assumé tel quel (consigné au GDD).
    /// </summary>
    public sealed class FirewallRoutine : IStartable, IDisposable
    {
        private readonly ThreatManager _threat;
        private readonly PrestigeManager _prestige;
        private readonly UpgradeManager _upgrades;
        private readonly UserCurrencies _currencies;
        private readonly GameSessionManager _session;

        private readonly Subject<FirewallPurchase> _onTriggered = new();
        private DisposableBag _disposables;

        /// <summary>Prête à acheter : vrai tant qu'aucun achat n'a eu lieu depuis le dernier passage sous le seuil.</summary>
        private bool _armed = true;

        public FirewallRoutine(
            ThreatManager threat,
            PrestigeManager prestige,
            UpgradeManager upgrades,
            UserCurrencies currencies,
            GameSessionManager session)
        {
            _threat = threat;
            _prestige = prestige;
            _upgrades = upgrades;
            _currencies = currencies;
            _session = session;
        }

        /// <summary>Émis après chaque achat d'office, pour la console de la scène de jeu.</summary>
        public Observable<FirewallPurchase> OnTriggered => _onTriggered;

        public void Start()
        {
            _threat.OnRaisedAboveAutoProxyThreshold
                .Subscribe(this, (_, self) => self.TryPurge())
                .AddTo(ref _disposables);

            // Réarmement dès que la jauge repasse sous le seuil, quelle qu'en soit la cause :
            // l'achat lui-même, un Proxy du joueur, le Data Wiper, une nouvelle run. Une
            // comparaison par variation, sans allocation.
            _threat.CurrentTrace
                .Subscribe(this, (trace, self) => { if (trace < self._threat.AutoProxyThreshold) self._armed = true; })
                .AddTo(ref _disposables);
        }

        private void TryPurge()
        {
            if (!_armed) return;
            if (!_prestige.IsAutoProxyUnlocked.CurrentValue) return;
            if (!_session.IsGameActive.CurrentValue) return;

            UpgradeModel best = FindMostExpensiveAffordableProxy(_currencies.Money.Amount.CurrentValue);
            if (best == null) return;

            float traceBefore = _threat.CurrentTrace.CurrentValue;

            // Désarmée AVANT l'achat : son Wipe redescend la jauge, et c'est cette descente qui la
            // réarme (abonnement ci-dessus). Désarmer après écraserait ce réarmement.
            _armed = false;

            // L'achat passe par le chemin normal : débit, niveau, révélation du Proxy suivant et
            // Wipe de la jauge (OnProxyWipe), Interception comprise.
            if (!_upgrades.TryPurchaseUpgrade(best.Config.Id, BuyQuantity.X1))
            {
                _armed = true;
                return;
            }

            float wiped = traceBefore - _threat.CurrentTrace.CurrentValue;
            _onTriggered.OnNext(new FirewallPurchase(best.Config.DisplayNameKey, wiped > 0f ? wiped : 0f));
        }

        /// <summary>
        /// Le Proxy visible dont le prochain niveau coûte le plus, parmi ceux qu'on peut payer.
        /// La liste est triée par ordre d'apparition : on s'arrête au premier jamais acheté, qui
        /// est le dernier visible. Aucune allocation.
        /// </summary>
        private UpgradeModel FindMostExpensiveAffordableProxy(double money)
        {
            IReadOnlyList<UpgradeModel> proxies = _upgrades.GetUpgradesOfType(UpgradeType.Proxy);

            UpgradeModel best = null;
            double bestCost = -1d;

            for (int i = 0; i < proxies.Count; i++)
            {
                UpgradeModel model = proxies[i];
                double cost = model.GetCumulativeCost(1);

                if (cost <= money && cost > bestCost)
                {
                    best = model;
                    bestCost = cost;
                }

                if (!model.IsOwned) break;
            }

            return best;
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _onTriggered.Dispose();
        }
    }
}
