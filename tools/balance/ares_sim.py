"""
Simulateur d'equilibrage d'Ares — miroir fidele du C#.

Vit DANS LE DEPOT et non dans un scratchpad : la version precedente avait ete ecrite dans un
dossier temporaire de session, et a disparu avec lui (voir docs/game-design.md).

Il lit BalancingConfig.asset et les JSON de GameData DIRECTEMENT : aucune valeur n'est recopiee
ici, donc le simulateur ne peut pas deriver du projet.

Sources repliquees :
  UpgradeModel.RecalculateCache        rendement, duree, trace, plafond, cout
  UpgradeManager.RecalculateTotals     TFlops, dissipation, synergie
  SimulationTicker.Tick                reduction saturante, debit
  ThreatManager                        jauge absolue, plafond dynamique
  PrestigeManager                      bonus, IsUnlocked (avec PrestigeRequirement)
  UserCurrencies.CalculatePendingCpuCycles
"""
import json
import math
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UPGRADE_DIR = os.path.join(ROOT, "Assets/GameData/Editor/UpgradeData")
PRESTIGE_DIR = os.path.join(ROOT, "Assets/GameData/Editor/PrestigeData")
CONFIG_ASSET = os.path.join(ROOT, "Assets/GameData/Balancing/BalancingConfig.asset")

MAX_TARGETED_REDUCTION = 0.95
MIN_COST_MULTIPLIER = 1.01
REQUIRE_PARENT_MAX_LEVEL = 0   # miroir de PrestigeRequirement.RequireParentMaxLevel


# ---------------------------------------------------------------------------
# Config : lue depuis l'asset, pas recopiee
# ---------------------------------------------------------------------------
class Cfg:
    def __init__(self, **overrides):
        raw = {}
        with open(CONFIG_ASSET, encoding="utf-8") as fh:
            for line in fh:
                m = re.match(r"^  _(\w+):\s*(-?[\d.eE+]+)\s*$", line)
                if m:
                    raw[m.group(1)] = float(m.group(2))

        self.base_trace_cap = raw["baseTraceCap"]
        self.max_trace_reduction = raw["maxTraceReduction"]
        self.dissipation_half_point = raw["dissipationHalfPointRatio"]
        self.trace_yield_exponent = raw["traceYieldExponent"]
        self.tflops_time_compression = raw["tflopsTimeCompression"]
        self.tflops_compression_exponent = raw["tflopsCompressionExponent"]
        self.tflops_yield_exponent = raw["tflopsYieldExponent"]
        self.abs_min_cycle_duration = raw["absoluteMinCycleDuration"]
        self.proxy_synergy_per_level = raw["proxySynergyPerLevel"]
        self.base_starting_money = raw["baseStartingMoney"]
        self.money_per_cpu_cycle = raw["moneyPerCpuCycle"]
        self.clean_exit_multiplier = raw["cleanExitMultiplier"]

        # Modele du joueur : seule friction du jeu manuel qui compte pour le rythme initial.
        self.manual_relaunch_delay = 0.4
        # Leviers d'experimentation. 1.0 / None = donnees du projet inchangees.
        self.prestige_cost_scale = 1.0
        self.force_harden_required_level = None   # None = ce que disent les JSON
        self.force_required_level_all = None      # met TOUS les paliers a cette valeur

        for k, v in overrides.items():
            if not hasattr(self, k):
                raise AttributeError(f"Cfg n'a pas de champ '{k}'")
            setattr(self, k, v)


