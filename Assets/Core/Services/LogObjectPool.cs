using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Core.Services.Console
{
    public class LogObjectPool
    {
        private readonly TextMeshProUGUI _prefab;
        private readonly Transform _container;
        private readonly Queue<TextMeshProUGUI> _pool;
        private readonly List<TextMeshProUGUI> _activeItems;
        private readonly int _maxPoolSize;

        public LogObjectPool(TextMeshProUGUI prefab, Transform container, int poolSize = 30)
        {
            _prefab = prefab;
            _container = container;
            _maxPoolSize = poolSize;
            _pool = new Queue<TextMeshProUGUI>(poolSize);
            _activeItems = new List<TextMeshProUGUI>(poolSize);

            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < _maxPoolSize; i++)
            {
                TextMeshProUGUI item = Object.Instantiate(_prefab, _container);
                item.gameObject.SetActive(false);
                _pool.Enqueue(item);
            }
        }

        /// <summary>
        /// Récupère un élément du pool, ou recycle le plus ancien si le pool est plein (Effet Tapis Roulant).
        /// Zéro instanciation à chaud.
        /// </summary>
        public TextMeshProUGUI GetOrCreate()
        {
            TextMeshProUGUI item;

            if (_pool.Count > 0)
            {
                item = _pool.Dequeue();
            }
            else
            {
                // Si tous les éléments sont actifs, on prend le plus ancien (le premier de la liste active)
                item = _activeItems[0];
                _activeItems.RemoveAt(0);
                item.transform.SetAsLastSibling(); // Remet en bas de la liste visuelle
            }

            item.gameObject.SetActive(true);
            _activeItems.Add(item);
            return item;
        }

        public void ReturnToPool(TextMeshProUGUI item)
        {
            item.gameObject.SetActive(false);
            if (_activeItems.Contains(item))
            {
                _activeItems.Remove(item);
            }
            _pool.Enqueue(item);
        }
    }
}