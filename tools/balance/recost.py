# -*- coding: utf-8 -*-
"""Aligne la croissance des COUTS des generateurs sur celle de leurs RENDEMENTS.

Ne touche QU'AUX COUTS. Comprimer aussi les rendements rabaisse le plafond de l'economie
et bloque la progression plus tot encore (mesure du 2026-09-12 : campagne tombee de
1,85e11 a 2,2e8 Datas).

Le palier 1 est multiplie par 1 : le debut de partie est strictement intact.

Usage : python tools/balance/recost.py <croissance scripts> <croissance hardware>
"""
import json, io, sys, collections

TARGETS = {'scripts': float(sys.argv[1]), 'hardware': float(sys.argv[2])}

for name, target in TARGETS.items():
    p = 'Assets/GameData/Editor/UpgradeData/%s.json' % name
    doc = json.load(io.open(p, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
    items = doc['items']
    n = len(items) - 1

    cost = (items[-1]['baseCost'] / items[0]['baseCost']) ** (1.0 / n)
    yld = (items[-1]['baseProductionYield'] / items[0]['baseProductionYield']) ** (1.0 / n)

    for i, it in enumerate(items):
        it['baseCost'] = float('%.6g' % (it['baseCost'] * (target / cost) ** i))

    print('%-9s cout x%.2f -> x%.2f  (rendement x%.2f, rapport %.2f -> %.2f)  palier 15 : %.3e'
          % (name, cost, target, yld, yld / cost, yld / target, items[-1]['baseCost']))
    io.open(p, 'w', encoding='utf-8', newline='').write(json.dumps(doc, ensure_ascii=False, indent=2))
