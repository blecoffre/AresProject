using TMPro;
using UnityEngine;

namespace Core.Services.Console
{
    /// <summary>
    /// Les lignes de la console : un anneau de N lignes instanciées une fois. La N+1-ième ligne
    /// recycle la plus ancienne (« effet tapis roulant »).
    ///
    /// Tampon circulaire depuis le 2026-10-14, en O(1). Il recyclait par une liste
    /// (<c>RemoveAt(0)</c>, linéaire) doublée d'une file, à l'époque où une ligne pouvait encore
    /// revenir au pool ; plus rien ne la rend depuis le retrait de <c>ReturnToPool</c>, si bien
    /// que les lignes s'utilisent toujours dans le même ordre — exactement un anneau.
    /// </summary>
    public class LogObjectPool
    {
        private readonly TextMeshProUGUI[] _items;

        /// <summary>La prochaine ligne à servir : neuve au premier tour, la plus ancienne ensuite.</summary>
        private int _next;

        public LogObjectPool(TextMeshProUGUI prefab, Transform container, int poolSize = 30)
        {
            int size = poolSize > 0 ? poolSize : 1;
            _items = new TextMeshProUGUI[size];

            for (int i = 0; i < size; i++)
            {
                TextMeshProUGUI item = Object.Instantiate(prefab, container);
                item.gameObject.SetActive(false);
                _items[i] = item;
            }
        }

        /// <summary>
        /// La ligne suivante. Zéro instanciation et zéro allocation à chaud.
        /// </summary>
        public TextMeshProUGUI GetOrCreate()
        {
            TextMeshProUGUI item = _items[_next];
            _next = (_next + 1) % _items.Length;

            // Déjà affichée : c'est la plus ancienne, qu'on recycle. Elle passe en bas de la liste
            // visuelle, sous la ligne la plus récente. Au premier tour, les lignes sont activées
            // dans l'ordre où elles ont été instanciées, donc déjà au bon rang.
            if (item.gameObject.activeSelf)
            {
                item.transform.SetAsLastSibling();
            }
            else
            {
                item.gameObject.SetActive(true);
            }

            return item;
        }
    }
}
