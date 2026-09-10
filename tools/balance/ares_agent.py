"""
Le « joueur parfait » et le pilote de campagne.

C'est un agent GLOUTON : il achete au meilleur gain immediat par euro depense. Il joue donc mieux
qu'un debutant et moins bien qu'un optimiseur — les chiffres qu'il produit sont un ordre de
grandeur fiable, pas une verite. Toute conclusion tiree d'ici doit etre lue comme telle.
"""
from ares_sim import Meta, Run


def best_offense(run):
    """Generateur au meilleur gain de $/s par $ depense. None si rien n'est payable."""
    base = run.mps()
    best, best_score, best_n = None, 0.0, 0
    for u in run.ups:
        if u.type == "Proxy" or u.cost_next() > run.money:
            continue
        n = u.afford(run.money, 100)
        if n <= 0:
            continue
        cost = u.cum_cost(n)
        keep = u.level
        u.add(n)
        run.recalc()
        gain = run.mps() - base
        u.level = keep
        u._recalc()
        run.recalc()
        if cost > 0 and gain / cost > best_score:
            best, best_score, best_n = u, gain / cost, n
    return best, best_n


def best_defense(run):
    """Proxy au meilleur gain de dissipation par $ depense."""
    base = run.dissipation
    best, best_score, best_n = None, 0.0, 0
    for u in run.proxies:
        if u.cost_next() > run.money:
            continue
        n = u.afford(run.money, 100)
        if n <= 0:
            continue
        cost = u.cum_cost(n)
        keep = u.level
        u.add(n)
        run.recalc()
        gain = run.dissipation - base
        u.level = keep
        u._recalc()
        run.recalc()
        if cost > 0 and gain / cost > best_score:
            best, best_score, best_n = u, gain / cost, n
    return best, best_n


def survival_seconds(run):
    d = run.debit()
    return float("inf") if d <= 0 else (run.trace_cap - run.trace) / d


def play_run(cfg, meta, safety_seconds=240.0, exit_at=0.95, max_seconds=9000.0,
             plateau_ratio=1.6, plateau_window=0.35, probe_at=None):
    """Joue UNE run jusqu'a l'exfiltration ou la saisie.

    Sortie sur PLATEAU en plus du seuil de jauge : les CPU Cycles allant en racine carree des
    Datas, un joueur optimal repart a neuf plutot que de gratter une run qui ne monte plus.
    """
    run = Run(cfg, meta)
    scr01 = run.by_id.get("SCR_01")
    next_decision = 0.0
    first_cycle_at = None
    t_auto = None
    probe_money = None
    peak_reduction = 0.0
    mark_t, mark_money = 0.0, 0.0

    def result(clean, timeout=False):
        return dict(run=run, t=run.t, clean=clean, cycles=run.cycles() * (
            cfg.clean_exit_multiplier if clean else 1.0),
            first_cycle_at=first_cycle_at, t_auto=t_auto, probe_money=probe_money,
            peak_reduction=peak_reduction, timeout=timeout, money=run.run_money)

    while run.t < max_seconds:
        dt = min(max(0.25, run.t / 400.0), 5.0)
        s = survival_seconds(run)
        if s < 30:
            dt = min(dt, max(0.1, s / 20.0))
        run.step(dt)

        if t_auto is None and scr01 is not None and scr01.level >= 10:
            t_auto = run.t
        if probe_at is not None and probe_money is None and run.t >= probe_at:
            probe_money = run.run_money
        if first_cycle_at is None and run.cycles() >= 1:
            first_cycle_at = run.t
        if run.dead:
            return result(False)

        if run.t - mark_t >= plateau_window * max(run.t, 1.0):
            grown = run.run_money / max(mark_money, 1e-12)
            plateaued = mark_money > 0 and grown < plateau_ratio
            mark_t, mark_money = run.t, run.run_money
            if plateaued and run.cycles() >= 1:
                return result(True)

        if run.trace >= exit_at * run.trace_cap and run.cycles() >= 1:
            return result(True)

        if run.t < next_decision:
            continue
        next_decision = run.t + min(max(0.5, run.t / 200.0), 10.0)

        for _ in range(12):
            if survival_seconds(run) >= safety_seconds:
                break
            u, n = best_defense(run)
            if u is None or not run.buy(u, n):
                break
        peak_reduction = max(peak_reduction, run.reduction())

        for _ in range(12):
            u, n = best_offense(run)
            if u is None or not run.buy(u, n):
                break

    return result(False, timeout=True)


def spend_cycles(meta):
    """Achete les noeuds debloques les moins chers d'abord.

    Monotone : dans cet arbre aucun noeud n'est un mauvais achat. La regle de niveau requis fait
    le reste — pour ouvrir un enfant, l'agent doit d'abord monter le parent au rang exige.
    """
    bought = 0
    while True:
        opts = []
        for nid in meta.nodes:
            if not meta.unlocked(nid):
                continue
            c = meta.node_cost(nid)
            if c is None or c > meta.cycles:
                continue
            opts.append((c, nid))
        if not opts:
            return bought
        opts.sort()
        if not meta.buy(opts[0][1]):
            return bought
        bought += 1


def campaign(cfg, safety_seconds=240.0, max_hours=16.0, max_runs=300):
    meta = Meta(cfg)
    total_t = 0.0
    runs = []
    first_prestige_at = None
    budget = max_hours * 3600.0

    while total_t < budget and len(runs) < max_runs and not meta.tree_complete():
        r = play_run(cfg, meta, safety_seconds=safety_seconds)
        total_t += r["t"]
        if first_prestige_at is None and r["first_cycle_at"] is not None:
            first_prestige_at = r["first_cycle_at"]
        meta.cycles += r["cycles"]
        spend_cycles(meta)
        runs.append(dict(t=r["t"], cycles=r["cycles"], money=r["money"],
                         red=r["peak_reduction"], total_t=total_t,
                         harden=[meta.levels[f"P_HARDEN_{i}"] for i in (1, 2, 3, 4)]))
        if r["cycles"] < 1 and len(runs) > 3:
            break

    return dict(meta=meta, runs=runs, total_t=total_t,
                first_prestige_at=first_prestige_at,
                complete=meta.tree_complete(), progress=meta.progress())
