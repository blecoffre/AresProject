using R3;
using System;

namespace Core.Models.Economy
{
    /// <summary>
    /// Les monnaies que le joueur accumule et dépense.
    ///
    /// Les TFlops n'en font PAS partie, volontairement : ce ne sont pas un stock mais une
    /// capacité dérivée du parc Hardware possédé, recalculée par l'UpgradeManager et exposée
    /// par sa propriété NetTFlops. Les garder ici sous forme de Currency alimentée par Add()
    /// laissait croire à une ressource qu'on accumule et qu'on dépense, ce qu'elles ne sont pas.
    /// </summary>
    public class UserCurrencies : IDisposable
    {
        public Currency Money { get; }
        public Currency CpuCycles { get; }     // La monnaie de Prestige

        /// <summary>Argent généré depuis le début de la partie. Statistique, sans effet de jeu.</summary>
        private readonly ReactiveProperty<double> _totalMoneyGenerated;
        public ReadOnlyReactiveProperty<double> TotalMoneyGenerated => _totalMoneyGenerated;

        /// <summary>
        /// Argent généré depuis le début de la RUN en cours. C'est lui, et lui seul, qui détermine
        /// le gain de CPU Cycles en fin de run.
        ///
        /// Le distinguer du cumul à vie n'est pas un détail : sur le compteur à vie, chaque run
        /// rapportait mécaniquement au moins autant que la précédente, sans rien faire. La boucle
        /// de méta-progression n'avait plus aucune tension, et flirter avec 95 % de Trace ne
        /// rapportait pas plus que mourir tôt.
        /// </summary>
        private readonly ReactiveProperty<double> _runMoneyGenerated;
        public ReadOnlyReactiveProperty<double> RunMoneyGenerated => _runMoneyGenerated;

        private readonly ReactiveProperty<double> _totalCpuCyclesGenerated;
        public ReadOnlyReactiveProperty<double> TotalCpuCyclesGenerated => _totalCpuCyclesGenerated;

        private readonly ReactiveProperty<int> _totalNumberOfDetections;
        public ReadOnlyReactiveProperty<int> TotalNumberOfDetections => _totalNumberOfDetections;

        /// <summary>
        /// Datas EXFILTRÉES depuis le début de la campagne. Ne retombe jamais à zéro entre deux
        /// runs, et c'est tout l'intérêt : une run qui n'atteint pas le palier de points suivant
        /// n'est plus perdue, ce qu'elle a rapporté continue de compter.
        ///
        /// <b>Une saisie n'alimente pas ce compteur.</b> C'est là que vit la punition depuis que
        /// les points ne se calculent plus sur la seule run. Sans cette règle, se faire prendre
        /// serait gratuit ; et si les points tombaient PENDANT la run plutôt qu'à l'exfiltration,
        /// il deviendrait même rentable de se faire saisir exprès — on encaisserait les points
        /// sans jamais faire monter le seuil suivant.
        /// </summary>
        private readonly ReactiveProperty<double> _campaignDatasBanked;
        public ReadOnlyReactiveProperty<double> CampaignDatasBanked => _campaignDatasBanked;

        /// <summary>
        /// Points de prestige DÉJÀ accordés. Le gain d'une exfiltration est la différence entre
        /// ce à quoi le cumul donne droit et ce compteur : impossible de toucher deux fois le
        /// même palier, quel que soit l'ordre des événements.
        /// </summary>
        private readonly ReactiveProperty<double> _cpuCyclesAwarded;
        public ReadOnlyReactiveProperty<double> CpuCyclesAwarded => _cpuCyclesAwarded;

        private readonly BalancingConfigSO _balancing;

        public UserCurrencies(BalancingConfigSO balancing)
        {
            _balancing = balancing;

            Money = new Currency();
            CpuCycles = new Currency();

            // NOTE : La Trace a été supprimée d'ici, elle vit désormais dans le ThreatManager !

            _totalMoneyGenerated = new ReactiveProperty<double>(0d);
            _runMoneyGenerated = new ReactiveProperty<double>(0d);
            _totalCpuCyclesGenerated = new ReactiveProperty<double>(0d);
            _totalNumberOfDetections = new ReactiveProperty<int>(0);
            _campaignDatasBanked = new ReactiveProperty<double>(0d);
            _cpuCyclesAwarded = new ReactiveProperty<double>(0d);
        }

        public void AddMoney(double amount)
        {
            if (amount <= 0) return;

            Money.Add(amount);
            _totalMoneyGenerated.Value += amount;
            _runMoneyGenerated.Value += amount;
        }

        public void AddCpuCycles(double amount)
        {
            if (amount <= 0) return;

            CpuCycles.Add(amount);
            _totalCpuCyclesGenerated.Value += amount;
        }

        public void RecordDetection()
        {
            _totalNumberOfDetections.Value += 1;
        }

