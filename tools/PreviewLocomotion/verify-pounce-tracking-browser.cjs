const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const {chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'));
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:850,height:800}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:2800/');await page.waitForFunction(()=>document.querySelector('#label').textContent.includes('집중'));
  const result=await page.evaluate(()=>{
   choose('mouse');paused=true;const center=$('stage').width/2;
   mouse=[center-105,170];for(let i=0;i<264;i++)update(1/120);
   const start={...pose,frameAge:age,flip};
   mouse=[center+180,120];for(let i=0;i<24;i++)update(1/120);
   const right={...pose,frameAge:age,flip,pixels:painted.toDataURL()};
   mouse=[center+180,235];for(let i=0;i<24;i++)update(1/120);
   const down={...pose,frameAge:age,flip,pixels:painted.toDataURL()};
   mouse=[center-105,170];
   while(time<5.10)update(1/120);
   const before={...pose,frameAge:age};
   while(time<5.20)update(1/120);
   const preparing={...pose,frameAge:age};
   while(time<6.70)update(1/120);
   const next={...pose};
   return {start,right,down,before,preparing,next};
  });
  for(const p of [result.start,result.right,result.down,result.before]){
   assert.equal(p.phase,'track');assert.equal(p.frameAge,2.6);assert.equal(p.x,-28);
   assert.equal(p.y,0);assert.equal(p.angle,0);assert.equal(p.scaleY,1);assert.equal(p.dwell,0);
  }
  assert.equal(result.start.flip,false);assert.equal(result.right.flip,true);
  assert.notEqual(result.right.pixels,result.down.pixels,'upright head/eye tracking must change visible image');
  assert.equal(result.preparing.phase,'watch');assert.ok(result.preparing.frameAge<.7);
  assert.equal(result.next.count,2);assert.equal(result.next.phase,'flight');
  const out=path.resolve(__dirname,'../../artifacts/repro/pounce-preview-20260911');fs.mkdirSync(out,{recursive:true});
  await page.locator('#timeline').fill('3.2');await page.locator('#timeline').dispatchEvent('input');
  await page.screenshot({path:path.join(out,'tracking-white.png')});
  await page.locator('#black').setChecked(true);
  await page.screenshot({path:path.join(out,'tracking-black.png')});
  assert.deepEqual(errors,[]);
  console.log('PASS browser: upright3s, fixed position, body turn, visible head/eye tracking, fresh dwell, repeated hop without leaving');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
