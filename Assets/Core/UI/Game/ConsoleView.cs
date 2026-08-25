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

        public void AppendLog(string message, ConsoleLogType type)
        {
            // On récupère une ligne propre depuis le pool (zéro instanciation)
            TextMeshProUGUI logItem = _pool.GetOrCreate();

            // Formatage du texte selon le type de log (Terminal Juice)
            logItem.text = FormatMessage(message, type);

            // Optionnel : Forcer le layout à se replacer en bas de la liste si besoin
            logItem.transform.SetAsLastSibling();
        }

        private string FormatMessage(string message, ConsoleLogType type)
        {
            return type switch
            {
                ConsoleLogType.Success => $"<color=#00FF00>[OK]</color> {message}",
                ConsoleLogType.Narrative => $"<color=#FF00FF>[SYSTEM_WARN]</color> {message}",
                _ => $"<color=#CCCCCC>[INFO]</color> {message}"
            };
        }
    }
}