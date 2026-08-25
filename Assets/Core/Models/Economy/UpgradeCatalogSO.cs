using Core.Models.Economy;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Economy.Data
{
    [CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "Core/Economy/Upgrade Catalog")]
    public class UpgradeCatalogSO : ScriptableObject
    {
        [Tooltip("Glisse ici tous tes UpgradeConfigSO")]
        [SerializeField] private List<UpgradeConfigSO> _upgrades = new List<UpgradeConfigSO>();

        // Dictionnaire mis en cache pour une recherche instantanée et Zéro-GC au runtime
        private Dictionary<string, UpgradeConfigSO> _upgradeDict;

        public IReadOnlyList<UpgradeConfigSO> GetAllUpgrades() => _upgrades;

        /// <summary>
        /// Récupère la configuration d'une amélioration via son ID (O(1)).
        /// </summary>
        public UpgradeConfigSO GetById(string id)
        {
            // Initialisation paresseuse (Lazy Initialization) du dictionnaire
            if (_upgradeDict == null)
            {
                InitializeDictionary();
            }

            if (_upgradeDict.TryGetValue(id, out var config))
            {
                return config;
            }

            Debug.LogError($"[CATALOG] Amélioration introuvable pour l'ID : {id}");
            return null;
        }

        private void InitializeDictionary()
        {
            _upgradeDict = new Dictionary<string, UpgradeConfigSO>(_upgrades.Count);
            foreach (var upgrade in _upgrades)
            {
                if (upgrade != null && !string.IsNullOrEmpty(upgrade.Id))
                {
                    if (!_upgradeDict.ContainsKey(upgrade.Id))
                    {
                        _upgradeDict.Add(upgrade.Id, upgrade);
                    }
                    else
                    {
                        Debug.LogError($"[CATALOG] Conflit d'ID détecté : {upgrade.Id} existe déjà !");
                    }
                }
            }
        }
    }
}