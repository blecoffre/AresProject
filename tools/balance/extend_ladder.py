# -*- coding: utf-8 -*-
"""Etend une echelle de generateurs a N paliers, en conservant ses deux extremites.

Le palier 1 et le point d'ARRIVEE sont preserves a l'identique : seule la taille des
marches change. Toutes les colonnes geometriques sont re-espacees ensemble — cout,
rendement, trace, duree de cycle — sinon on deplacerait l'equilibre relatif des paliers
en croyant ne toucher qu'a leur nombre.

Usage : python tools/balance/extend_ladder.py <fichier> <prefixe> <nb paliers>
"""
import json, io, sys, collections

GEOMETRIC = ('baseCost', 'baseProductionYield', 'traceGeneratedPerSecond',
             'baseCycleDuration', 'durationReductionPerLevel', 'minCycleDuration',
             'traceCapIncrease')
LINEAR = ('costMultiplier',)

name, prefix, target = sys.argv[1], sys.argv[2], int(sys.argv[3])
p = 'Assets/GameData/Editor/UpgradeData/%s.json' % name
doc = json.load(io.open(p, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
first, last = doc['items'][0], doc['items'][-1]
model = doc['items'][0]

out = []
for i in range(target):
    t = i / (target - 1.0)
    item = collections.OrderedDict()
    for key, value in model.items():
        if key == 'id':
            item[key] = '%s_%02d' % (prefix, i + 1)
        elif key == 'order':
            item[key] = i + 1
        elif key in GEOMETRIC and first.get(key):
            item[key] = float('%.6g' % (first[key] * (last[key] / first[key]) ** t))
        elif key in LINEAR:
            item[key] = float('%.4g' % (first[key] + (last[key] - first[key]) * t))
        else:
            item[key] = value
    out.append(item)

doc['items'] = out
io.open(p, 'w', encoding='utf-8', newline='').write(json.dumps(doc, ensure_ascii=False, indent=2))

g = (out[-1]['baseCost'] / out[0]['baseCost']) ** (1.0 / (target - 1))
print('%-9s %2d paliers  cout x%.2f par marche  |  %s_01 %.3g -> %s_%02d %.3e'
      % (name, target, g, prefix, out[0]['baseCost'], prefix, target, out[-1]['baseCost']))