        /// <summary>
        /// Datas cumulées à exfiltrer sur la campagne pour détenir <paramref name="points"/>
        /// points de prestige.
        ///
        /// <b>Modèle des BOÎTES.</b> Chaque point est une boîte à remplir entièrement, et son prix
        /// vaut <c>PremierPalier × Croissance^(N−1)</c>. Le cumul demandé pour N points est donc la
        /// SOMME des N boîtes, pas le prix de la dernière — à 1 M et ×1,5 : première boîte 1 M,
        /// deuxième 1,5 M, et il faut donc <b>2,5 M</b> de cumul pour détenir deux points.
        ///
        /// ⚠️ Ce n'était PAS le modèle jusqu'au 2026-09-25. Le cumul était comparé au seul prix de
        /// la dernière boîte, si bien que 2,5 M rendaient trois points au lieu de deux : chaque
        /// point ne coûtait en réalité que l'ÉCART avec le précédent, soit 12 % du palier. Le
        /// malentendu était consigné à l'identique dans le code, le GDD et le harnais.
        ///
        /// Somme géométrique : <c>premier × (croissance^N − 1) / (croissance − 1)</c>.
        /// </summary>
        public double CumulativeForPoints(double points)
        {
            if (points <= 0d) return 0d;

            double first = _balancing.PrestigeFirstThresholdDatas;
            double growth = Math.Max(1.0001d, _balancing.PrestigeThresholdGrowth);

            return first * (Math.Pow(growth, points) - 1d) / (growth - 1d);
        }

        /// <summary>
        /// Points de prestige auxquels donne droit un cumul de Datas exfiltrées — l'inverse de
        /// <see cref="CumulativeForPoints"/>.
        ///
        /// Les boîtes déjà remplies restent acquises et le reliquat travaille pour la suivante :
        /// c'est le cumul qui est sauvegardé, donc une run qui n'achève pas sa boîte n'est jamais
        /// perdue, et le prix de la prochaine ne retombe pas entre deux runs.
        ///
        /// Remplace la conversion appliquée à l'argent de la SEULE run, qui rendait vingt-huit
        /// points dès la première partie et forçait à choisir entre raréfier les points et
        /// garder l'exfiltration atteignable — les deux étant pilotés par la même valeur.
        /// </summary>
        public double CalculatePointsFor(double bankedDatas)
        {
            double first = _balancing.PrestigeFirstThresholdDatas;
            if (bankedDatas < first) return 0d;

            double growth = Math.Max(1.0001d, _balancing.PrestigeThresholdGrowth);

            return Math.Floor(Math.Log(1d + bankedDatas * (growth - 1d) / first, growth));
        }

        /// <summary>
        /// Points que rapporterait une exfiltration immédiate, contribution de la run comprise.
        /// Ne crédite RIEN : l'affichage du bouton l'appelle en continu.
        /// </summary>
        public double PreviewPointsForContribution(double contribution)
        {
            double projected = CalculatePointsFor(_campaignDatasBanked.Value + Math.Max(0d, contribution));
            return Math.Max(0d, projected - _cpuCyclesAwarded.Value);
        }

        /// <summary>
        /// Verse la contribution d'une run réussie au cumul de campagne et crédite les points
        /// qu'elle débloque.
        ///
        /// Appelé à chaque fin de run : une sortie verse sa contribution bonus de palier compris,
        /// une saisie 30 % seulement (`SeizedContributionRatio`, depuis le 2026-10-02). Versement
        /// et points sont résolus au même instant, ce qui rend impossible d'encaisser un palier
        /// sans faire monter le seuil suivant.
        /// </summary>
        public double BankRunContribution(double contribution)
        {
            if (contribution > 0d) _campaignDatasBanked.Value += contribution;

            double earned = Math.Max(0d, CalculatePointsFor(_campaignDatasBanked.Value) - _cpuCyclesAwarded.Value);
            if (earned <= 0d) return 0d;

            _cpuCyclesAwarded.Value += earned;
            AddCpuCycles(earned);
            return earned;
        }

        /// <summary>Restaure le cumul de campagne depuis une sauvegarde.</summary>
        public void RestoreCampaignProgress(double banked, double awarded)
        {
            _campaignDatasBanked.Value = banked < 0d ? 0d : banked;
            _cpuCyclesAwarded.Value = awarded < 0d ? 0d : awarded;
        }

        /// <summary>
        /// Remet à zéro ce qui appartient à la run. Appelé par le wipe, APRÈS que le gain de
        /// prestige a été calculé — l'ordre compte, sinon la run ne rapporterait jamais rien.
        /// </summary>
        public void ResetRunCounters()
        {
            _runMoneyGenerated.Value = 0d;
        }

        public void LoadFromSave(
            double savedMoney,
            double savedCpuCycles,
            double totalMoney,
            double runMoney,
            double totalCpuCycles,
            int totalDetections)
        {
            Money.Reset(savedMoney);
            CpuCycles.Reset(savedCpuCycles);

            _totalMoneyGenerated.Value = totalMoney;
            _runMoneyGenerated.Value = runMoney;
            _totalCpuCyclesGenerated.Value = totalCpuCycles;
            _totalNumberOfDetections.Value = totalDetections;
        }

        public void Dispose()
        {
            Money.Dispose();
            CpuCycles.Dispose();

            _totalMoneyGenerated.Dispose();
            _runMoneyGenerated.Dispose();
            _totalCpuCyclesGenerated.Dispose();
            _totalNumberOfDetections.Dispose();
            _campaignDatasBanked.Dispose();
            _cpuCyclesAwarded.Dispose();
        }
    }
}
