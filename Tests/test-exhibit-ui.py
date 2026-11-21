"""Real C# prebuilt browser test; DB failure is honest, HEADLESS is not Unity LIVE."""
import argparse,json
from pathlib import Path
from playwright.sync_api import sync_playwright
p=argparse.ArgumentParser();p.add_argument('--url',required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--chromium',default='/usr/bin/chromium');a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
with sync_playwright() as browser_api:
 browser=browser_api.chromium.launch(executable_path=a.chromium,headless=True,args=['--no-sandbox']);page=browser.new_page(viewport={'width':1450,'height':1000});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.goto(a.url);page.wait_for_function("document.querySelector('#tab-monitor').getAttribute('aria-selected')==='true'")
 token=page.evaluate('token')
 s=page.request.get(a.url+'/api/monitor/state',headers={'X-Lab-Token':token}).json()
 assert s.get('frame') and s['frame']['origin']=='headless-fixture',s
 assert s['status']!='LIVE GAME';assert s.get('choice'),s
 assert page.request.get(a.url+'/api/database/state').status==403
 page.click('#tab-database');page.locator('#database-view').scroll_into_view_if_needed();page.wait_for_function("document.querySelector('#db-status').textContent.includes('조회 실패')",timeout=60000)
 assert '만들' in page.locator('#db-counts').inner_text() or '생성하지' in page.locator('#db-counts').inner_text()
 assert '300명' not in page.locator('#db-counts').inner_text()
 assert '발표용 가상 데이터' in page.locator('#db-source').inner_text()
 page.locator('#database-view').screenshot(path=str(a.output/'database-actual-proxy-failure.png'))
 page.click('#tab-simulation');assert not page.locator('#run').is_disabled();page.select_option('#mode','control');page.fill('#rounds','3');page.click('#run');page.wait_for_function("document.querySelector('#status').textContent.includes('실험 완료')",timeout=120000)
 assert page.locator('#levels polyline').count()==2
 page.click('#replay');page.wait_for_function("document.querySelector('#status').textContent.includes('산출물 동일')",timeout=120000)
 page.locator('#simulation-view').screenshot(path=str(a.output/'prebuilt-real-csharp-simulation.png'))
 page.click('#tab-monitor');assert 'LIVE GAME' not in page.locator('#source-badge').inner_text()
 page.set_viewport_size({'width':390,'height':844});page.click('#tab-database');page.locator('#database-view').screenshot(path=str(a.output/'database-mobile.png'))
 assert not errors,errors
 print('EXHIBIT_UI_PASS auto tab/session from actual HEADLESS files, honest proxy failure, source labels, prebuilt actual C# graph/replay, token checks, mobile; NOT Unity/Windows LIVE')
 browser.close()
