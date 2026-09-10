"""
1) Controle de fidelite : le simulateur reproduit-il les chiffres consignes dans game-design.md ?
2) Mesure de la dette ouverte : effet du palier `requiredLevel` de la chaine Blindage.
"""
from ares_sim import Cfg, Meta
from ares_agent import play_run, campaign

REFERENCE = [
    ("SCR_01 niveau 10", "90 s"),
    ("Datas a 5 min", "33 760"),
    ("Duree run 1", "27,0 min"),
    ("Campagne / runs", "9,93 h / 19"),
]

print("=" * 78)
print("1. CONTROLE DE FIDELITE — valeurs consignees dans docs/game-design.md")
print("=" * 78)
cfg = Cfg()
r = play_run(cfg, Meta(cfg), probe_at=300.0)
top = max((int(u.id.split("_")[1]) for u in r["run"].scripts if u.level > 0), default=0)
print(f"  {'mesure':<22} {'attendu':>12}   {'obtenu':>12}")
print("  " + "-" * 52)
auto = f"{r['t_auto']:.0f} s" if r["t_auto"] else "jamais"
print(f"  {'SCR_01 niveau 10':<22} {'90 s':>12}   {auto:>12}")
print(f"  {'Datas a 5 min':<22} {'33 760':>12}   {r['probe_money']:>12,.0f}")
print(f"  {'Duree run 1':<22} {'27,0 min':>12}   {r['t']/60:>10.1f} min")
print(f"  {'plus haut Script':<22} {'SCR_05':>12}   {'SCR_%02d' % top:>12}")

print()
print("=" * 78)
print("2. EFFET DU PALIER SUR LA CHAINE BLINDAGE")
print("=" * 78)
print(f"  {'requiredLevel':>14} | {'runs':>5} {'duree':>8} {'arbre':>9} | "
      f"{'run med':>8} | {'HARDEN_4 ouvert':>16} | {'Datas fin':>11}")
print("  " + "-" * 84)

for label, forced in (("1 (avant)", 1), ("3 (retenu)", 3), ("0 (max)", 0)):
    c = Cfg(force_harden_required_level=forced)
    res = campaign(c, max_hours=16, max_runs=300)
    runs = res["runs"]
    d = sorted(x["t"] for x in runs)
    etat = "COMPLET" if res["complete"] else f"{res['progress']*100:.0f}%"

    opened = next((i + 1 for i, x in enumerate(runs) if x["harden"][3] > 0), None)
    print(f"  {label:>14} | {len(runs):>5} {res['total_t']/3600:>7.2f}h {etat:>9} | "
          f"{d[len(d)//2]/60:>7.1f}m | "
          f"{(f'run {opened}' if opened else 'jamais'):>16} | {runs[-1]['money']:>11.2e}")

print()
print("  « HARDEN_4 ouvert » = la run ou le noeud le plus puissant de la branche")
print("  (x100 le bonus de HARDEN_1) recoit son premier rang.")