# ---------------------------------------------------------------------------
# Meta-progression (miroir de PrestigeManager)
# ---------------------------------------------------------------------------
class Meta:
    def __init__(self, cfg):
        self.cfg = cfg
        self.nodes = {}
        for f in sorted(os.listdir(PRESTIGE_DIR)):
            if not f.endswith(".json"):
                continue
            with open(os.path.join(PRESTIGE_DIR, f), encoding="utf-8") as fh:
                for it in json.load(fh).get("items", []):
                    # Miroir de PrestigeItemData.requiredLevel = 1 : un champ absent vaut 1.
                    it.setdefault("requiredLevel", 1)
                    if cfg.force_required_level_all is not None:
                        it["requiredLevel"] = cfg.force_required_level_all
                    if (cfg.force_harden_required_level is not None
                            and it["id"].startswith("P_HARDEN_")
                            and it.get("prerequisiteId", "").startswith("P_HARDEN_")):
                        it["requiredLevel"] = cfg.force_harden_required_level
                    self.nodes[it["id"]] = it
        self.levels = {k: 0 for k in self.nodes}
        self.cycles = 0.0
        self.recompute()

    def max_level(self, nid):
        return int(self.nodes[nid].get("maxLevel", 1))

    def node_cost(self, nid):
        n = self.nodes[nid]
        lv = self.levels[nid]
        if lv >= self.max_level(nid):
            return None
        return n["baseCost"] * (n.get("costMult", 1.0) ** lv) * self.cfg.prestige_cost_scale

    def required_level(self, nid):
        """Miroir de PrestigeRequirement.ResolveRequiredLevel()."""
        pre = self.nodes[nid].get("prerequisiteId") or ""
        if not pre or pre not in self.nodes:
            return 0
        raw = int(self.nodes[nid].get("requiredLevel", 1))
        parent_max = self.max_level(pre)
        return parent_max if raw <= REQUIRE_PARENT_MAX_LEVEL else min(raw, parent_max)

    def unlocked(self, nid):
        pre = self.nodes[nid].get("prerequisiteId") or ""
        if not pre or pre not in self.nodes:
            return True
        return self.levels.get(pre, 0) >= self.required_level(nid)

    def buy(self, nid):
        c = self.node_cost(nid)
        if c is None or not self.unlocked(nid) or c > self.cycles:
            return False
        self.cycles -= c
        self.levels[nid] += 1
        self.recompute()
        return True

    def tree_complete(self):
        return all(self.levels[k] >= self.max_level(k) for k in self.nodes)

    def progress(self):
        owned = sum(self.levels.values())
        total = sum(self.max_level(k) for k in self.nodes)
        return owned / total

    def recompute(self):
        self.compute_bonus = 0.0
        self.trace_reduction = 0.0
        self.trace_capacity_bonus = 0.0
        self.cost_mult_reduction = 0.0
        self.starting_money = 0.0
        self.starting_power = 0.0
        self.specific = {}
        for nid, n in self.nodes.items():
            lv = self.levels[nid]
            if lv <= 0:
                continue
            tot = float(n.get("bonus", 0.0)) * lv
            bt, tgt = n["bonusType"], (n.get("targetUpgradeId") or "")
            if bt == "GlobalComputeMultiplier":
                self.compute_bonus += tot
            elif bt == "TraceReduction":
                self.trace_reduction += tot
            elif bt == "TraceCapacityMultiplier":
                self.trace_capacity_bonus += tot
            elif bt == "CostMultiplierReduction":
                self.cost_mult_reduction += tot
            elif bt == "StartingMoney":
                self.starting_money += tot
            elif bt == "StartingComputerPower":
                self.starting_power += tot
            elif bt.startswith("SpecificUpgrade"):
                d = self.specific.setdefault(tgt, dict(cost=0.0, yld=0.0, time=0.0, auto=0.0))
                if bt.endswith("CostReduction"):
                    d["cost"] += tot
                elif bt.endswith("YieldBoost"):
                    d["yld"] += tot
                elif bt.endswith("TimeReduction"):
                    d["time"] += tot
                else:
                    d["auto"] += tot
        self.global_compute_multiplier = 1.0 + self.compute_bonus
        self.trace_reduction_multiplier = max(0.1, 1.0 - self.trace_reduction)
        self.trace_capacity_multiplier = max(1.0, 1.0 + self.trace_capacity_bonus)

    def bonuses_for(self, uid):
        return self.specific.get(uid, dict(cost=0.0, yld=0.0, time=0.0, auto=0.0))


