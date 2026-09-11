const assert=require('node:assert/strict');
// The seated rear is intentionally redrawn, so preserving an artificial green
// texture marker is no longer its contract. Check the actual visible contour
// and premultiplied pixel continuity instead, including entry/exit endpoints.
require('./raster-harness.cjs').renderer().then(({rig,renderer})=>{
  let previous,worst=0;
  const boundary=data=>{
    const points=[];
    // Rear below the ribbon: its tiny authored cutouts are not body contours.
    for(let y=170;y<216;y++)for(let x=64;x<204;x++){
      const i=(y*256+x)*4;
      if(data[i+3]<128)continue;
      if([i-4,i+4,i-1024,i+1024].some(j=>data[j+3]<128))points.push([x,y]);
    }
    return points;
  };
  for(let step=0;step<=100;step++){
    const sit=step/100,data=Uint8ClampedArray.from(renderer.render(rig.pose({sit})).getContext('2d').getImageData(0,0,256,256).data),edge=boundary(data);
    assert.ok(edge.length>40,`rear silhouette vanished at ${sit}`);
    if(previous){
      for(const [from,to] of [[edge,previous.edge],[previous.edge,edge]])for(const a of from){
        const nearest=Math.min(...to.map(b=>Math.hypot(a[0]-b[0],a[1]-b[1])));
        assert.ok(nearest<=2,`rear contour pops ${nearest}px at sit ${sit}`);
      }
      let difference=0,n=0;
      for(let y=170;y<216;y++)for(let x=64;x<204;x++){
        const i=(y*256+x)*4,aa=data[i+3]/255,ab=previous.data[i+3]/255;
        for(let c=0;c<3;c++){difference+=Math.abs(data[i+c]*aa-previous.data[i+c]*ab);n++;}
        difference+=Math.abs(data[i+3]-previous.data[i+3]);n++;
      }
      worst=Math.max(worst,difference/n);
      assert.ok(difference/n<2,`rear raster abruptly changes at sit ${sit}: ${difference/n}`);
    }
    previous={data,edge};
  }
  console.log(`PASS: 101 sitting frames, <=2px contour steps, worst premultiplied rear pixel step ${worst.toFixed(3)}/255`);
}).catch(e=>{console.error(e);process.exitCode=1;});
