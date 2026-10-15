using Core.Models.Economy;
using Core.Models.Simulation;
using Core.Services.Economy;
using Core.Services.Security;
using R3;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Simulation
{
    /// <summary>
    /// Journal de fin de run : une ligne CSV par run terminée, dans
    /// <c>journal-runs.csv</c>, à côté de la sauvegarde.
    ///
    /// <b>Pourquoi.</b> Session « chiffres » (2026-10-15) : le GD fixe des cibles de campagne
    /// (10 à 12 h, 80 à 100 runs, durée des runs en cloche, premier point à la run 2) et un
    /// harnais les mesurera avec un bot. Le journal sert à CALER ce bot sur un humain : Bertrand
    /// joue quelques runs, et on compare ses lignes à celles du bot. Sans lui, la simulation
    /// mesurerait le bot, pas un joueur.
    ///
    /// <b>Outil de développement</b> : enregistré seulement en éditeur et en build de
    /// développement (<c>Debug.isDebugBuild</c>). Rien de ce qu'il écrit n'est affiché au joueur,
    /// d'où des en-têtes de colonnes en clair et non des clés de localisation.
    ///
    /// <b>Limite connue.</b> Les compteurs d'Overclocks et de Proxies, et les pics de TFlops,
    /// partent de zéro au lancement du jeu : une run commencée avant une fermeture n'en compte
    /// que la seconde moitié. Pour le calibrage, jouer chaque run d'une traite.
    /// </summary>
    public sealed class RunJournal : IStartable, IDisposable
    {
        private const string FileName = "journal-runs.csv";

        private const string Header =
            "horodatage;run;issue;duree_s;jauge_fin;datas_run;cycles_gagnes;cycles_avant_bonus;" +
            "cycles_cumules;points_depenses;tflops_nettes_max;tflops_brutes_max;overclocks;" +
            "proxies;data_wiper;victoire";

        private readonly GameSessionManager _session;
        private readonly UpgradeManager _upgrades;
        private readonly ThreatManager _threat;
        private readonly PrestigeManager _prestige;
        private readonly UserCurrencies _currencies;
        private readonly VictorySystem _victory;

        /// <summary>Réutilisé d'une ligne à l'autre : une run se termine toutes les cinq minutes.</summary>
        private readonly StringBuilder _line = new StringBuilder(256);

        private DisposableBag _disposables;
        private string _path;
        private int _runCount;

        // Ce que le bilan de run ne porte pas, compté ici pendant la run.
        private double _peakNetTFlops;
        private double _peakGrossTFlops;
        private int _overclocks;
        private int _proxies;

        public RunJournal(
            GameSessionManager session,
            UpgradeManager upgrades,
            ThreatManager threat,
            PrestigeManager prestige,
            UserCurrencies currencies,
            VictorySystem victory)
        {
            _session = session;
            _upgrades = upgrades;
            _threat = threat;
            _prestige = prestige;
            _currencies = currencies;
            _victory = victory;
        }

        public void Start()
        {
            _path = Path.Combine(Application.persistentDataPath, FileName);
            _runCount = CountLoggedRuns(_path);

            // Pics et non valeurs finales : au moment où le bilan est émis, la run est déjà
            // effacée et les TFlops sont retombées à zéro. Ces deux flux ne bougent qu'aux achats.
            _upgrades.NetTFlops
                .Subscribe(this, (value, self) => { if (value > self._peakNetTFlops) self._peakNetTFlops = value; })
                .AddTo(ref _disposables);

            _upgrades.GrossTFlops
                .Subscribe(this, (value, self) => { if (value > self._peakGrossTFlops) self._peakGrossTFlops = value; })
                .AddTo(ref _disposables);

            _threat.OnOverclockPenalty
                .Subscribe(this, (_, self) => self._overclocks++)
                .AddTo(ref _disposables);

            _upgrades.OnProxyWipe
                .Subscribe(this, (_, self) => self._proxies++)
                .AddTo(ref _disposables);

            _session.OnSessionEnded
                .Subscribe(this, (summary, self) => self.Append(summary))
                .AddTo(ref _disposables);

            // Une run neuve commence quand la partie se réarme : les compteurs repartent de zéro.
            // Skip(1) : la valeur courante n'est pas un redémarrage.
            _session.IsGameActive
                .Skip(1)
                .Where(isActive => isActive)
                .Subscribe(this, (_, self) => self.ResetRunCounters())
                .AddTo(ref _disposables);
        }

        private void ResetRunCounters()
        {
            _peakNetTFlops = _upgrades.NetTFlops.CurrentValue;
            _peakGrossTFlops = _upgrades.GrossTFlops.CurrentValue;
            _overclocks = 0;
            _proxies = 0;
        }

        private void Append(RunSummary summary)
        {
            _runCount++;

            CultureInfo inv = CultureInfo.InvariantCulture;
            _line.Clear();
            _line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv)).Append(';')
                 .Append(_runCount.ToString(inv)).Append(';')
                 .Append(summary.Reason == RunEndReason.CleanExit ? "sortie" : "saisie").Append(';')
                 .Append(summary.ElapsedSeconds.ToString("0.0", inv)).Append(';')
                 .Append(summary.ThreatAtEnd.ToString("0.000", inv)).Append(';')
                 .Append(summary.DataGenerated.ToString("R", inv)).Append(';')
                 .Append(summary.CpuCyclesEarned.ToString("R", inv)).Append(';')
                 .Append(summary.CpuCyclesBeforeBonus.ToString("R", inv)).Append(';')
                 .Append(_currencies.CpuCyclesAwarded.CurrentValue.ToString("R", inv)).Append(';')
                 .Append(_prestige.TotalPointsSpent.CurrentValue.ToString(inv)).Append(';')
                 .Append(_peakNetTFlops.ToString("R", inv)).Append(';')
                 .Append(_peakGrossTFlops.ToString("R", inv)).Append(';')
                 .Append(_overclocks.ToString(inv)).Append(';')
                 .Append(_proxies.ToString(inv)).Append(';')
                 .Append(summary.EmergencyUses.ToString(inv)).Append(';')
                 .Append(_victory.HasWon.CurrentValue ? "oui" : "non")
                 .Append(Environment.NewLine);

            try
            {
                if (!File.Exists(_path))
                {
                    File.WriteAllText(_path, Header + Environment.NewLine);
                }

                File.AppendAllText(_path, _line.ToString());
                Debug.Log($"[JOURNAL] Run {_runCount} consignée dans {_path}");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Un journal de développement ne doit jamais faire tomber une fin de run.
                Debug.LogWarning($"[JOURNAL] Écriture impossible ({_path}) : {ex.Message}");
            }
        }

        /// <summary>
        /// Les runs déjà consignées, pour continuer la numérotation d'une session à l'autre :
        /// une ligne par run, moins l'en-tête. Supprimer le fichier pour repartir de la run 1.
        /// </summary>
        private static int CountLoggedRuns(string path)
        {
            try
            {
                if (!File.Exists(path)) return 0;

                int lines = 0;
                foreach (string line in File.ReadLines(path))
                {
                    if (line.Length > 0) lines++;
                }

                return Math.Max(0, lines - 1);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[JOURNAL] Lecture impossible ({path}) : {ex.Message}");
                return 0;
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
