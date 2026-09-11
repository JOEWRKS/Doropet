const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const dependencies=path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules');
const {chromium}=require(path.join(dependencies,'playwright'));
const canvas=require(path.join(dependencies,'@napi-rs/canvas'));
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:850,height:800}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:2800/');await page.waitForFunction(()=>document.querySelector('#label').textContent.includes('집중'));
  const out=path.resolve(__dirname,'../../artifacts/repro/pounce-preview-20260911');fs.mkdirSync(out,{recursive:true});
  const sheet=canvas.createCanvas(1200,600),c=sheet.getContext('2d');
  for(let row=0;row<2;row++)for(const [col,time] of [1.45,1.60,1.75,1.90,2.15].entries()){
   await page.locator('#black').setChecked(!!row);
   await page.locator('#timeline').fill(String(time));await page.locator('#timeline').dispatchEvent('input');
   const png=await page.locator('#stage').screenshot();
   const im=await canvas.loadImage(png);
   c.fillStyle=row?'#101014':'#fcfaf8';c.fillRect(col*240,row*300,240,300);
   c.drawImage(im,im.width/2-140,70,280,205,col*240,row*300+30,240,176);
   c.fillStyle=row?'#eee':'#333';c.font='15px sans-serif';c.fillText(time+'s',col*240+12,row*300+24);
  }
  fs.writeFileSync(path.join(out,'contact-sheet.png'),sheet.toBuffer('image/png'));
  await page.locator('#black').setChecked(false);await page.locator('#mouse').click();
  const bounds=await page.locator('#stage').boundingBox();
  await page.mouse.move(bounds.x+bounds.width/2-100,bounds.y+172);
  await page.waitForTimeout(700);assert.match(await page.locator('#status').innerText(),/^0회/);
  await page.mouse.move(5,5);await page.waitForTimeout(100);
  await page.mouse.move(bounds.x+bounds.width/2-100,bounds.y+172);
  await page.waitForTimeout(900);assert.match(await page.locator('#status').innerText(),/^0회/);
  await page.waitForFunction(()=>document.querySelector('#status').textContent.startsWith('1회'));
  await page.waitForTimeout(2200);assert.match(await page.locator('#status').innerText(),/^1회/);
  await page.mouse.move(5,5);await page.waitForTimeout(500);
  await page.mouse.move(bounds.x+bounds.width/2+60,bounds.y+172);
  await page.waitForFunction(()=>document.querySelector('#status').textContent.startsWith('2회'));
  assert.deepEqual(errors,[]);
  await page.locator('#auto').click();await page.screenshot({path:path.join(out,'page.png')});
  console.log('PASS browser: images decoded, scrubbed flight/landing, dwell reset, one-shot hold, rearm, no page errors');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
