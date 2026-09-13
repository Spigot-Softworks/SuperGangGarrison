import {chromium} from 'playwright';
import {mkdir,writeFile} from 'node:fs/promises';
import {join,resolve,relative,extname,isAbsolute} from 'node:path';
import {setTimeout as delay} from 'node:timers/promises';
import assert from 'node:assert/strict';

const output=resolve(process.env.OG_BROWSER_QA_DIR??'artifacts/pyro-hold');
const umbrella=process.env.OG_UMBRELLA==='1';
const ltd=process.env.OG_LTD==='1';
const mouseButton=umbrella?'right':'left';
await mkdir(output,{recursive:true});
async function serveLocalCandidate(context){
  if(!process.env.OG_BROWSER_LOCAL_ROOT)return;
  // Serve the candidate to these test contexts only. Keeping the normal site
  // origin exercises the actual room API/WebRTC path with its production CORS
  // rules, without uploading candidate files to the public website.
  const root=resolve(process.env.OG_BROWSER_LOCAL_ROOT);
  const origin=new URL(process.env.OG_BROWSER_URL??'https://superganggarrison.com/').origin;
  const types={'.html':'text/html','.css':'text/css','.js':'text/javascript','.json':'application/json','.wasm':'application/wasm','.png':'image/png','.wav':'audio/wav','.ogg':'audio/ogg'};
  await context.route(origin+'/**',async route=>{
    const pathname=new URL(route.request().url()).pathname;
    const requestRoot=process.env.OG_BROWSER_FRAMEWORK_ROOT&&pathname.startsWith('/_framework/')?resolve(process.env.OG_BROWSER_FRAMEWORK_ROOT):root;
    const path=resolve(requestRoot,'.'+decodeURIComponent(pathname==='/'?'/index.html':pathname));
    const within=relative(requestRoot,path);
    if(within.startsWith('..')||isAbsolute(within)){await route.abort();return;}
    try{await route.fulfill({path,contentType:types[extname(path)]??'application/octet-stream',headers:{'Cache-Control':'no-store'}});}
    catch(error){if(error.code==='ENOENT')await route.fulfill({status:404,body:'Not found'});else throw error;}
  });
}
const browser=await chromium.launch({headless:true,args:['--enable-gpu','--use-angle=d3d11','--disable-background-timer-throttling','--disable-renderer-backgrounding']});
const context=await browser.newContext({viewport:{width:1280,height:720}});
await serveLocalCandidate(context);
await context.addInitScript(()=>{
  localStorage.setItem('opengarrison:first-play-hints-v1',JSON.stringify({HasShown:true}));
  localStorage.setItem('opengarrison:settings-v1',JSON.stringify({ParticleMode:0}));
});
const page=await context.newPage(),samples=[],errors=[];
let release,beforeHold,releaseSample,secondHoldSample;
const secondHoldSamples=[];
const hostFireSamples=[];
let passed=false;
let owner;
page.on('pageerror',e=>errors.push(e.message));
const state=()=>page.evaluate(()=>OpenGarrisonBrowserHost.getAutomationState());
async function wait(fn,label){console.log('Waiting: '+label);for(let end=Date.now()+180000;Date.now()<end;){const s=await state();await writeFile(join(output,'guest-state.json'),JSON.stringify({label,s},null,2));if(s&&fn(s))return s;await delay(200);}throw Error(label);}
async function action(g,l){console.log('Guest action: '+g+'/'+l);if(!await page.evaluate(([g,l])=>OpenGarrisonBrowserHost.invokeAutomationAction(g,l),[g,l]))throw Error(l);}
async function selectPracticeMap(target){
  const requested=process.env.OG_PRACTICE_MAP;
  if(!requested)return;
  const names=requested==='gg2_koth_harvest'?[requested,'Harvest']:[requested];
  for(const name of names)if(await target.evaluate(name=>OpenGarrisonBrowserHost.setAutomationValue('practice_map',name),name))return;
  throw Error('Practice map unavailable: '+requested);
}
try{
  await page.goto(process.env.OG_BROWSER_URL??'https://superganggarrison.com/',{waitUntil:'domcontentloaded',timeout:120000});
  release=await page.evaluate(async()=>await(await fetch('release.json',{cache:'no-store'})).json());
  if(process.env.OG_BROWSER_FRAMEWORK_ROOT)release={...release,aot:false,diagnosticFrameworkRoot:process.env.OG_BROWSER_FRAMEWORK_ROOT};
  await wait(s=>s.mainMenuOpen&&!s.startupSplashOpen&&s.canEnterGameplaySession,'menu');
  await page.screenshot({path:join(output,'main-menu.png')});
  await page.evaluate(()=>OpenGarrisonBrowserHost.focusCanvas());
  if(ltd){
    await action('menu','Last to Die');await action('ltd','Play Solo');await action('ltd','Standard');
    await wait(s=>s.lastToDie?.phase==='SurvivorChoice','LTD survivor choice');
    assert(await page.evaluate(()=>OpenGarrisonBrowserHost.setAutomationValue('ltd_survivor','ltd.survivor.soldier')));
    const reward=await wait(s=>s.lastToDie?.offerChoices?.length,'LTD starting reward');
    assert(await page.evaluate(id=>OpenGarrisonBrowserHost.setAutomationValue('ltd_reward',id),reward.lastToDie.offerChoices[0]));
    await wait(s=>s.lastToDie?.phase==='Playing'&&s.localPlayerAlive,'LTD playing');
  }else{
  await action('menu','Practice');
  if(process.env.OG_PYRO_COOP!=='1')await selectPracticeMap(page);
  if(process.env.OG_PYRO_COOP==='1'){
    console.log('Starting two-player co-op');
    const hostContext=await browser.newContext({viewport:{width:1280,height:720}});
    await serveLocalCandidate(hostContext);
    await hostContext.addInitScript(()=>localStorage.setItem('opengarrison:first-play-hints-v1',JSON.stringify({HasShown:true})));
    owner=await hostContext.newPage();
    await owner.goto(process.env.OG_BROWSER_URL??'https://superganggarrison.com/',{waitUntil:'domcontentloaded',timeout:120000});
    async function hostWait(fn,label){console.log('Waiting: '+label);for(let end=Date.now()+180000;Date.now()<end;){const s=await owner.evaluate(()=>OpenGarrisonBrowserHost.getAutomationState());await writeFile(join(output,'host-state.json'),JSON.stringify({label,s},null,2));if(s&&fn(s))return s;await delay(200);}throw Error(label);}
    async function hostAction(g,l){console.log('Host action: '+g+'/'+l);if(!await owner.evaluate(([g,l])=>OpenGarrisonBrowserHost.invokeAutomationAction(g,l),[g,l]))throw Error(l);}
    await hostWait(s=>s.mainMenuOpen&&!s.startupSplashOpen&&s.canEnterGameplaySession,'host menu');
    await hostAction('menu','Practice');
    await selectPracticeMap(owner);
    for(const k of ['practice_enemy_bots','practice_friendly_bots'])await owner.evaluate(k=>OpenGarrisonBrowserHost.setAutomationValue(k,'0'),k);
    await hostAction('practice','Co-Op Lobby');
    const room=await hostWait(s=>s.roomPhase==='Lobby','host room');
    await action('practice','Join Co-Op');
    await page.evaluate(code=>OpenGarrisonBrowserHost.setAutomationValue('ltd_code',code),room.lastToDie.roomCode);
    await action('ltd','Join');
    await wait(s=>s.roomPhase==='Lobby','guest room');
    await hostWait(s=>s.roomRoster.length===2,'two players');
    await hostAction('ltd','Ready Up');await action('ltd','Ready Up');await delay(500);
    await hostAction('ltd','Start');
    const hostJoin=await hostWait(s=>s.teamSelectOpen||s.classSelectOpen,'host join');
    if(hostJoin.teamSelectOpen)await hostAction('teamselect','RED');
    await hostWait(s=>s.classSelectOpen,'host class');await hostAction('classselect','Scout');
    console.log('Host playing, guest joining');
  }else await page.evaluate(()=>OpenGarrisonBrowserHost.startAutomationPractice(0,0));
  const guestJoin=await wait(s=>s.teamSelectOpen||s.classSelectOpen,'join');
  if(guestJoin.teamSelectOpen)await action('teamselect','RED');
  await wait(s=>s.classSelectOpen,'class');
  if(umbrella){
    if(!await page.evaluate(()=>OpenGarrisonBrowserHost.invokeAutomationAction('classselect','Civilian'))){
      // Older builds lack the Civilian automation selector. Solo reproduction
      // can still use the existing game console after selecting another class.
      assert.notEqual(process.env.OG_PYRO_COOP,'1','Old co-op build lacks Civilian selector');
      await action('classselect','Pyro');
      await page.evaluate(()=>OpenGarrisonBrowserHost.runAutomationConsoleCommand('set_class civilian'));
    }
  }else await action('classselect','Pyro');
  }
  await wait(s=>s.localPlayerAlive,'spawn');await delay(2000);
  beforeHold=await state();
  if(process.env.OG_PRACTICE_MAP&&!ltd)assert.equal(beforeHold.currentMap,process.env.OG_PRACTICE_MAP);
  await page.mouse.move(820,200);
  await page.screenshot({path:join(output,'before.png')});
  await page.mouse.down({button:mouseButton});
  const pressedAt=Date.now();
  for(let i=0;i<(ltd?12:32);i++){
    await delay(umbrella&&i<6?25:250);const s=await state();
    if(owner)hostFireSamples.push({phase:'first',i,state:await owner.evaluate(()=>OpenGarrisonBrowserHost.getAutomationState())});
    samples.push({worldFlameCount:s.worldFlameCount,protocolFlameCount:s.protocolFlameCount,sample:i,elapsedMs:Date.now()-pressedAt,flameSmokeCount:s.flameSmokeCount,rocketSmokeCount:s.rocketSmokeCount,primaryAmmo:s.primaryAmmo,equippedItemId:s.equippedItemId,focused:s.browserInputFocused,alive:s.localPlayerAlive,x:s.localPlayerX,y:s.localPlayerY,overlay:s.gameplayOverlay,umbrellaActive:s.umbrellaActive,umbrellaOpeningTicks:s.umbrellaOpeningTicks,weaponAnimation:s.weaponAnimation,weaponSprite:s.weaponSprite});
    if([1,3,7,15,23,31].includes(i)||(umbrella&&i<6))await page.screenshot({path:join(output,`hold-${i}.png`)});
  }
  await page.mouse.up({button:mouseButton});await delay(2000);
  releaseSample=await state();
  await page.screenshot({path:join(output,'released.png')});
  await page.mouse.down({button:mouseButton});
  for(let i=0;i<10;i++){
    await delay(200);secondHoldSample=await state();
    if(owner)hostFireSamples.push({phase:'second',i,state:await owner.evaluate(()=>OpenGarrisonBrowserHost.getAutomationState())});
    secondHoldSamples.push({worldFlameCount:secondHoldSample.worldFlameCount,protocolFlameCount:secondHoldSample.protocolFlameCount,flameSmokeCount:secondHoldSample.flameSmokeCount,primaryAmmo:secondHoldSample.primaryAmmo,rocketSmokeCount:secondHoldSample.rocketSmokeCount,equippedItemId:secondHoldSample.equippedItemId,alive:secondHoldSample.localPlayerAlive});
  }
  await page.screenshot({path:join(output,'second-hold.png')});await page.mouse.up({button:mouseButton});
  if(process.env.OG_LTD_EXPECT_SUSTAINED==='1'){
    const early=samples.slice(0,8);
    assert(beforeHold.primaryAmmo-Math.min(...early.map(s=>s.primaryAmmo))>=2,'LTD Soldier did not fire repeatedly while M1 was held');
    assert(early.every(s=>s.alive&&s.equippedItemId===beforeHold.equippedItemId),'LTD weapon selection changed during held fire');
    // A rocket can hit the nearby wall and its smoke can expire before the
    // end of this hold. Observe the shot over time, not at one final instant.
    assert(secondHoldSamples.some(s=>s.rocketSmokeCount>0)&&secondHoldSamples.some(s=>s.primaryAmmo<releaseSample.primaryAmmo),'LTD Soldier did not resume firing after re-press');
    assert(secondHoldSamples.every(s=>s.alive&&s.equippedItemId===beforeHold.equippedItemId),'LTD weapon selection changed after re-press');
    assert.deepEqual(errors,[]);
  }
  if(process.env.OG_PYRO_EXPECT_SUSTAINED==='1'){
    // A full fuel tank lasts several seconds; its initial puff is not proof of
    // continuous fire. Check every sample across the first two seconds, then a
    // real release/re-press after the first hold has exhausted the tank.
    assert(samples.slice(1,8).every(s=>s.alive&&s.focused&&s.flameSmokeCount>0),'Flames stopped during the first two seconds of held M1');
    assert.equal(releaseSample.flameSmokeCount,0,'Flames continued after releasing M1');
    assert(secondHoldSample.flameSmokeCount>0,'Flames did not resume after releasing and pressing M1 again');
    assert.deepEqual(errors,[]);
  }
  if(umbrella&&process.env.OG_UMBRELLA_EXPECT_OPEN==='1'){
    assert(samples.some(s=>s.weaponAnimation==='CivvieUmbrellaOpening'),'Opening animation never appeared');
    assert(samples.slice(8).every(s=>s.umbrellaActive&&s.weaponAnimation==='CivvieUmbrellaHold'&&s.weaponSprite==='CivvieUmbrellaOpenAnimS'),'Open umbrella did not remain visible while holding M2');
    assert.equal(releaseSample.umbrellaActive,false);
    assert(secondHoldSample.umbrellaActive&&secondHoldSample.weaponAnimation==='CivvieUmbrellaHold','Umbrella failed to reopen');
    assert.deepEqual(errors,[]);
  }
  passed=true;
  console.log(JSON.stringify({samples,errors}));
}finally{
  await writeFile(join(output,'result.json'),JSON.stringify({passed,release,beforeHold,samples,errors,releaseSample,secondHoldSample,secondHoldSamples,hostFireSamples},null,2));
  for(const p of [page,owner].filter(Boolean)){
    await p.evaluate(()=>OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect')).catch(()=>{});
    await p.evaluate(()=>OpenGarrisonBrowserHost.invokeAutomationAction('ltd','Leave Room')).catch(()=>{});
  }
  await browser.close();
}
