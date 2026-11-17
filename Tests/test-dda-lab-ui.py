"""Optional Playwright/Chromium smoke test against a running local Lab server."""
from playwright.sync_api import sync_playwright
from pathlib import Path
import json,argparse,tempfile
p=argparse.ArgumentParser();p.add_argument("--url",default="http://127.0.0.1:8765");p.add_argument("--chromium");p.add_argument("--output",type=Path);a=p.parse_args();out=a.output or Path(tempfile.mkdtemp(prefix="lab-ui-proof-"));out.mkdir(parents=True,exist_ok=True)
with sync_playwright() as p:
 browser=p.chromium.launch(executable_path=a.chromium,headless=True,args=['--no-sandbox'])
 page=browser.new_page(viewport={'width':1440,'height':1100});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.goto(a.url);page.select_option('#mode','control');page.select_option('#scenario','declining');page.fill('#rounds','3');page.click('#run');page.wait_for_function("document.querySelector('#status').textContent.includes('실험 완료')",timeout=120000)
 assert page.locator('#count').inner_text()=='6'
 assert page.locator('#levels polyline').count()==2
 page.click('#replay');page.wait_for_function("document.querySelector('#status').textContent.includes('산출물 동일')",timeout=120000)
 assert page.locator('#stats tr').count()==6
 page.screenshot(path=str(out/'dashboard-desktop.png'),full_page=True)
 with page.expect_download() as dl:page.click('#json')
 path=str(out/'ui-export.json');dl.value.save_as(path)
 data=json.loads(Path(path).read_text());assert data['manifest']['replay']['identical']
 page.click('#compare');page.set_input_files('#file',path);page.wait_for_function("document.querySelectorAll('#levels polyline').length===4")
 page.select_option('#modelMetric','b');assert page.locator('#model polyline').count()==4
 page.click('#clearCompare');page.select_option('#mode','combat');page.fill('#rounds','2');page.click('#run');page.wait_for_function("document.querySelector('#status').textContent.includes('실험 완료')",timeout=120000)
 assert int(page.locator('#starts').inner_text())>0
 assert page.locator('#conditions tr').count()>0
 page.screenshot(path=str(out/'dashboard-combat.png'),full_page=True)
 page.set_viewport_size({'width':390,'height':844});page.screenshot(path=str(out/'dashboard-mobile.png'),full_page=True)
 assert page.request.post(a.url+'/api/run',data={}).status==403
 token=page.evaluate('token')
 assert page.request.post(a.url+'/api/run',headers={'X-Lab-Token':token},data={'rounds':0}).status==400
 page.click('#import');page.set_input_files('#file',path);page.wait_for_function("document.querySelector('#count').textContent==='6'")
 page.click('#replay');page.wait_for_function("document.querySelector('#status').textContent.includes('실험 완료')",timeout=120000)
 assert page.locator('#count').inner_text()=='6'
 page.click('#import');page.set_input_files('#file',{'name':'invalid.json','mimeType':'application/json','buffer':b'{"schemaVersion":1}'})
 page.wait_for_function("document.querySelector('#status').classList.contains('error')")
 assert page.locator('#count').inner_text()=='6'
 assert not errors,errors
 print('UI_PASS real C# run, replay, plots, export, comparison import, narrow viewport; no JS errors')
 browser.close()
