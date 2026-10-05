// Development only: use a distinct diagnostic browser profile, never the user's active window.
import assert from 'node:assert/strict';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {setTimeout as delay} from 'node:timers/promises';
import {promisify} from 'node:util';
import {execFile} from 'node:child_process';
import {connect,pageTarget,localSocket} from '../extension/cdp.mjs';
import {buildInjection} from '../extension/payload.mjs';
const root=dirname(dirname(fileURLToPath(import.meta.url))),port=Number(process.argv[2]),out=process.argv[3],gif=process.argv[4],profile=process.argv[5];
if(!Number.isInteger(port)||!out)throw Error('Usage: node tools/windows.smoke.mjs <diagnostic-port> <output-dir> [custom-gif] [diagnostic-profile]');
const target=(await(await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find(pageTarget);
if(!target)throw Error('No official main page');
const client=await connect(localSocket(target.webSocketDebuggerUrl,port)),checks=[];
const evaluate=async expression=>{const result=await client.call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(result.exceptionDetails)throw Error(JSON.stringify(result.exceptionDetails));return result.result?.value;};
const doc='document.querySelector("#aemeath-extension-overlay").contentDocument';
const wait=async(expression,limit=10000)=>{const deadline=Date.now()+limit;while(Date.now()<deadline){if(await evaluate(expression))return;await delay(100);}throw Error('Timed out: '+expression);};
const settings=async()=>{await evaluate('window.__aemeathExtension.openSettings()');await wait('document.querySelector("#aemeath-extension-overlay")?.contentDocument?.querySelector("#settings-dialog")?.open===true');};
const save=async()=>{await evaluate(`${doc}.querySelector('#preview-images').click()`);await wait('window.__aemeathExtension.status().overlay===false',18000);};
const settingsEntry=async()=>{
 const executable=join(process.env.SystemRoot,'System32/WindowsPowerShell/v1.0/powershell.exe');
 const env={...process.env,PSModulePath:join(process.env.SystemRoot,'System32/WindowsPowerShell/v1.0/Modules')};
 await promisify(execFile)(executable,['-NoProfile','-ExecutionPolicy','Bypass','-File',join(root,'.build/release/runtime/windows/launch.ps1'),'-ProfileDirectory',profile,'-OpenSettings','-NoDialog'],{windowsHide:true,timeout:70000,env});
 await wait('document.querySelector("#aemeath-extension-overlay")?.contentDocument?.querySelector("#settings-dialog")?.open===true');
};
const upload=async(kind,file,mime)=>{
 const bytes=await readFile(file);
 await evaluate(`(()=>{const w=document.querySelector('#aemeath-extension-overlay').contentWindow,b=atob(${JSON.stringify(bytes.toString('base64'))}),a=new Uint8Array(b.length);for(let i=0;i<b.length;i++)a[i]=b.charCodeAt(i);const transfer=new w.DataTransfer();transfer.items.add(new w.File([a],'custom-image',{type:${JSON.stringify(mime)}}));const input=w.document.querySelector('#${kind}-file');input.files=transfer.files;input.dispatchEvent(new w.Event('change',{bubbles:true}));})()`);
 await wait(`!${doc}.querySelector('#preview-images').disabled`);
 assert.match(await evaluate(`${doc}.querySelector('#settings-status').textContent`),/图片已准备好/);
};
try{
 await client.call('Emulation.setFocusEmulationEnabled',{enabled:true});await client.call('Page.bringToFront');
 await wait('document.querySelector("#aemeath-extension-overlay")?.contentDocument?.querySelector("#settings-dialog")?.open===true');
 assert.equal(await evaluate(`${doc}.querySelector('#intro-title').value`),'欢迎回来');
 checks.push('fresh settings entry opens with generic default text');
 await mkdir(out,{recursive:true});
 await writeFile(join(out,'settings.png'),Buffer.from((await client.call('Page.captureScreenshot',{format:'png'})).data,'base64'));
 await evaluate(`${doc}.querySelector('#preview-images').click()`);await delay(8300);
 await writeFile(join(out,'startup.png'),Buffer.from((await client.call('Page.captureScreenshot',{format:'png'})).data,'base64'));
 await wait('window.__aemeathExtension.status().overlay===false',10000);
 assert.equal((await evaluate('window.__aemeathExtension.status()')).wallpaper,true);
 checks.push('full animation completes and default wallpaper remains');
 if(profile){
  await settingsEntry();
  checks.push('settings launcher reuses verified running session');
 }else await settings();
 await upload('avatar',join(root,'assets/artwork.jpg'),'image/jpeg');
 await upload('artwork',join(root,'assets/avatar.jpg'),'image/jpeg');
 await evaluate(`(()=>{const d=${doc},i=d.querySelector('#intro-title');i.value='Windows 用户，欢迎回来';i.dispatchEvent(new Event('input',{bubbles:true}));})()`);
 await save();await settings();
 assert.equal(await evaluate(`${doc}.querySelector('#intro-title').value`),'Windows 用户，欢迎回来');
 assert.match(await evaluate(`${doc}.querySelector('#avatar-preview').src`),/^data:image\/png;base64,/);
 assert.match(await evaluate(`${doc}.querySelector('#artwork-preview').src`),/^data:image\/png;base64,/);
 checks.push('custom avatar, static artwork, contours and welcome text persist');
 if(gif){
  await upload('artwork',gif,'image/gif');await save();await settings();
  assert.match(await evaluate(`${doc}.querySelector('#artwork-preview').src`),/^data:image\/gif;base64,/);
  const mime=await evaluate(`(async()=>{const u=document.documentElement.style.getPropertyValue('--aemeath-wallpaper').match(/blob:[^"\\s)]+/)[0];return (await fetch(u)).headers.get('content-type');})()`);
  assert.equal(mime,'image/gif');checks.push('custom GIF persists and remains a GIF wallpaper');
 }
 await evaluate(`${doc}.querySelector('#reset-images').click();${doc}.querySelector('#preview-images').click()`);await delay(700);
 await client.call('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
 await client.call('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
 await wait('window.__aemeathExtension.status().overlay===false');checks.push('Escape skips the saved animation');
 await settings();assert.equal(await evaluate(`${doc}.querySelector('#intro-title').value`),'欢迎回来');checks.push('restore defaults resets custom text and image');
 await evaluate(`${doc}.querySelector('#restore-appearance').click()`);
 assert.equal(await evaluate('!!window.__aemeathExtension||document.documentElement.hasAttribute("data-aemeath-skin")'),false);
 checks.push('restore appearance removes the extension and wallpaper');
 if(profile){
  await evaluate(await buildInjection(root));await wait('window.__aemeathExtension?.status().ready===true');
  assert.equal((await evaluate('window.__aemeathExtension.status()')).phase,'playing');
  await settingsEntry();
  checks.push('settings launcher opens a dialog during an active startup animation');
  await evaluate('window.__aemeathExtension.dispose()');
 }
 await writeFile(join(out,'windows-verification.json'),JSON.stringify({date:'2026-10-05',profile:'diagnostic browser directory; account state may be shared',checks},null,2));
 checks.forEach(check=>console.log('PASS '+check));
}finally{await client.call('Emulation.setFocusEmulationEnabled',{enabled:false}).catch(()=>{});client.close();}
