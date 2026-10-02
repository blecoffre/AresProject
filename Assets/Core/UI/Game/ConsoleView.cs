using Core.Models.Console;
using Core.Services.Console;
using TMPro;
using UnityEngine;

namespace Core.UI.Game
{
    public class ConsoleView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _logPrefab;
        [SerializeField] private Transform _logContainer;

        [Header("Settings")]
        [SerializeField] private int _maxPoolSize = 30;

        private LogObjectPool _pool;

        private void Awake()
        {
            // Initialisation du pool textuel au démarrage de la vue
            _pool = new LogObjectPool(_logPrefab, _logContainer, _maxPoolSize);
        }

        private string _successPrefix = string.Empty;
        private string _narrativePrefix = string.Empty;
        private string _standardPrefix = string.Empty;

        public void AppendLog(string message, ConsoleLogType type)
        {
            // On récupère une ligne propre depuis le pool (zéro instanciation)
            TextMeshProUGUI logItem = _pool.GetOrCreate();

            // Formatage du texte selon le type de log (Terminal Juice)
            logItem.text = FormatMessage(message, type);

            // Optionnel : Forcer le layout à se replacer en bas de la liste si besoin
            logItem.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Les étiquettes de début de ligne, déjà colorées et localisées par le presenter. Les
        /// lignes déjà affichées gardent les leurs : c'est un historique.
        /// </summary>
        public void SetTagPrefixes(string success, string narrative, string standard)
        {
            _successPrefix = success;
            _narrativePrefix = narrative;
            _standardPrefix = standard;
        }

        private string FormatMessage(string message, ConsoleLogType type)
        {
            return type switch
            {
                ConsoleLogType.Success => _successPrefix + message,
                ConsoleLogType.Narrative => _narrativePrefix + message,
                _ => _standardPrefix + message
            };
        }
    }
}