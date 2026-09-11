const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({rig,renderer})=>{
  const read=p=>Uint8ClampedArray.from(renderer.render(p).getContext('2d').getImageData(0,0,256,256).data);
  function ink(d,p,x,y){
    const c=rig.body(x,y,p),a=rig.body(x,y-.1,p),b=rig.body(x,y+.1,p),len=Math.hypot(b[0]-a[0],b[1]-a[1]);
    const nx=-(b[1]-a[1])/len,ny=(b[0]-a[0])/len;let mass=0;
    for(let t=-4;t<=4;t+=.25){
      const px=(c[0]+16)*2+nx*t,py=(c[1]+16)*2+ny*t,ix=Math.floor(px),iy=Math.floor(py),fx=px-ix,fy=py-iy;
      for(let yy=0;yy<2;yy++)for(let xx=0;xx<2;xx++){const i=((iy+yy)*256+ix+xx)*4,w=(xx?fx:1-fx)*(yy?fy:1-fy);mass+=(255-Math.min(d[i],d[i+1],d[i+2]))*d[i+3]/255*w*.25;}
    }return mass;
  }
  const rest=rig.pose({}),base=read(rest);let worst=1,where='';
  for(let distance=0;distance<10;distance+=.5){const p=rig.pose({walk:1,distance}),frame=read(p);
    for(const [x,y] of [[27,76],[34,79],[61,78]]){const ratio=ink(frame,p,x,y)/ink(base,rest,x,y);if(ratio<worst){worst=ratio;where=`${distance} at ${x},${y}`;}}
  }
  assert.ok(worst>=.95,`joint stroke loses weight: ${(worst*100).toFixed(1)}% of resting ink, ${where}`);
  console.log(`PASS: moving joint ink >=${(worst*100).toFixed(1)}% of rest`);
}).catch(e=>{console.error(e);process.exitCode=1;});
