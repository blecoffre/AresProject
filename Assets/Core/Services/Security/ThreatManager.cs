using Core.Models.Economy;
using Core.Services.Economy;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Security
{
    /// <summary>
    /// La jauge de l'A.M.I. — le compte à rebours de la run, et la SEULE vérité sur la Trace.
    ///
    /// <b>Refonte « Time Attack » du 2026-10-13.</b> La Trace n'est plus générée par les actions
    /// du joueur : Scripts et Hardware ne laissent plus rien, les Proxies ne dissipent plus un
    /// débit. Il reste un compte à rebours inéluctable, indexé sur la puissance installée :
    ///
    /// <c>V_AMI = Base × (1 + Facteur × TFlopsBruts) ^ Exposant ÷ (1 + Blindage)</c>
    ///
    /// Le plafond est FIXE (10 000 UT) et le plancher vaut zéro. Le franchir déclenche la Saisie
    /// Fédérale.
    ///
    /// <b>Seul ce service modifie la jauge.</b> Les trois actions qui la bousculent passent par
    /// ses méthodes : la pénalité d'Overclock, le Wipe d'un Proxy acheté, le Data Wiper (−2 500 UT). Le
    /// temps, lui, est poussé par le SimulationTicker — l'inverse créerait une dépendance
    /// circulaire avec le GameSessionManager, qui seul sait si une run est en cours.
    ///
    /// <b>Pourquoi les TFlops BRUTES.</b> La vitesse lit la seule somme du parc Hardware, sans
    /// P_DATAM, sans Noyau, sans le sacrifice du Data Wiper : le prestige accélère les revenus
    /// sans accélérer la mort, et sacrifier des TFlops ne ralentit jamais la menace — sans quoi
    /// le Data Wiper deviendrait un soin gratuit.
    /// </summary>
    public sealed class ThreatManager : IStartable, IDisposable
    {
        private readonly BalancingConfigSO _balancing;
        private readonly UpgradeManager _upgrades;
        private readonly PrestigeManager _prestige;

        private readonly ReactiveProperty<float> _currentTrace = new(0f);
        private readonly ReactiveProperty<float> _normalizedThreat = new(0f);
        private readonly ReactiveProperty<float> _speed = new(0f);
        private readonly Subject<Unit> _onCriticalLockdown = new();
        private readonly Subject<float> _onOverclockPenalty = new();

        private DisposableBag _disposables;

        /// <summary>Trace accumulée, en Unités de Trace (UT).</summary>
        public ReadOnlyReactiveProperty<float> CurrentTrace => _currentTrace;

        /// <summary>La jauge de 0 (Safe) à 1 (Saisie). Dérivée de la Trace et du plafond.</summary>
        public ReadOnlyReactiveProperty<float> NormalizedThreat => _normalizedThreat;

        /// <summary>
        /// Vitesse de l'A.M.I., en UT par seconde, Blindage compris. Mise en cache et recalculée
        /// seulement quand une de ses entrées bouge : TFlops bruts, Blindage, ou réglage retouché
        /// dans l'inspecteur.
        /// </summary>
        public ReadOnlyReactiveProperty<float> CurrentSpeed => _speed;

        /// <summary>Plafond de la jauge, en UT. Fixe en cours de partie, lu à l'usage.</summary>
        public float TraceCap => Mathf.Max(1f, _balancing.AmiTraceCap);

        /// <summary>Émis quand la jauge atteint son plafond.</summary>
        public Observable<Unit> OnCriticalLockdown => _onCriticalLockdown;

        /// <summary>
        /// Émis à chaque pénalité d'Overclock infligée, avec les UT ajoutées. Lu par le journal de
        /// fin de run (session « chiffres », 2026-10-15), qui compte les appuis d'un joueur réel
        /// pour caler le bot du harnais.
        /// </summary>
        public Observable<float> OnOverclockPenalty => _onOverclockPenalty;

        public ThreatManager(BalancingConfigSO balancing, UpgradeManager upgrades, PrestigeManager prestige)
        {
            _balancing = balancing;
            _upgrades = upgrades;
            _prestige = prestige;
        }

        public void Start()
        {
            // CombineLatest et non un calcul par frame : la vitesse ne bouge qu'à l'achat d'un
            // Hardware ou d'un nœud de Blindage. Le Pow n'est payé qu'à ces moments-là.
            Observable.CombineLatest(
                    _upgrades.GrossTFlops,
                    _prestige.TotalHardenBonus,
                    (gross, harden) => ComputeSpeed(gross, harden))
                .Subscribe(speed => _speed.Value = speed)
                .AddTo(ref _disposables);

            // Seule entrée qui n'est pas réactive d'elle-même : l'asset d'équilibrage. Sans ce
            // signal, régler la vitesse en Play Mode n'aurait d'effet qu'au prochain achat.
            _balancing.Edited
                .Subscribe(_ => RefreshSpeed())
                .AddTo(ref _disposables);

            _upgrades.OnProxyWipe
                .Subscribe(ApplyProxyWipe)
                .AddTo(ref _disposables);
        }

        /// <summary>
        /// Fait avancer le compte à rebours de <paramref name="deltaTime"/> secondes. Appelé à
        /// chaque frame par le SimulationTicker, et seulement quand la traque est armée.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            AddTrace(_speed.CurrentValue * deltaTime);
        }

        /// <summary>
        /// Inflige la pénalité d'un Overclock : <c>V_AMI × secondes de pénalité</c>, en UT.
        ///
        /// Indexée sur la vitesse et non fixe : l'appui coûte toujours les MÊMES secondes de
        /// survie, de la première à la dernière minute. Le Blindage la réduit en UT, jamais en
        /// secondes, puisqu'il ralentit la vitesse elle-même. Peut déclencher la saisie.
        /// Retourne les UT ajoutées.
        /// </summary>
        public float ApplyOverclockPenalty()
        {
            float penalty = _speed.CurrentValue * Mathf.Max(0f, _balancing.OverclockPenaltySeconds);
            AddTrace(penalty);
            _onOverclockPenalty.OnNext(penalty);
            return penalty;
        }

        /// <summary>
        /// Retire des UT de la jauge, sans descendre sous zéro. L'excédent est PERDU, pas mis en
        /// réserve : c'est ce qui oblige à acheter un Proxy au bon moment plutôt qu'à vide.
        /// Retourne les UT réellement retirées.
        /// </summary>
        public float Wipe(float amount)
        {
            if (amount <= 0f) return 0f;

            float before = _currentTrace.CurrentValue;
            float after = Mathf.Max(0f, before - amount);
            if (after == before) return 0f;

            _currentTrace.Value = after;
            RefreshNormalized();
            return before - after;
        }

        /// <summary>Vide la jauge. Réservé au wipe de fin de run.</summary>
        public void ResetTrace()
        {
            _currentTrace.Value = 0f;
            RefreshNormalized();
        }

        /// <summary>
        /// Repositionne la jauge sans déclencher de saisie : réservé au chargement d'une
        /// sauvegarde. Le déclenchement appartient au jeu en cours, pas à la restauration, où
        /// l'UI n'est pas encore abonnée. Le GameStateGateway plafonne déjà la valeur sous le
        /// maximum.
        /// </summary>
        public void RestoreTrace(float trace)
        {
            _currentTrace.Value = Mathf.Clamp(trace, 0f, TraceCap);
            RefreshNormalized();
        }

        /// <summary>
        /// Le Wipe d'un Proxy acheté. L'UpgradeManager le chiffre en SECONDES DE SURVIE — base du
        /// Proxy, paliers de niveau et quantité achetée compris — et c'est ici qu'elles
        /// deviennent des UT, à la vitesse courante et avec la branche Interception :
        ///
        /// <c>UT = secondes × V_AMI × (1 + Interception)</c>
        ///
        /// En secondes et non en UT fixes : un même Proxy offre le même répit à toute vitesse de
        /// l'A.M.I., au lieu de devenir dérisoire en fin de partie ou de vider vingt jauges au
        /// début.
        /// </summary>
        private void ApplyProxyWipe(float survivalSeconds)
        {
            float interception = 1f + Mathf.Max(0f, _prestige.TotalInterceptBonus.CurrentValue);
            Wipe(survivalSeconds * _speed.CurrentValue * interception);
        }

        private void AddTrace(float amount)
        {
            if (amount <= 0f) return;

            // Déjà saisi : la run est en cours de résolution, on n'y ajoute rien.
            float cap = TraceCap;
            if (_currentTrace.CurrentValue >= cap) return;

            _currentTrace.Value = Mathf.Min(cap, _currentTrace.CurrentValue + amount);
            RefreshNormalized();

            if (_currentTrace.CurrentValue >= cap)
            {
                Debug.LogWarning("[THREAT] SAISIE FÉDÉRALE. Arrêt des scripts.");
                _onCriticalLockdown.OnNext(Unit.Default);
            }
        }

        private void RefreshSpeed()
        {
            _speed.Value = ComputeSpeed(
                _upgrades.GrossTFlops.CurrentValue,
                _prestige.TotalHardenBonus.CurrentValue);
        }

        /// <summary>
        /// <c>V_AMI = Base × (1 + Facteur × TFlopsBruts) ^ Exposant ÷ (1 + Blindage)</c>.
        /// Calcul en double : les TFlops bruts dépassent 1e11 en fin de partie.
        /// </summary>
        private float ComputeSpeed(double grossTFlops, float hardenBonus)
        {
            double scale = Math.Pow(
                1d + _balancing.AmiTFlopsFactor * Math.Max(0d, grossTFlops),
                _balancing.AmiSpeedExponent);

            return (float)(_balancing.AmiBaseSpeed * scale / (1d + Math.Max(0f, hardenBonus)));
        }

        private void RefreshNormalized()
        {
            _normalizedThreat.Value = Mathf.Clamp01(_currentTrace.CurrentValue / TraceCap);
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _onCriticalLockdown.Dispose();
            _onOverclockPenalty.Dispose();
            _normalizedThreat.Dispose();
            _currentTrace.Dispose();
            _speed.Dispose();
        }
    }
}
