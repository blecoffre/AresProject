"""Pourquoi la campagne bloque-t-elle a 18 % ? Qui reste non achete, et pourquoi ?"""
from ares_sim import Cfg, Meta
from ares_agent import campaign

cfg = Cfg(force_harden_required_level=1)
res = campaign(cfg, max_hours=16, max_runs=300)
meta, runs = res["meta"], res["runs"]

print(f"CAMPAGNE : {len(runs)} runs, {res['total_t']/3600:.2f} h, arbre {res['progress']*100:.1f} %")
print(f"cycles en banque a la fin : {meta.cycles:,.1f}")
print()
print(f"{'run':>4} {'duree':>8} {'Datas':>11} {'cycles':>9} {'banque':>10} {'HARDEN':>14}")
print("-" * 62)
for i, x in enumerate(runs):
    print(f"{i+1:>4} {x['t']/60:>7.1f}m {x['money']:>11.2e} {x['cycles']:>9,.1f} "
          f"{'':>10} {str(x['harden']):>14}")

print()
print("NOEUDS NON MAXES, et ce qui les bloque")
print(f"{'noeud':>24} {'niv':>4}/{'max':<4} {'cout prochain':>14}  {'verrou':>28}")
print("-" * 82)
shown = 0
for nid in sorted(meta.nodes):
    lv, mx = meta.levels[nid], meta.max_level(nid)
    if lv >= mx:
        continue
    cost = meta.node_cost(nid)
    if meta.unlocked(nid):
        lock = "ouvert — trop cher" if cost > meta.cycles else "OUVERT ET PAYABLE (?)"
    else:
        pre = meta.nodes[nid]["prerequisiteId"]
        lock = f"{pre} niv.{meta.levels[pre]}/{meta.required_level(nid)}"
    print(f"{nid:>24} {lv:>4}/{mx:<4} {cost:>14,.1f}  {lock:>28}")
    shown += 1
    if shown >= 18:
        print(f"{'...':>24}  (+{sum(1 for n in meta.nodes if meta.levels[n] < meta.max_level(n)) - shown} autres)")
        break
