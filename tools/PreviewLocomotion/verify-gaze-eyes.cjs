const assert=require('node:assert/strict'),fs=require('node:fs'),{create}=require('./four-leg-harness.cjs');
(async()=>{
 const {renderer,canvas}=await create(),make=(w,h)=>canvas.createCanvas(w,h);
 const painter=fs.existsSync(__dirname+'/gaze-eyes.js')?require('./gaze-eyes.js').create(renderer.original,make):{paint:()=>{const c=make(384,384);c.getContext('2d').drawImage(renderer.original,0,0,384,384);return c}};
 const data=(x,y)=>painter.paint(x,y).getContext('2d').getImageData(0,0,384,384).data;
 const neutral=data(0,0),right=data(.85,.5),left=data(-.85,-.5);
 let lashChange=0;
 for(let y=46*4;y<49*4;y++)for(let x=18*4;x<27*4;x++){
  const i=(y*384+x)*4;for(let c=0;c<3;c++)lashChange+=Math.abs(right[i+c]-left[i+c]);
 }
 assert.ok(lashChange>3000,'the whole eye including upper outline must move, not only its iris');
 const centroid=buf=>{let sum=0,xsum=0,ysum=0;for(let y=49*4;y<61*4;y++)for(let x=18*4;x<44*4;x++){
  const i=(y*384+x)*4,r=buf[i],g=buf[i+1],b=buf[i+2],weight=Math.max(0,Math.min(b-g-12,b-r+12));sum+=weight;xsum+=x*weight;ysum+=y*weight;
 }return [xsum/sum,ysum/sum]};
 assert.ok(centroid(right)[0]-centroid(left)[0]>.5,'iris colour must follow horizontal gaze');
 assert.ok(centroid(right)[1]-centroid(left)[1]>.2,'iris colour must follow vertical gaze');
 for(let y=0;y<384;y++)for(let x=0;x<384;x++){
  const i=(y*384+x)*4;assert.equal(right[i+3],neutral[i+3],'iris motion must not punch alpha holes');
  const allowed=x>=15*4&&x<30*4&&y>=43*4&&y<60*4||x>=32*4&&x<46*4&&y>=46*4&&y<63*4;
  if(!allowed||x>=25*4&&x<34*4&&y>=57*4&&y<62*4)for(let c=0;c<4;c++)assert.equal(right[i+c],neutral[i+c],'mouth and artwork outside eye neighborhoods stay untouched');
 }
 assert.deepEqual(data(0,0),neutral,'neutral gaze restores exact original eye texture');
 console.log('PASS whole-eye rendering: moving upper outline and iris, unchanged alpha/mouth/outer art, exact neutral return');
})();
