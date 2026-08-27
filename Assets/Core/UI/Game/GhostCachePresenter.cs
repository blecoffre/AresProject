using Core.Models.Console;
using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Simulation;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Pilote le bouton du Ghost Cache et raconte le Zéro-Day Exploit dans la console.
    ///
    /// Trois états, et la jauge change de SENS entre eux : elle se remplit pendant
    /// l'accumulation, puis se vide pendant l'Exploit. C'est volontaire — le joueur lit la même
    /// barre comme une réserve puis comme un compte à rebours, et la couleur marque la bascule.
    /// </summary>
    public class GhostCachePresenter : IStartable, IDisposable
    {
        private readonly GhostCacheView _view;
        private readonly GhostCacheSystem _ghostCache;
        private readonly PrestigeManager _prestige;
        private readonly ConsolePresenter _console;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;

        public GhostCachePresenter(
            GhostCacheView view,
            GhostCacheSystem ghostCache,
            PrestigeManager prestige,
            ConsolePresenter console,
            ILocalizationService loc)
        {
            _view = view;
            _ghostCache = ghostCache;
            _prestige = prestige;
            _console = console;
            _loc = loc;
        }

        public void Start()
        {
            _view.OnOverdriveClicked += HandleClicked;

            // Les trois sources sont ramenées à un ENTIER avant d'atteindre le presenter. Sans
            // ces filtres, le libellé serait reconstruit — donc une chaîne allouée — à chaque
            // frame pendant les cinq minutes de charge puis les trente secondes d'Exploit.
            _ghostCache.ChargeSeconds
                .Select(seconds => Mathf.RoundToInt(seconds / _ghostCache.CapacitySeconds * 100f))
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Les TROIS nœuds de l'Exploit changent ce qu'affiche le bouton : le déblocage et la
            // capacité, le multiplicateur de rendement, le malus de Trace. On écoute donc le
            // signal de recalcul global plutôt que chaque propriété — s'abonner à la seule
            // capacité laissait le bouton annoncer ×50 alors que le joueur venait d'acheter
            // les dix rangs qui le portent à ×100.
            _prestige.OnBonusesRecalculated
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            _ghostCache.OverdriveRemaining
                .Select(Mathf.CeilToInt)
                .DistinctUntilChanged()
                .Subscribe(_ => Refresh())
                .AddTo(ref _disposables);

            // Seul abonnement qui fait autre chose que rafraîchir : c'est lui qui porte la
            // narration. Il couvre les deux bords de l'Exploit, y compris une fin provoquée par
            // un wipe — le joueur doit savoir que ses Proxies sont revenus en ligne.
            _ghostCache.IsOverdriveActive
                .Skip(1)
                .Subscribe(HandleOverdriveToggled)
                .AddTo(ref _disposables);

            Refresh();
        }

        private void HandleOverdriveToggled(bool isActive)
        {
            _console.Log(
                _loc.GetText(isActive ? "LOG_OVERDRIVE_START" : "LOG_OVERDRIVE_END"),
                ConsoleLogType.Narrative);

            Refresh();
        }

        private void Refresh()
        {
            if (_ghostCache.IsOverdriveActive.CurrentValue)
            {
                float remaining = _ghostCache.OverdriveRemaining.CurrentValue;

                _view.ApplyState(
                    _loc.GetText("UI_GHOSTCACHE_ACTIVE", Mathf.CeilToInt(remaining)),
                    interactable: false,
                    fillAmount: remaining / _ghostCache.OverdriveDurationSeconds,
                    fillColor: _view.OverdriveColor);

                return;
            }

            // Verrouillé tant que `P_EXPLOIT_CHARGES` n'a pas été acheté. État à part entière et
            // non un bouton grisé muet : le joueur doit savoir OÙ aller le débloquer, sinon il
            // regarde une barre inerte sans comprendre.
            if (!_ghostCache.IsUnlocked)
            {
                _view.ApplyState(
                    _loc.GetText("UI_GHOSTCACHE_LOCKED"),
                    interactable: false,
                    fillAmount: 0f,
                    fillColor: _view.ChargingColor);

                return;
            }

            if (_ghostCache.IsReady)
            {
                // Les quatre nombres viennent des constantes et du prestige, jamais du texte :
                // sinon un rééquilibrage laisserait le bouton mentir au joueur.
                _view.ApplyState(
                    _loc.GetText(
                        "UI_GHOSTCACHE_READY",
                        _ghostCache.AvailableCharges,
                        // Valeurs BRUTES : le gabarit les met en forme avec « 0.# », ce qui rend
                        // « 100 » et « 7,5 ». Les arrondir ici affichait « x8 » pour un malus
                        // réel de 7,5 — le bouton mentait d'un demi-point.
                        _ghostCache.EffectiveYieldMultiplier,
                        _ghostCache.OverdriveDurationSeconds,
                        _ghostCache.EffectiveTraceMultiplier),
                    interactable: true,
                    // Avancement de la charge SUIVANTE, pas 1 : c'est la couleur qui dit
                    // « armé », la barre reste libre de montrer ce qui se recharge derrière.
                    // Elle atteint 1 d'elle-même quand toutes les charges sont pleines.
                    fillAmount: _ghostCache.NormalizedCharge,
                    fillColor: _view.ReadyColor);

                return;
            }

            float normalized = _ghostCache.NormalizedCharge;

            _view.ApplyState(
                _loc.GetText("UI_GHOSTCACHE_CHARGING", Mathf.RoundToInt(normalized * 100f)),
                interactable: false,
                fillAmount: normalized,
                fillColor: _view.ChargingColor);
        }

        private void HandleClicked()
        {
            // Le retour est ignoré à dessein : le bouton est déjà grisé hors état prêt, et
            // l'abonnement à IsOverdriveActive se charge d'annoncer un déclenchement réussi.
            _ghostCache.TryTriggerOverdrive();
        }

        public void Dispose()
        {
            _view.OnOverdriveClicked -= HandleClicked;
            _disposables.Dispose();
        }
    }
}
