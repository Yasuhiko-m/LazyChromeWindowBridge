import assert from 'node:assert/strict';

export async function testMultiMonitor({ caller, cdp, until, delay, evidence, pageA, pageB }) {
  const topology = await caller('monitors');
  const preferred = process.env.LCWB_TEST_WINDOW_POSITION?.split(',').map(Number);
  const work = topology.find(m => preferred && preferred[0] >= m.bounds.left && preferred[0] < m.bounds.right && preferred[1] >= m.bounds.top && preferred[1] < m.bounds.bottom)?.workArea ?? topology.find(m => m.primary).workArea;
  const outside = r => topology.every(m => r.left + r.width <= m.bounds.left || r.left >= m.bounds.left + m.bounds.width || r.top + r.height <= m.bounds.top || r.top >= m.bounds.top + m.bounds.height);
  const error = (a,b) => Math.max(...['left','top','width','height'].map(k => Math.abs(a[k]-b[k])));
  const geometry = s => caller('geometry', { id:s.appSessionId });
  const monitor = async s => (await caller('monitor')).sessions.find(r => r.appSessionId === s.appSessionId);
  const channels = [[1,0,0],[0,0,1],[0,1,0],[1,1,0],[1,0,1]];
  const rows = [];
  for (let i=0;i<5;i++) {
    const url = pageA.url + '/dynamic-' + 'abcde'[i];
    const made = await caller('launch',url);
    await until(() => geometry(made), g => g?.state === 'Visible', 'native map ' + i);
    const s = (await caller('sessions')).find(s => s.appSessionId === made.appSessionId);
    await until(() => cdp.extension('chrome.tabs.get('+s.tabId+').then(t=>({status:t.status,url:t.url}))'), t=>t.status==='complete'&&t.url===url, 'fixture navigation');
    const normal = { left:work.left+40+i*35,top:work.top+50+i*25,width:1280,height:800 };
    await caller('set-bounds',{id:s.appSessionId,rect:normal});
    rows.push({...s,normal,channels:channels[i]});
  }
  const [a,b,c,d,e] = rows, parkedRows = rows.slice(1);
  await until(() => caller('profile',e.launchUrl), p=>p&&error(p.normal,e.normal)===0,'normal profile stable');
  await caller('monitor-start');
  assert((await caller('monitor')).sessions.every(s=>s.state==='ACTIVE'));
  assert.equal((await caller('monitor')).frames,0);
  for (const s of parkedRows) {
    const parked=await caller('park',{id:s.appSessionId}); assert(outside(parked.current));
  }
  assert.equal(new Set(rows.map(s=>s.appSessionId)).size,5);
  assert.equal(new Set(rows.map(s=>s.windowId)).size,5);
  const initialGeometry=await Promise.all(rows.map(geometry)); // Caller driver is sequential; requests preserve write order.
  assert.equal(new Set(initialGeometry.map(g=>g.identity.hwnd)).size,5);
  // CDP metadata inspection is test-only, restricted to this isolated browser.
  const ownedTabs=()=>rows.map(s=>s.tabId);
  async function attached() {
    return cdp.extension('chrome.debugger.getTargets().then(ts=>ts.filter(t=>'+JSON.stringify(ownedTabs())+'.includes(t.tabId)&&t.attached).map(t=>t.tabId))');
  }
  async function stats() {
    return caller('process-stats',{pids:(await cdp.call('SystemInfo.getProcessInfo')).processInfo.map(p=>p.id)});
  }
  async function ready(targets=parkedRows) {
    await until(()=>caller('monitor'), m=>targets.every(s=>m.sessions.find(r=>r.appSessionId===s.appSessionId)?.state==='Live'), 'all requested sessions live',45000);
  }
  async function sample(label,targets=parkedRows,seconds=6) {
    await ready(targets);
    const before=await caller('monitor'), beforeStats=await stats(), start=performance.now();
    const samples=new Map(targets.map(s=>[s.appSessionId,new Map()]));
    while(performance.now()-start<seconds*1000) {
      for(const frame of await caller('monitor-frames')) if(samples.has(frame.appSessionId)) samples.get(frame.appSessionId).set(frame.sequence,frame);
      await delay(100);
    }
    const elapsed=(performance.now()-start)/1000, after=await caller('monitor'), afterStats=await stats();
    const perSession=[];
    for(const s of targets) {
      const frames=[...samples.get(s.appSessionId).values()], unique=new Set(frames.map(f=>f.centerHash)).size;
      const current=after.sessions.find(r=>r.appSessionId===s.appSessionId), previous=before.sessions.find(r=>r.appSessionId===s.appSessionId), g=await geometry(s);
      assert(frames.length>=3&&unique>=2,label+' freshness: '+JSON.stringify({current,sampled:frames.length,unique}));
      assert(outside(g.current)); assert.equal(g.state,'Parked');
      assert.equal(error(g.normal,s.normal),0);
      for(const f of frames) {
        assert.equal(f.windowId,s.windowId); assert.deepEqual(f.identity,g.identity);
        const low=f.centerPixel.filter((_,i)=>!s.channels[i]),high=f.centerPixel.filter((_,i)=>s.channels[i]);
        assert(Math.min(...high)>Math.max(...low)+15,label+' cross-session fixture pixels: '+JSON.stringify(f));
      }
      perSession.push({appSessionId:s.appSessionId,windowId:s.windowId,identity:g.identity,
        effectiveFps:(current.frames-previous.frames)/elapsed,frames:current.frames-previous.frames,distinctHashes:unique,
        maxFrameGapSeconds:Math.max(...frames.slice(1).map((f,i)=>(Date.parse(f.receivedAt)-Date.parse(frames[i].receivedAt))/1000)),
        meanCaptureMilliseconds:frames.reduce((n,f)=>n+f.captureMilliseconds,0)/frames.length,
        dimensions:[frames.at(-1).width,frames.at(-1).height],normal:g.normal,current:g.current});
    }
    const active=after.sessions.find(s=>s.appSessionId===a.appSessionId);
    assert.equal(active.frames,0); assert.equal(active.bytes,0); assert.equal(active.state,'ACTIVE'); assert(!active.capturing);
    assert(!(await attached()).includes(a.tabId));
    const metrics={elapsedSeconds:elapsed,aggregateBase64BytesPerSecond:(after.bytes-before.bytes)/elapsed,
      callerOneCoreCpuPercent:(afterStats.callerCpuSeconds-beforeStats.callerCpuSeconds)/elapsed*100,
      chromeOneCoreCpuPercent:(afterStats.chromeCpuSeconds-beforeStats.chromeCpuSeconds)/elapsed*100,
      callerWorkingMiB:afterStats.callerWorkingBytes/1048576,callerPrivateMiB:afterStats.callerPrivateBytes/1048576,
      chromeWorkingMiB:afterStats.chromeWorkingBytes/1048576,chromePrivateMiB:afterStats.chromePrivateBytes/1048576,perSession};
    evidence(label,{result:'PASS',metrics,active});
    return metrics;
  }
  await sample('multi-five-session-intended-use');
  evidence('multi-initial-identities',{windows:initialGeometry,topology});
  const oldWorker = await cdp.worker();
  const blank = (await cdp.call('Target.getTargets')).targetInfos.find(t => t.type === 'page' && t.url === 'about:blank');
  const workerControl = await cdp.call('Target.attachToTarget', { targetId: blank.targetId, flatten: true });
  await cdp.call('ServiceWorker.enable', {}, workerControl.sessionId);
  await cdp.call('ServiceWorker.stopAllWorkers', {}, workerControl.sessionId);
  await until(async () => (await cdp.call('Target.getTargets')).targetInfos.some(t => t.targetId === oldWorker.targetId), v => !v, 'four-target worker stops');
  await cdp.call('Target.detachFromTarget', { sessionId: workerControl.sessionId });
  assert.notEqual((await cdp.worker()).targetId, oldWorker.targetId);
  await sample('multi-worker-recovery-four-eligible-only');
  const nav=pageB.url+'/dynamic-c-navigated';
  const unaffectedBefore=await caller('monitor');
  await cdp.extension('chrome.tabs.update('+c.tabId+',{url:'+JSON.stringify(nav)+'})');
  await until(()=>cdp.extension('chrome.tabs.get('+c.tabId+').then(t=>({status:t.status,url:t.url,windowId:t.windowId}))'),t=>t.status==='complete'&&t.url===nav&&t.windowId===c.windowId,'one PARKED navigation');
  await sample('multi-navigation-isolation');
  for(const s of parkedRows.filter(s=>s!==c)) assert.equal((await monitor(s)).generation,unaffectedBefore.sessions.find(r=>r.appSessionId===s.appSessionId).generation);
  const restoreStarted=performance.now();
  const restored=await caller('restore',{id:b.appSessionId});
  assert.equal(error(restored.current,b.normal),0);
  await until(()=>attached(),ids=>!ids.includes(b.tabId),'RESTORE detaches B');
  const restoreAndDetachMilliseconds=performance.now()-restoreStarted;
  const stoppedB=await monitor(b);
  assert.equal(stoppedB.state,'ACTIVE'); assert.equal(await caller('monitor-frame',{id:b.appSessionId}),null);
  await sample('multi-three-continue-after-restore',parkedRows.slice(1),4);
  assert.equal((await monitor(b)).frames,stoppedB.frames);
  await caller('park',{id:b.appSessionId});
  await sample('multi-repark-same-session',parkedRows,4);
  evidence('multi-restore-error',{maximumPixels:error(restored.current,b.normal),restoreAndDetachMilliseconds,restored});
  const aBefore=await geometry(a), bBefore=await geometry(b);
  const left=topology.filter(m=>m.bounds.left<0).sort((x,y)=>x.bounds.left-y.bounds.left)[0];
  const manual={left:(left?.workArea.left??0)+100,top:(left?.workArea.top??0)+80,width:1100,height:720};
  const aSet=await caller('set-bounds',{id:a.appSessionId,rect:manual});
  assert.equal(error(aSet.current,manual),0); assert.deepEqual(aSet.identity,aBefore.identity);
  assert.deepEqual((await geometry(b)).current,bBefore.current);
  await until(()=>caller('profile',a.launchUrl),p=>p&&error(p.normal,manual)===0,'manual observer persists');
  let rejected=false; try {await caller('set-bounds',{id:b.appSessionId,rect:manual});}catch {rejected=true;} assert(rejected);
  evidence('multi-manual-physical-bounds',{result:'PASS',before:aBefore,after:await geometry(a),actualNegativeMonitor:!!left,unaffectedB:await geometry(b)});
  const matrix=[];
  for(const [strategy,maxWidth,maxHeight] of [['normal',320,180],['small',320,180],['normal',240,135],['small',240,135],['normal',240,135]]) {
    for(const s of parkedRows) {
      const size=strategy==='small'?{width:512,height:320}:{width:s.normal.width,height:s.normal.height};
      await caller('park-size-test',{id:s.appSessionId,...size});
      assert(outside((await geometry(s)).current)); assert.equal(error((await caller('profile',s.launchUrl)).normal,s.normal),0);
    }
    await caller('monitor-start',{options:{framesPerSecond:2,maxWidth,maxHeight}});
    await delay(1000);
    matrix.push({strategy,maxWidth,maxHeight,fps:2,...await sample('multi-comparison-'+strategy+'-'+maxWidth)});
  }
  evidence('multi-performance-matrix',{matrix,cpuBasis:'percent of one core; caller includes test-only frame hash sampling; Base64 excludes JSON/WebSocket headers'});
  // Active tab can change only inside the established window.
  const extra=await cdp.extension('chrome.tabs.create({windowId:'+e.windowId+',url:'+JSON.stringify(pageB.url+'/dynamic-e-tab')+',active:true}).then(t=>({id:t.id,windowId:t.windowId}))');
  const originalE=e.tabId; e.tabId=extra.id;
  await until(()=>caller('monitor-frame',{id:e.appSessionId}),f=>f?.tabId===extra.id,'active tab within E is reacquired');
  await sample('multi-owned-active-tab-change',parkedRows,4);
  await cdp.extension('chrome.tabs.remove('+extra.id+')'); e.tabId=originalE;
  await until(()=>caller('monitor-frame',{id:e.appSessionId}),f=>f?.tabId===originalE,'closing active tab returns to E original');
  // Selected/monitored close and stale consumer Set cannot redirect another window.
  await cdp.extension('chrome.windows.remove('+e.windowId+')');
  await until(()=>monitor(e),s=>s.state==='Unavailable'&&!s.capturing,'closed monitor clears');
  rejected=false;try{await caller('set-bounds',{id:e.appSessionId,rect:manual});}catch{rejected=true;}assert(rejected);
  await sample('multi-close-isolation',parkedRows.slice(0,3),4);
  // Normal-size or experimentally shrunken PARK never becomes the relaunch profile.
  const eRelaunch=await caller('launch',e.launchUrl);
  await until(()=>geometry(eRelaunch),g=>g?.state==='Visible','closed E profile relaunch');
  assert.equal(error((await geometry(eRelaunch)).current,e.normal),0);
  // Shrink one live PARKED target for final shutdown, verifying original Normal restoration.
  await caller('park-size-test',{id:b.appSessionId,width:512,height:320});
  const shutdown=await caller('shutdown-host');
  assert.equal(shutdown.connections,0);assert.equal(shutdown.capturingConnections,0);
  await until(()=>attached(),ids=>ids.length===0,'all debugger targets detach before browser cleanup');
  const finalGeometry=await caller('shutdown-geometry');
  for(const s of parkedRows.slice(0,3)) assert.equal(error(finalGeometry.find(g=>g.appSessionId===s.appSessionId).current,s.normal),0);
  evidence('multi-clean-shutdown',{result:'PASS',shutdown,finalGeometry,restoreErrors:parkedRows.slice(0,3).map(s=>({id:s.appSessionId,pixels:error(finalGeometry.find(g=>g.appSessionId===s.appSessionId).current,s.normal)}))});
  console.log('PASS: multi-session five sessions, four PARKED fresh isolated captures, ACTIVE zero capture, manual native bounds, comparisons, lifecycle and exact shutdown restore.');
}
