"""Optional offline gallery smoke test. Requires Playwright and a Chromium executable."""
from pathlib import Path
import argparse, json, shutil, threading
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from functools import partial
from playwright.sync_api import sync_playwright

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--chromium',default=shutil.which('chromium'))
a=p.parse_args();doc=Path(__file__).resolve().parent
server=ThreadingHTTPServer(('127.0.0.1',0),partial(SimpleHTTPRequestHandler,directory=str(doc)))
threading.Thread(target=server.serve_forever,daemon=True).start()
base='http://127.0.0.1:'+str(server.server_port)
with sync_playwright() as pw:
    options={'headless':True,'args':['--no-sandbox','--disable-dev-shm-usage','--disable-background-networking']}
    if a.chromium:options['executable_path']=a.chromium
    browser=pw.chromium.launch(**options)
    page=browser.new_page(viewport={'width':1440,'height':1000})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('**/*',lambda route:route.continue_() if route.request.url.startswith(base+'/') else route.abort())
    page.goto(base+'/review.html')
    assert len(page.locator('main').evaluate('(e)=>getComputedStyle(e).gridTemplateColumns').split())==2
    values=page.locator('#view option').evaluate_all('(nodes)=>nodes.map(n=>n.value)')
    results=[]
    for suffix in values:
        page.select_option('#view',suffix)
        page.wait_for_function('Array.from(document.images).every(i=>i.complete && i.naturalWidth>0)')
        sizes=page.locator('main img').evaluate_all('(nodes)=>nodes.map(i=>[i.naturalWidth,i.naturalHeight])')
        assert len(sizes)==2 and all(w>0 and h>0 for w,h in sizes)
        results.append({'view':suffix,'loaded_image_sizes':sizes})
    page.set_viewport_size({'width':700,'height':900})
    assert page.locator('main').evaluate('(e)=>getComputedStyle(e).gridTemplateColumns').count(' ')==0
    assert not errors,errors
    browser.close()
server.shutdown();server.server_close()
(doc/'gallery_test_report.json').write_text(json.dumps({'views_loaded':results,'mobile_single_column':True,'page_errors':errors,'note':'Local HTML documentation test only; no Unity or game integration.'},indent=2))
print('ART_GALLERY_TEST_PASSED',len(results),'view modes, desktop/mobile layout, zero page errors')