# ---------------------------------------------------------------------------
# Generateur (miroir d'UpgradeModel)
# ---------------------------------------------------------------------------
_RAW = None


def _load_upgrades():
    global _RAW
    if _RAW is None:
        _RAW = []
        for f in ("scripts.json", "hardware.json", "proxys.json"):
            with open(os.path.join(UPGRADE_DIR, f), encoding="utf-8") as fh:
                _RAW.extend(json.load(fh)["items"])
    return _RAW


class Up:
    def __init__(self, raw, cfg):
        self.c = cfg
        self.id = raw["id"]
        self.type = raw["type"]
        self.base_cost = float(raw["baseCost"])
        self.cost_mult = float(raw["costMultiplier"])
        self.base_yield = float(raw.get("baseProductionYield", 0.0))
        self.base_duration = float(raw.get("baseCycleDuration", 0.0))
        self.base_trace = float(raw.get("traceGeneratedPerSecond", 0.0))
        self.base_cap = float(raw.get("traceCapIncrease", 0.0))
        self.automation_level = int(raw.get("automationLevel", 10))
        self.ms = sorted(raw.get("milestones", []), key=lambda m: m["level"])
        self.level = 0
        self.tflops = 0.0
        self.synergy = 1.0
        self.b = dict(cost=0.0, yld=0.0, time=0.0, auto=0.0)
        self.gcmr = 0.0
        self._recalc()

    def set_meta(self, meta):
        self.b = meta.bonuses_for(self.id)
        self.gcmr = meta.cost_mult_reduction
        self._recalc()

    def push(self, tflops, synergy):
        if tflops != self.tflops or synergy != self.synergy:
            self.tflops, self.synergy = tflops, synergy
            self._recalc()

    def add(self, n):
        self.level += n
        self._recalc()

    @property
    def owned(self):
        return self.level > 0

    @property
    def automated(self):
        return self.level >= max(1, self.automation_level - round(self.b["auto"]))

    def cost_next(self):
        return self.adj_base_cost * self.cost_mult_eff ** self.level

    def cum_cost(self, n):
        if n <= 0:
            return 0.0
        c0 = self.cost_next()
        if n == 1:
            return c0
        m = self.cost_mult_eff
        return c0 * (m ** n - 1.0) / (m - 1.0)

    def afford(self, budget, cap=200):
        c0 = self.cost_next()
        if budget < c0 or c0 <= 0:
            return 0
        m = self.cost_mult_eff
        n = max(1, min(cap, int(math.log(1 + budget * (m - 1) / c0) / math.log(m))))
        while n > 1 and self.cum_cost(n) > budget:
            n -= 1
        while n < cap and self.cum_cost(n + 1) <= budget:
            n += 1
        return n

    def _recalc(self):
        c, lvl = self.c, self.level
        power = float(lvl)
        dur = self.base_duration * (1.0 - min(MAX_TARGETED_REDUCTION, self.b["time"]))
        tmul = 1.0
        for m in self.ms:
            if lvl < m["level"]:
                continue
            f, e = float(m["factor"]), m["effect"]
            if e == "YieldMultiplier":
                power *= f
            elif e == "DurationMultiplier":
                dur *= f
            elif e == "TraceMultiplier":
                tmul *= f

        self.power = power
        tyf = (1.0 + self.tflops) ** c.tflops_yield_exponent if self.type == "Script" else 1.0
        self.yield_ = self.base_yield * (1.0 + self.b["yld"]) * power * tyf

        a = c.trace_yield_exponent
        if self.type == "Script":
            d = dur / (1.0 + self.tflops * c.tflops_time_compression) ** c.tflops_compression_exponent
            d /= self.synergy
            self.duration = max(c.abs_min_cycle_duration, d)
            if lvl > 0:
                self.trace_per_cycle = (self.base_trace * self.base_duration
                                        * (power * tyf) ** a * tmul)
                self.trace_ps = self.trace_per_cycle / self.duration
            else:
                self.trace_per_cycle = self.trace_ps = 0.0
            self.cap_inc = self.dissip = 0.0
        elif self.type == "Hardware":
            self.duration = self.trace_per_cycle = self.dissip = 0.0
            self.trace_ps = (self.base_trace * power ** a * tmul) if lvl > 0 else 0.0
            self.cap_inc = (self.base_cap * power ** a) if lvl > 0 else 0.0
        else:  # Proxy
            self.duration = self.trace_ps = self.cap_inc = self.trace_per_cycle = 0.0
            self.dissip = self.base_trace * power * (1.0 + self.b["yld"]) * tmul

        self.adj_base_cost = self.base_cost * (1.0 - min(MAX_TARGETED_REDUCTION, self.b["cost"]))
        self.cost_mult_eff = max(MIN_COST_MULTIPLIER, self.cost_mult - self.gcmr)


