(()=>{'use strict';const $=id=>document.getElementById(id);let data=null;
const fmt=x=>typeof x==='number'?x.toFixed(2):'미수집';
function row(id,cells){const tr=document.createElement('tr');for(const x of cells){const td=document.createElement('td');td.textContent=x??'미수집';tr.append(td)}$(id).append(tr)}
function render(){if(!data)return;const a=data.analysis,kind=$('db-source').value;const names={novice:'초보',normal:'일반',expert:'숙련'};
$('db-status').textContent=`${data.status} · 최근 SELECT ${data.lastSelectUtc||'없음'} · ${data.writeStatus||data.error||''} · 조회 가능한 행 기준(RLS 필터 가능)`;
$('db-counts').textContent=a?.counts?JSON.stringify(a.counts[kind])+' · 발표 목표 300프로필/300전투/3300샘플은 실제 조회값과 대조해야 합니다.':'출처별 원시 SELECT 불가 · 건수/평균을 생성하지 않습니다.';
$('db-requests').textContent=JSON.stringify(data.tables||{},null,2)+'\n'+(data.liveSchema||'');
for(const id of ['db-groups','db-patterns','db-bosses','db-mixed','db-chart'])$(id).replaceChildren();
for(const g of a?.groups||[]){if(g.source!==kind)continue;row('db-groups',[names[g.group],g.players,g.validFirst,fmt(g.score),`${fmt(g.activeSeconds)} / ${fmt(g.elapsedSeconds)}`,fmt(g.parryRate),fmt(g.dodgeRate),fmt(g.hits)]);const bar=document.createElement('div');bar.style.cssText=`background:#276b60;min-width:110px;height:${Math.max(25,Math.min(135,g.players))}px;padding:6px`;bar.textContent=`${names[g.group]} ${g.players}프로필`;bar.title=$('db-source').selectedOptions[0].text; $('db-chart').append(bar)}
for(const p of a?.patterns||[]){if(p.source===kind)row('db-patterns',[names[p.group],`${p.boss}/${p.pattern}`,`${p.players}/${p.opportunities}`,`${fmt(p.successRate)} / ${fmt(p.parryRate)} / ${fmt(p.hitRate)}`,p.suggestedLevel,p.eligible?'충족: Lv0 / 3소유자 / 20기회':'표본 부족'])}
for(const b of a?.bosses||[])if(b.source===kind)row('db-bosses',[b.boss,b.battles+'전투',fmt(b.seconds)+'초']);
for(const p of a?.mixedView||[])row('db-mixed',[p.group_name,p.boss,p.pattern,p.players,p.opportunities,p.success_rate,p.suggested_level]);}
async function state(){try{const r=await fetch('/api/database/state',{headers:{'X-Lab-Token':token}});if(!r.ok)throw Error('DB 상태 HTTP '+r.status);data=await r.json();render()}catch(e){$('db-status').textContent=e.message}}
$('db-source').onchange=render;$('db-refresh').onclick=async()=>{try{await fetch('/api/database/refresh',{method:'POST',headers:{'X-Lab-Token':token,'Content-Type':'application/json'},body:'{}'});state()}catch(e){$('db-status').textContent=e.message}};
async function live(){try{const r=await fetch('/api/monitor/state?since=0',{headers:{'X-Lab-Token':token}});const s=await r.json();$('db-game').textContent=s.frame?.cloud?`${s.status} · 캡처 ${s.frame.capturedUtc}\n`+Object.entries(s.frame.cloud).map(([k,v])=>({configuration:'설정',proficiencyEnabled:'숙련도 HTTP 활성화',legacyBattle:'기존 전투 전송',legacyPattern:'기존 패턴 전송',assessment:'숙련도 평가 전송',proficiencyBattle:'숙련도 전투 전송',proficiencySamples:'숙련도 패턴 샘플',pendingLegacy:'기존 큐(마지막 동기화 관측)',pendingProtected:'보호 큐(마지막 동기화 관측)',lastSuccessfulSendUtc:'최근 서버 수락 UTC',lastError:'마지막 전송 오류'}[k]||k)+': '+(v===-1||v===''?'미수집':String(v))).join('\n'):'게임 송신 상태 미수집 · DB SELECT 성공으로 대체하지 않습니다.'}catch{}}
state();setInterval(state,2000);setInterval(live,2000);
})();
