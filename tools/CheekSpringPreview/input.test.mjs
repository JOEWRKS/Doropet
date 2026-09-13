import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
import * as motion from './motion.mjs';
async function harness(){
  let time=0,frame;
  const elements=new Map();
  const draws=[];
  const translations=[];
  const context={clearRect(){},save(){},restore(){},translate(...args){translations.push(args);},scale(){},drawImage(...args){draws.push(args);}};
  const canvas={style:{},getContext:()=>context,getBoundingClientRect:()=>({width:1024}),setPointerCapture(){}};
  elements.set('canvas',canvas);
  const document={querySelector(key){if(!elements.has(key))elements.set(key,{});return elements.get(key);},body:{classList:{toggle(){}}}};
  const scope={document,Image:class{width=160;height=19360;set src(value){this.onload();}},performance:{now:()=>time},requestAnimationFrame:fn=>frame=fn,
    setTimeout,clearTimeout,...motion};
  const script=(await readFile(new URL('./preview.mjs',import.meta.url),'utf8')).replace(/^import[^\n]*\n/,'');
  await vm.runInNewContext(`(async()=>{${script}\n})()`,scope);
  return {elements,canvas,draws,translations,paint(ms){time=ms;draws.length=0;translations.length=0;frame(ms);},setTime(ms){time=ms;}};
}
test('padded pose banks retain full frame and native scale at maximum pull',async()=>{
  const h=await harness();h.elements.get('#hold').onclick();h.paint(0);
  for(const args of h.draws)assert.deepEqual(args.slice(1),[0,19200,160,160,-80,-80,160,160]);
});
test('regrabbing a returning cheek preserves the displayed stretch instead of restoring full pull',async()=>{
  const h=await harness();h.elements.get('#hold').onclick();h.elements.get('#release').onclick();
  h.paint(60);h.canvas.onpointerdown({clientX:100,pointerId:1});h.paint(61);
  const label=h.elements.get('output').textContent;
  assert.match(label,/당김 3\d%/);
});
test('pointer release and capture loss start only one spring and eventually restore zero',async()=>{
  const h=await harness();h.canvas.onpointerdown({clientX:100,pointerId:1});
  h.canvas.onpointermove({clientX:60});h.canvas.onpointerup();h.setTime(100);h.canvas.onlostpointercapture();h.paint(2000);
  assert.equal(Number(h.elements.get('input').value),0);
});

test('a maximum release adds a subtle backward bounce only to the reaction preview',async()=>{
  const h=await harness();h.elements.get('#hold').onclick();h.paint(0);
  assert.equal(h.translations[2][0],768,'holding has no backward kick');
  h.elements.get('#release').onclick();h.paint(0);
  assert.equal(h.translations[2][0],768,'release starts continuously');
  h.paint(150);
  assert.equal(h.translations[0][0],256,'baseline stays in place');
  assert.ok(h.translations[2][0]>=770&&h.translations[2][0]<=772,'backward bounce is 1–2 native pixels');
  assert.equal(h.translations[2][0]-768,2*(h.translations[3][0]-768),'both scales show the same native motion');
  h.paint(440);assert.equal(h.translations[2][0],768,'settling restores exact position');
});

test('light pulls retain signed recoil with a readable small response, below the maximum bounce',async()=>{
  const h=await harness(),slider=h.elements.get('input');slider.value=5;slider.oninput();
  h.elements.get('#release').onclick();h.paint(160);
  assert.ok(h.draws[2][2]<6400,'light pull crosses into the negative recoil poses');
  assert.ok(h.translations[2][0]>769&&h.translations[2][0]<770,'light release adds about half a native pixel');
});

test('release displacement is continuous at start and settle, with no kick at zero input',async()=>{
  const h=await harness();h.elements.get('#release').onclick();h.paint(150);assert.equal(h.translations[2][0],768);
  h.elements.get('#hold').onclick();h.elements.get('#release').onclick();
  h.paint(150.01);assert.ok(Math.abs(h.translations[2][0]-768)<.001);
  h.paint(589.99);assert.ok(Math.abs(h.translations[2][0]-768)<.001);
  h.paint(590);assert.equal(h.translations[2][0],768);
});

test('maximum backward bounce mirrors and regrabs without a position jump',async()=>{
  const h=await harness();h.elements.get('#flip').onclick();h.elements.get('#hold').onclick();h.elements.get('#release').onclick();h.paint(150);
  const x=h.translations[2][0],pose=h.draws[2][2];assert.ok(x<768);
  h.canvas.onpointerdown({clientX:100,pointerId:1});h.paint(150);
  assert.equal(h.translations[2][0],x);assert.equal(h.draws[2][2],pose);
  h.canvas.onpointermove({clientX:100});h.paint(250);assert.equal(h.translations[2][0],x);
  h.canvas.onpointerup();h.paint(250);assert.equal(h.translations[2][0],x);
  h.paint(690);assert.equal(h.translations[2][0],768);
});

test('repeated regrab and maximum release cannot accumulate a large displacement',async()=>{
  const h=await harness();h.elements.get('#hold').onclick();h.elements.get('#release').onclick();
  for(let i=1;i<=5;i++){
    h.paint(i*150);assert.ok(h.translations[2][0]<=772,'repeated kicks remain within two native pixels');
    h.canvas.onpointerdown({clientX:100,pointerId:1});h.canvas.onpointermove({clientX:0});h.canvas.onpointerup();
  }
});
