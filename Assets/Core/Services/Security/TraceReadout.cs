using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Platform;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Security
{
    /// <summary>
    /// Le capteur imparfait posé sur la jauge de l'A.M.I. <b>Il ne ment jamais, il retarde.</b>
    ///
    /// <b>Refonte « Time Attack » (lot 3, 2026-10-14).</b> La jauge est devenue un compte à rebours
    /// déterministe, et un compte à rebours entièrement observable ne surprend personne. Le
    /// brouillard reste donc, mais il ne vit plus que dans l'AFFICHAGE : aucune règle de jeu ne le
    /// lit, le <see cref="ThreatManager"/> reste la seule vérité.
    ///
    /// Deux lectures, et deux seulement :
    /// <list type="bullet">
    /// <item><b>L'aiguille</b> — le dernier relevé. Elle ne bouge qu'à chaque relevé, et c'est elle
    /// que lisent les paliers d'extraction, le gain annoncé par le bouton de sortie et les alertes
    /// sonores : le joueur ne touche jamais moins que ce qu'on lui annonce.</item>
    /// <item><b>La borne pessimiste</b> — où la jauge peut être, au pire :
    /// <c>relevé + V_AMI × âge × pessimisme + sauts connus</c>. Elle avance à chaque frame et ne
    /// passe jamais sous la vérité.</item>
    /// </list>
    ///
    /// <b>Un relevé tous les N UT remplis, pas toutes les N secondes.</b> L'ancien intervalle en
    /// secondes ne tenait plus face à une vitesse qui varie d'un ordre de grandeur : au départ la
    /// jauge restait figée une minute entière (session du 2026-10-14), en fin de partie la run
    /// durait moins qu'un intervalle. En UT, le relevé suit le remplissage de lui-même. Le capteur
    /// — la puissance de calcul nette — fait passer l'intervalle de 800 à 200 UT et ramène le
    /// pessimisme de ×3 vers ×1.
    /// </summary>
    public sealed class TraceReadout : ITickable, IDisposable
    {
        private readonly ThreatManager _threat;
        private readonly BalancingConfigSO _balancing;
        private readonly ITimeSource _time;
        private readonly UpgradeManager _upgrades;

        /// <summary>Vérité à la frame précédente, en UT : sert à mesurer ce qui s'est rempli.</summary>
        private float _previousTrace;

        /// <summary>Valeur du dernier relevé, en UT.</summary>
        private float _lastKnownTrace;

        /// <summary>UT remplies depuis le dernier relevé : c'est ce qui déclenche le suivant.</summary>
        private float _filledSinceSample;

        /// <summary>Secondes écoulées depuis le dernier relevé.</summary>
        private float _sampleAge;

        /// <summary>
        /// UT ajoutées d'un coup depuis le dernier relevé, au-delà de ce que la vitesse explique :
        /// les pénalités d'Overclock. Le joueur les connaît (la console les annonce) ; la borne les
        /// compte, sans quoi trois appuis rapides la feraient passer sous la vérité.
        /// </summary>
        private float _knownJumps;

        private readonly ReactiveProperty<float> _lastKnownFraction = new(0f);
        private readonly ReactiveProperty<float> _estimatedMaxFraction = new(0f);
        private readonly ReactiveProperty<int> _sampleAgeSeconds = new(0);

        /// <summary>
        /// Dernière valeur RELEVÉE de la jauge, en fraction : l'aiguille. Paliers, gain du bouton
        /// de sortie et alertes sonores se lisent ici, jamais sur la vérité.
        /// </summary>
        public ReadOnlyReactiveProperty<float> LastKnownFraction => _lastKnownFraction;

        /// <summary>
        /// Borne pessimiste : où la jauge peut se trouver au pire. Jamais sous la vérité — c'est
        /// la seule garantie que le joueur possède.
        /// </summary>
        public ReadOnlyReactiveProperty<float> EstimatedMaxFraction => _estimatedMaxFraction;

        /// <summary>
        /// Âge du relevé en secondes ENTIÈRES. N'émet qu'une fois par seconde : c'est ce qui permet
        /// au libellé « relevé il y a N s » de vieillir sans recomposer une chaîne à chaque frame.
        /// </summary>
        public ReadOnlyReactiveProperty<int> SampleAgeSeconds => _sampleAgeSeconds;

        /// <summary>Âge du dernier relevé, en secondes.</summary>
        public float SecondsSinceSample => _sampleAge;

        /// <summary>Valeur du dernier relevé, en UT. Sauvegardée, avec l'âge et les sauts connus.</summary>
        public float LastKnownTrace => _lastKnownTrace;

        /// <summary>Sauts connus depuis le dernier relevé, en UT. Sauvegardés.</summary>
        public float KnownJumps => _knownJumps;

        public TraceReadout(ThreatManager threat,
                            BalancingConfigSO balancing,
                            ITimeSource time,
                            UpgradeManager upgrades)
        {
            _threat = threat;
            _balancing = balancing;
            _time = time;
            _upgrades = upgrades;
        }

        public void Tick()
        {
            // Pause, menu, écran de fin figé : le brouillard s'arrête avec la simulation.
            float deltaTime = _time.DeltaTime;
            if (deltaTime <= 0f) return;

            float truth = _threat.CurrentTrace.CurrentValue;

            // La jauge ne redescend que sur un Wipe (Proxy, Data Wiper) ou un wipe de fin de run.
            // Dans tous ces cas le joueur voit l'événement : le relevé se recale sur-le-champ, et
            // la borne repart aussitôt de là — elle ne disparaît plus jusqu'au relevé suivant.
            if (truth < _previousTrace)
            {
                Resync(truth);
                return;
            }

            // Jauge vide et rien encore relevé : la traque n'est pas armée. Le brouillard ne dérive
            // pas, sinon la borne monterait sur une jauge qui ne bouge pas.
            if (truth <= 0f && _lastKnownTrace <= 0f)
            {
                return;
            }

            float delta = truth - _previousTrace;
            _previousTrace = truth;

            float speed = _threat.CurrentSpeed.CurrentValue;

            float expected = speed * deltaTime;
            if (delta > expected) _knownJumps += delta - expected;

            _filledSinceSample += delta;
            _sampleAge += deltaTime;

            // Un seul calcul du capteur par frame, réutilisé par l'intervalle et le pessimisme.
            float precision = ResolvePrecisionDivisor();

            if (_filledSinceSample >= ResolveIntervalUT(precision))
            {
                Sample(truth);
            }

            RefreshBand(speed, precision);
            RefreshAgeSeconds();
        }

        /// <summary>
        /// Recale le relevé sur la vérité, sans délai : jauge qui redescend, nouvelle run.
        /// </summary>
        public void Resync(float trueTrace)
        {
            _previousTrace = trueTrace;
            _lastKnownTrace = trueTrace;
            _filledSinceSample = 0f;
            _sampleAge = 0f;
            _knownJumps = 0f;

            float fraction = ToFraction(trueTrace);
            _sampleAgeSeconds.Value = 0;
            _lastKnownFraction.Value = fraction;
            _estimatedMaxFraction.Value = fraction;
        }

        /// <summary>
        /// Restaure le relevé TEL QU'IL ÉTAIT à la fermeture du jeu, et non sur la vérité.
        ///
        /// Recaler sur la vérité au chargement — l'ancien comportement — révélait la position
        /// exacte de la jauge : quitter puis relancer suffisait à lever le brouillard (session du
        /// 2026-10-14). À appeler après la restauration de la jauge, dont il lit la valeur.
        /// </summary>
        public void Restore(float lastKnownTrace, float sampleAge, float knownJumps)
        {
            float truth = _threat.CurrentTrace.CurrentValue;

            // Un relevé au-dessus de la vérité est impossible — la jauge ne redescend que par un
            // événement qui recale le relevé. Fichier abîmé ou édité : on recale.
            if (lastKnownTrace < 0f || lastKnownTrace > truth || sampleAge < 0f || knownJumps < 0f)
            {
                Resync(truth);
                return;
            }

            _previousTrace = truth;
            _lastKnownTrace = lastKnownTrace;
            _filledSinceSample = truth - lastKnownTrace;
            _sampleAge = sampleAge;
            _knownJumps = knownJumps;

            _lastKnownFraction.Value = ToFraction(lastKnownTrace);
            RefreshBand(_threat.CurrentSpeed.CurrentValue, ResolvePrecisionDivisor());
            RefreshAgeSeconds();
        }

        private void Sample(float truth)
        {
            // L'âge AVANT la valeur : les abonnés de l'aiguille relisent l'âge pour composer leur
            // libellé, et ils doivent lire celui du relevé qui vient d'être pris.
            _lastKnownTrace = truth;
            _filledSinceSample = 0f;
            _sampleAge = 0f;
            _knownJumps = 0f;
            _sampleAgeSeconds.Value = 0;
            _lastKnownFraction.Value = ToFraction(truth);
        }

        /// <summary>
        /// <c>borne = relevé + V_AMI × âge × pessimisme + sauts connus</c>.
        ///
        /// Jamais sous la vérité : la vérité vaut relevé + Σ(vitesse × dt) + sauts, la vitesse ne
        /// baisse jamais pendant une run, et le pessimisme ne descend pas sous 1. Le capteur le
        /// RAMÈNE vers 1 sans jamais passer dessous — l'interpolation part de 1 et va vers le
        /// réglage —, ce qui garde la borne honnête quelle que soit la puissance installée.
        /// </summary>
        private void RefreshBand(float speed, float precision)
        {
            float pessimism = Mathf.Lerp(1f, _balancing.TraceReadoutPessimism, 1f / precision);
            float projected = _lastKnownTrace + speed * _sampleAge * pessimism + _knownJumps;

            _estimatedMaxFraction.Value = ToFraction(projected);
        }

        private void RefreshAgeSeconds()
        {
            int seconds = (int)_sampleAge;
            if (seconds != _sampleAgeSeconds.CurrentValue) _sampleAgeSeconds.Value = seconds;
        }

        /// <summary>
        /// UT à remplir entre deux relevés : de 800 au départ à 200 avec un parc conséquent. Le
        /// plancher reste le plancher — sous lui, l'aiguille serait continue pour l'œil et le
        /// brouillard n'existerait plus.
        /// </summary>
        private float ResolveIntervalUT(float precisionDivisor)
        {
            return Mathf.Max(_balancing.TraceReadoutMinIntervalUT,
                             _balancing.TraceReadoutMaxIntervalUT / precisionDivisor);
        }

        /// <summary>
        /// Ce que la capacité de calcul NETTE achète en visibilité :
        /// <c>1 + puissance × log10(1 + TFlops / échelle)</c>. Vaut 1 à zéro TFlop — aucune aide,
        /// le brouillard nominal — et ne descend jamais dessous.
        ///
        /// Les TFlops NETTES, à dessein : le sacrifice du Data Wiper aveugle aussi le capteur, et
        /// la méta-progression (P_DATAM, Noyau) aide à voir clair.
        /// </summary>
        private float ResolvePrecisionDivisor()
        {
            float tflops = Mathf.Max(0f, (float)_upgrades.NetTFlops.CurrentValue);
            float reference = Mathf.Max(1f, _balancing.TraceReadoutSensorReferenceTFlops);

            return 1f + _balancing.TraceReadoutSensorPower * Mathf.Log10(1f + tflops / reference);
        }

        private float ToFraction(float trace)
        {
            return Mathf.Clamp01(trace / _threat.TraceCap);
        }

        public void Dispose()
        {
            _lastKnownFraction.Dispose();
            _estimatedMaxFraction.Dispose();
            _sampleAgeSeconds.Dispose();
        }
    }
}