# ---------------------------------------------------------------------------
# Une run (miroir de SimulationTicker + ThreatManager)
# ---------------------------------------------------------------------------
class Run:
    def __init__(self, cfg, meta):
        self.c = cfg
        self.meta = meta
        self.ups = [Up(r, cfg) for r in _load_upgrades()]
        for u in self.ups:
            u.set_meta(meta)
        self.scripts = [u for u in self.ups if u.type == "Script"]
        self.hardware = [u for u in self.ups if u.type == "Hardware"]
        self.proxies = [u for u in self.ups if u.type == "Proxy"]
        self.by_id = {u.id: u for u in self.ups}
        self.money = cfg.base_starting_money + meta.starting_money
        self.run_money = 0.0
        self.trace = 0.0
        self.t = 0.0
        self.recalc()

    def recalc(self):
        c, m = self.c, self.meta
        hw = sum(u.yield_ for u in self.hardware)
        t = (hw + m.starting_power) * m.global_compute_multiplier
        syn = 1.0 + sum(u.level for u in self.proxies) * c.proxy_synergy_per_level
        for u in self.ups:
            u.push(t, syn)
        self.tflops = t
        self.trace_cap = ((c.base_trace_cap + sum(u.cap_inc for u in self.hardware))
                          * m.trace_capacity_multiplier)
        compression = (1.0 + t * c.tflops_time_compression) ** c.tflops_compression_exponent
        self.dissipation = (sum(u.dissip for u in self.proxies)
                            * (1.0 + math.log10(1.0 + t)) * compression)

    def _eff_duration(self, u):
        return u.duration + (0.0 if u.automated else self.c.manual_relaunch_delay)

    def mps(self):
        return sum(u.yield_ / self._eff_duration(u) for u in self.scripts if u.owned)

    def brute(self):
        s = sum(u.trace_per_cycle / self._eff_duration(u) for u in self.scripts if u.owned)
        s += sum(u.trace_ps for u in self.hardware)
        return s * self.meta.trace_reduction_multiplier

    def reduction(self):
        b = self.brute()
        if b <= 0 or self.dissipation <= 0:
            return 0.0
        c = self.c
        return c.max_trace_reduction * self.dissipation / (
            self.dissipation + c.dissipation_half_point * b)

    def debit(self):
        return self.brute() * (1.0 - self.reduction())

    def cycles(self):
        return math.floor(math.sqrt(self.run_money / self.c.money_per_cpu_cycle))

    def buy(self, u, n):
        cost = u.cum_cost(n)
        if cost > self.money:
            return False
        self.money -= cost
        u.add(n)
        self.recalc()
        return True

    def step(self, dt):
        gain = self.mps() * dt
        self.money += gain
        self.run_money += gain
        self.trace = min(self.trace_cap, self.trace + self.debit() * dt)
        self.t += dt

    @property
    def dead(self):
        return self.trace >= self.trace_cap
