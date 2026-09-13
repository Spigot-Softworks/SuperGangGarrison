import assert from 'node:assert/strict';
import {mkdir,writeFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {setTimeout as delay} from 'node:timers/promises';
import {chromium} from 'playwright';

const base=process.env.OG_BROWSER_URL??'http://127.0.0.1:5077';
const output=resolve(process.env.OG_BROWSER_QA_DIR??'artifacts/browser-hud');
await mkdir(output,{recursive:true});
const debugPort=process.env.OG_BROWSER_DEBUG_PORT;
const browser=await chromium.launch({headless:true, args:debugPort?['--remote-debugging-port='+debugPort]:[]});
const context=await browser.newContext({viewport:{width:1280,height:720}});
await context.addInitScript(()=>localStorage.setItem('opengarrison:first-play-hints-v1',JSON.stringify({HasShown:true})));
const page=await context.newPage();const errors=[],checks=[];
page.on('pageerror',error=>{errors.push(error.message);console.log('PAGE ERROR: '+error.message);});
page.on('console',message=>{console.log('BROWSER '+message.type()+': '+message.text());if(message.type()==='error')errors.push(message.text());});
async function state(){return page.evaluate(()=>OpenGarrisonBrowserHost.getAutomationState());}
async function wait(predicate,label){let s;for(let end=Date.now()+180000;Date.now()<end;){s=await state();if(s&&predicate(s)){console.log('PASS: '+label);return s;}await delay(200);}throw Error(label+': '+JSON.stringify(s));}
async function action(group,label){console.log('Action: '+group+' / '+label);assert(await page.evaluate(([g,l])=>OpenGarrisonBrowserHost.invokeAutomationAction(g,l),[group,label]),label);}
async function command(text){assert(await page.evaluate(t=>OpenGarrisonBrowserHost.runAutomationConsoleCommand(t),text),text);await delay(300);}
async function press(code){await page.evaluate(c=>OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync('HandleBrowserKey',c,true),code);await delay(150);await page.evaluate(c=>OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync('HandleBrowserKey',c,false),code);await delay(150);}
function checkWeapons(s){
 const abilities=Object.entries(s.hudBounds).filter(([key])=>key.startsWith('local.ability.slot.')).map(([,rect])=>rect);
 const weapons=Object.entries(s.hudBounds).filter(([key])=>key.startsWith('local.weapon.')&&key!=='local.weapon.prompt').map(([,rect])=>rect);
 assert(abilities.length>0);assert(weapons.length>0);
 for(const a of abilities)for(const w of weapons){assert(a.y+a.height<=w.y-8,JSON.stringify({a,w}));}
 checks.push({viewport:[s.viewportWidth,s.viewportHeight],abilities,weapons});
}
let passed=false;
try{
 await page.goto(base,{waitUntil:'domcontentloaded',timeout:120000});
 await wait(s=>!s.startupSplashOpen&&s.mainMenuOpen&&s.canEnterGameplaySession,'main menu');
 await page.evaluate(()=>OpenGarrisonBrowserHost.focusCanvas());
 await action('menu','Practice');assert(await page.evaluate(()=>OpenGarrisonBrowserHost.startAutomationPractice(0,0)));
 await wait(s=>s.teamSelectOpen,'team select');await action('teamselect','RED');await wait(s=>s.classSelectOpen,'class select');await action('classselect','Soldier');
 await wait(s=>s.localPlayerAlive&&s.practiceSessionActive,'Soldier practice spawn');
 assert.equal((await state()).customBubbleBinding,'Keyboard:None');await press('KeyR');assert.equal((await state()).bubbleMenu,'None');
 for(const size of [{width:1280,height:720},{width:960,height:540},{width:1536,height:864}]){
  await page.setViewportSize(size);await delay(500);checkWeapons(await state());await page.screenshot({path:join(output,'soldier-'+size.width+'.png')});
 }
 await press('KeyQ');checkWeapons(await state());await press('KeyQ');checkWeapons(await state());
 for(const kind of ['heavy','demoman','spy']){await command('set_class '+kind);await wait(s=>s.localPlayerAlive,kind+' spawned');checkWeapons(await state());}
 await command('disconnect');await wait(s=>s.mainMenuOpen,'back to menu');
 await action('menu','Last to Die');await action('ltd','Play Solo');await action('ltd','Standard');
 await wait(s=>s.lastToDie?.phase==='SurvivorChoice','LTD survivor selection');
 assert(await page.evaluate(()=>OpenGarrisonBrowserHost.setAutomationValue('ltd_survivor','ltd.survivor.soldier')));
 const choice=await wait(s=>s.lastToDie?.offerChoices?.length>0,'LTD starting reward');
 assert(await page.evaluate(id=>OpenGarrisonBrowserHost.setAutomationValue('ltd_reward',id),choice.lastToDie.offerChoices[0]));
 const ltd=await wait(s=>s.localPlayerAlive&&s.survivorBuffActive&&s.hudBounds['last-to-die.buff-icon'],'live LTD Survivor buff');
 checkWeapons(ltd);const buff=ltd.hudBounds['last-to-die.buff-icon'],hp=ltd.hudBounds['local.health'];
 assert(Math.abs(buff.y-hp.y)<45,JSON.stringify({buff,hp}));assert(buff.x>hp.x&&buff.x<hp.x+150,JSON.stringify({buff,hp}));
 await page.screenshot({path:join(output,'ltd-survivor.png')});checks.push({buff,hp});
 await command('disconnect');assert.deepEqual(errors,[]);passed=true;
}finally{
 await writeFile(join(output,'result.json'),JSON.stringify({passed,checks,errors},null,2));await browser.close();
}
