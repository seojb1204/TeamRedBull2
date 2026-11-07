"""Analyze actual verification CSV; this script never chooses a DDA arm or updates a model."""
import argparse
import csv
from collections import Counter, defaultdict
import json
import os
import tempfile
from pathlib import Path
from statistics import mean
cache = tempfile.mkdtemp(prefix='dda-verification-plot-cache-')
os.environ.setdefault('MPLCONFIGDIR', cache)
os.environ.setdefault('XDG_CACHE_HOME', cache)
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--results', type=Path, default=Path(__file__).resolve().parents[1] / 'Docs/Validation/dda_results')
a = p.parse_args()
root = a.results
raw, checks, summaries, groups = {}, {}, [], {}
for source in ['main', 'pr13']:
    with (root / source / 'battles.csv').open() as f:
        raw[source] = list(csv.DictReader(f))
    with (root / source / 'checks.csv').open() as f:
        checks[source] = list(csv.DictReader(f))
    grouped = defaultdict(list)
    for row in raw[source]:
        if row['attack'] == 'True':
            grouped[(row['profile'], row['boss'], int(row['battle']))].append(row)
    groups[source] = grouped
    for (profile, boss, battle), rows in sorted(grouped.items()):
        summaries.append(dict(source=source, profile=profile, boss=boss, battle=battle,
            assigned_average=mean(int(r['assigned_level']) for r in rows),
            next_average=mean(int(r['next_level']) for r in rows),
            injected_success=mean(float(r['success']) for r in rows),
            a_update_frobenius=float(rows[0]['model_a_delta']), b_update_l2=float(rows[0]['model_b_delta']),
            a_changed=rows[0]['a_before'] != rows[0]['a_after'], b_changed=rows[0]['b_before'] != rows[0]['b_after']))
with (root / 'battle_summary.csv').open('w', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(summaries[0])); w.writeheader(); w.writerows(summaries)
scenarios = []
for source in ['main', 'pr13']:
    for profile, boss in sorted({(r['profile'], r['boss']) for r in raw[source]}):
        series = [r for r in summaries if r['source']==source and r['profile']==profile and r['boss']==boss]
        records = [r for r in raw[source] if r['attack']=='True' and r['profile']==profile and r['boss']==boss]
        violations = sum((profile.startswith('low_') and int(r['next_level'])>int(r['assigned_level'])) or
                         (profile.startswith('high_') and int(r['next_level'])<int(r['assigned_level'])) for r in records)
        scenarios.append(dict(source=source, profile=profile, boss=boss, battles=len(series),
            first_average=series[0]['assigned_average'], fifth_average=series[4]['assigned_average'],
            last_average=series[-1]['assigned_average'], next_average=series[-1]['next_average'],
            assigned_levels=';'.join(str(r['assigned_average']) for r in series),
            attack_pattern_extreme_direction_violations=violations,
            battles_with_A_update=sum(r['a_changed'] for r in series), battles_with_b_update=sum(r['b_changed'] for r in series),
            convergence_evidence='NOT_ESTABLISHED: success supplied by experiment, independent of chosen arm'))
with (root / 'scenario_summary.csv').open('w', newline='') as f:
    w=csv.DictWriter(f,fieldnames=list(scenarios[0]));w.writeheader();w.writerows(scenarios)
report = {source:dict(status_counts=dict(Counter(r['status'] for r in rows)),
    failures=[r for r in rows if r['status'] in ['FAIL','ERROR']],
    simulated_sessions=len(groups[source]),pattern_rows=len(raw[source])) for source,rows in checks.items()}
(root / 'summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))

def series(source, profile, boss='arakus'):
    return sorted([r for r in summaries if r['source']==source and r['profile']==profile and r['boss']==boss],key=lambda r:r['battle'])
colors={'main':'#ac493c','pr13':'#167b78'}
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10})
fig, axes=plt.subplots(2,3,figsize=(15,8),sharex=True,sharey=True)
profiles=['low_30','high_100','improving_20_to_100','mid_70','loss_streak_high','mixed_by_pattern']
for ax,profile in zip(axes.flat,profiles):
    for source in colors:
        rows=series(source,profile)
        ax.plot([r['battle'] for r in rows],[r['assigned_average'] for r in rows],marker='o',markersize=3,color=colors[source],label=source)
    ax.axvline(4.5,color='gray',ls=':',lw=1);ax.axhline(0,color='gray',lw=.5)
    ax.set_title(profile);ax.set_ylim(-2.2,2.2);ax.set_yticks([-2,-1,0,1,2]);ax.grid(alpha=.2)
    ax.set_xlabel('Battle');ax.set_ylabel('Assigned attack-pattern mean')
axes[0,0].legend();fig.suptitle('Arakus: actual production DDA decisions on prescribed synthetic outcomes\nmain f13bb6b vs PR #13 2bcab41; no human-performance or convergence claim',fontsize=13)
fig.tight_layout();fig.savefig(root/'difficulty_comparison.png',dpi=180);plt.close(fig)
fig,axes=plt.subplots(3,2,figsize=(13,10),sharex=True)
for col,profile in enumerate(['high_100','mid_70']):
    for source in colors:
        rows=series(source,profile);xs=[r['battle'] for r in rows]
        for row,key in enumerate(['assigned_average','a_update_frobenius','b_update_l2']):
            axes[row,col].plot(xs,[r[key] for r in rows],marker='o',ms=2,color=colors[source],label=source)
    axes[0,col].set_title(profile);axes[0,col].set_ylim(-2.2,2.2);axes[0,col].legend()
    for row,name in enumerate(['Assigned difficulty mean','A change per battle (Frobenius)','b change per battle (L2)']):
        axes[row,col].set_ylabel(name);axes[row,col].grid(alpha=.2)
        if row > 0:
            axes[row,col].set_ylim(bottom=0)
            axes[row,col].ticklabel_format(axis='y', style='plain', useOffset=False)
    axes[2,col].set_xlabel('Battle')
fig.suptitle('Stable difficulty does not imply stopped learning\nChanges are measured from the actual model arrays; values aggregate all observed patterns of one boss')
fig.tight_layout();fig.savefig(root/'learning_vs_assignment.png',dpi=180);plt.close(fig)
fig,axes=plt.subplots(1,5,figsize=(18,4),sharex=True,sharey=True)
for ax,boss in zip(axes,['arakus','phanes','adicus','aethelos','kharos']):
    for source in colors:
        rows=series(source,'improving_20_to_100',boss)
        ax.plot([r['battle'] for r in rows],[r['assigned_average'] for r in rows],color=colors[source],label=source)
    ax.axvline(8.5,color='gray',ls=':');ax.axvline(12.5,color='gray',ls=':');ax.set_title(boss);ax.set_ylim(-2.2,2.2);ax.grid(alpha=.2);ax.set_xlabel('Battle')
axes[0].set_ylabel('Assigned attack-pattern mean');axes[0].legend()
fig.suptitle('Prescribed improvement: 20% (1-8), 70% (9-12), 100% (13-24)\nPolicy response only; supplied success is not an observed gameplay result')
fig.tight_layout();fig.savefig(root/'all_boss_improvement.png',dpi=180);plt.close(fig)
print(json.dumps(report,ensure_ascii=False,indent=2))
