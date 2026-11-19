"""Browser tests with actual C# HEADLESS publisher files. Screenshots always FIXTURE/REPLAY.
Unit live/stale behavior is tested separately; this never claims Unity or Windows execution.
"""
import argparse,json,time
from pathlib import Path
from playwright.sync_api import sync_playwright
p=argparse.ArgumentParser();p.add_argument('--url',required=True);p.add_argument('--fixture-root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--chromium',default='/usr/bin/chromium');a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
with sync_playwright() as pw:
 browser=pw.chromium.launch(executable_path=a.chromium,headless=True,args=['--no-sandbox']);page=browser.new_page(viewport={'width':1600,'height':1100});errors=[];page.on('pageerror',lambda e:errors.append(str(e)));page.goto(a.url);initial_token=page.evaluate('token');page.request.post(a.url+'/api/monitor/disconnect',headers={'X-Lab-Token':initial_token},data={});page.click('#tab-monitor');page.wait_for_function("document.querySelector('#live-badge').textContent==='DISCONNECTED'");assert page.locator('#live-attack').inner_text().startswith('연결 없음')
 token=page.evaluate('token');assert page.request.get(a.url+'/api/monitor/state').status==403
 page.fill('#monitor-root',str(a.fixture_root));page.click('#monitor-connect');page.wait_for_function("document.querySelector('#live-badge').textContent==='FIXTURE'");assert 'HEADLESS' in page.locator('#live-source').inner_text();assert page.locator('#source-badge').inner_text()=='FIXTURE';assert '현재 공격' not in page.locator('#live-attack').inner_text() or '미확인' in page.locator('#live-attack').inner_text()
 assert page.locator('#live-end tr').count()>0;assert page.locator('#live-patterns tr').count()>0
 with page.expect_download() as dl:page.click('#monitor-export')
 replay=a.output/'actual-headless-recording.json';dl.value.save_as(str(replay));data=json.loads(replay.read_text());assert data['schema']=='tower.dda.replay/1';assert data['frames'];assert all(f['origin']=='headless-fixture' for f in data['frames']);assert any(f['kind']=='attack-applied' for f in data['frames']);assert any(f['phase']=='tutorial' for f in data['frames'])
 with page.expect_download() as dl:page.click('#monitor-csv')
 dl.value.save_as(str(a.output/'actual-observations.csv'));assert 'capturedUtc' in (a.output/'actual-observations.csv').read_text(encoding='utf-8-sig')
 page.click('#monitor-import');page.set_input_files('#monitor-file',str(replay));page.wait_for_function("document.querySelector('#live-badge').textContent==='REPLAY'");assert '실시간' in page.locator('#live-message').inner_text();assert page.locator('#monitor-replay-controls').is_visible()
 attack=next(i for i,f in enumerate(data['frames']) if f['hasAttack']);page.locator('#monitor-seek').evaluate('(el,v)=>{el.value=v;el.dispatchEvent(new Event("input"))}',str(attack));assert '기록' in page.locator('#attack-label').inner_text();assert 'Lv' in page.locator('#live-attack').inner_text();assert page.locator('#live-end tr').count()==0;page.click('#presentation');page.screenshot(path=str(a.output/'monitor-replay-attack-desktop.png'),full_page=True);page.click('#presentation')
 learned=next(i for i,f in enumerate(data['frames']) if f.get('learning') and any(p['learned'] for p in f['learning']['patterns']));page.locator('#monitor-seek').evaluate('(el,v)=>{el.value=v;el.dispatchEvent(new Event("input"))}',str(learned));assert page.locator('#live-end tr').count()>0;page.screenshot(path=str(a.output/'monitor-replay-learning.png'),full_page=True)
 tutorial=next(i for i,f in enumerate(data['frames']) if f['phase']=='tutorial');page.locator('#monitor-seek').evaluate('(el,v)=>{el.value=v;el.dispatchEvent(new Event("input"))}',str(tutorial));assert page.locator('#tutorial-score').inner_text()!='—';assert '3.0s' in page.locator('#tutorial-active').inner_text();page.screenshot(path=str(a.output/'monitor-replay-tutorial.png'),full_page=True)
 page.set_viewport_size({'width':390,'height':844});page.screenshot(path=str(a.output/'monitor-mobile.png'),full_page=True);assert page.locator('#live-badge').inner_text()=='REPLAY';page.set_viewport_size({'width':1600,'height':1100})
 page.click('#tab-simulation');assert page.locator('#source-badge').inner_text().startswith('SIMULATION');assert page.locator('#form').is_visible();page.click('#tab-monitor');assert page.locator('#live-badge').inner_text()=='REPLAY'
 # Existing unlinked files never masquerade as a current battle, even after a fresh write.
 page.fill('#monitor-root',str(a.fixture_root.parent/'save'));page.click('#monitor-connect');page.wait_for_function("document.querySelector('#monitor-session').options.length>1");options=page.locator('#monitor-session option').evaluate_all('(xs)=>xs.map(x=>x.value)');legacy=next(x for x in options if x.startswith('legacy:'));page.select_option('#monitor-session',legacy);page.wait_for_function("document.querySelector('#live-badge').textContent==='REPLAY'");assert page.locator('#legacy-rows tr').count()>0;assert page.locator('#live-attack').inner_text()=='실시간 미수집';page.locator('details').filter(has=page.locator('#legacy-rows')).locator('summary').click();assert '사건 발생 시각이 아닙니다' in page.locator('#legacy-time').inner_text()
 assert page.request.post(a.url+'/api/monitor/replay',headers={'X-Lab-Token':token},data={'schemaVersion':1}).status==400
 assert page.request.get(a.url+'/api/result?id=../../progress.json').status==404
 page.click('#monitor-disconnect');page.wait_for_function("document.querySelector('#live-badge').textContent==='DISCONNECTED'");page.screenshot(path=str(a.output/'monitor-disconnected.png'),full_page=True)
 assert not errors,errors
 print('MONITOR_UI_PASS actual C# HEADLESS files, source separation, replay seek, attack/learning/tutorial, CSV/JSON, legacy unknown timing, disconnect, narrow viewport; NOT Unity/Windows')
 browser.close()
