const assert=require('node:assert/strict');
require('./raster-harness.cjs').renderer().then(({rig,renderer})=>{
  const read=p=>Uint8ClampedArray.from(renderer.render(p).getContext('2d').getImageData(0,0,256,256).data);
  const a=read(rig.pose({})),b=read(rig.pose({walk:1e-7}));
  let error=0,count=0;
  for(let i=0;i<a.length;i++)if(i%4===3){error+=Math.abs(a[i]-b[i]);count++;}
  assert.ok(error/count<.1,`first motion frame shifts the original silhouette: ${error/count}`);
  const firstSit=read(rig.pose({sit:1e-7}));let entryError=0;
  for(let i=3;i<a.length;i+=4)entryError+=Math.abs(a[i]-firstSit[i]);
  assert.ok(entryError/(256*256)<.1,`sitting swaps raster style on its first frame: ${entryError/(256*256)}`);
  // Full-amplitude gait: these known solid attachment interiors must stay
  // opaque in the actual composed bitmap, not just in rig coordinates.
  for(let distance=0;distance<10;distance+=.5){
    const p=rig.pose({walk:1,distance}),frame=read(p);
    for(const [cx,cy] of [[22,71],[40,77],[65,73]])for(const dx of [-1,0,1]){
      const x=Math.round((cx+dx+16)*2),y=Math.round((cy+p.bob+16)*2);
      assert.ok(frame[(y*256+x)*4+3]>240,`walking attachment is transparent at distance ${distance}, root ${cx}, offset ${dx}`);
    }
  }
  const sit=read(rig.pose({sit:1}));
  // Head stays on the standing artwork. Forepaws now deliberately align to the
  // seated floor; their new contact contract is in verify-seated-floor.cjs.
  for(const amount of [.25,.5,.75,1]){
    const frame=amount===1?sit:read(rig.pose({sit:amount}));
    for(const [x,y] of [[57,73],[60,71],[64,67],[67,65]]){
      assert.ok(frame[((y+16)*2*256+(x+16)*2)*4+3]>245,`diagonal torso hole at sit ${amount}, ${x},${y}`);
    }
    for(const [left,top,right,bottom] of [[19,29,47,60]]){
      let difference=0,n=0;
      for(let y=(top+16)*2;y<(bottom+16)*2;y++)for(let x=(left+16)*2;x<(right+16)*2;x++)for(let c=0;c<4;c++){
        const i=(y*256+x)*4+c;difference+=Math.abs(frame[i]-a[i]);n++;
      }
      assert.ok(difference/n<1,`sitting changes head at ${amount}: ${difference/n}`);
    }
  }
  const pixel=(x,y)=>Array.from(sit.slice(((Math.round((y+16)*2))*256+Math.round((x+16)*2))*4,((Math.round((y+16)*2))*256+Math.round((x+16)*2))*4+4));
  assert.ok(pixel(74,76)[3]>245,'seated rump must form a filled round haunch, not an extended paw');
  assert.ok(pixel(73,87)[3]<20,'seated paw protrudes below the tucked silhouette');
  const edge=[];for(let x=75;x<84;x+=.5)edge.push(pixel(x,72));
  assert.ok(edge.some(p=>p[3]>180&&Math.max(...p.slice(0,3))<140),'redrawn rear outline is missing or too faint');
  console.log('PASS: original-to-motion alignment, round seated haunch, tucked paw and visible rear stroke');
}).catch(error=>{console.error(error);process.exitCode=1;});
