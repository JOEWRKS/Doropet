(function(root){
  const cycle=10;
  // Left-facing art: far fore, near fore, near hind, occluded far hind.
  // The fourth paw is rendered behind the torso; it must not deform its mesh.
  const rest=[{root:[22,71],tip:[23,79]},{root:[39,77],tip:[43,85]},{root:[64,74],tip:[66,82]},{root:[55,70],tip:[54.5,78.5]}];
  const clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x);};
  const foreground=[[0,0],[96,0],[96,48],[69,48],[69,51],[67,55],[65,59],[63,63],[60,62],[58,58],[56,62],[52,66],[49,70],[46,72],[43,72],[42,68],[23,68],[20,70],[17,70],[0,70]];
  function headDistance(x,y){
    let inside=false,best=1e9;for(let i=0,j=foreground.length-1;i<foreground.length;j=i++){
      const a=foreground[i],b=foreground[j],dx=b[0]-a[0],dy=b[1]-a[1];
      if((a[1]>y)!=(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0])inside=!inside;
      const t=clamp(((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy));best=Math.min(best,Math.hypot(x-a[0]-t*dx,y-a[1]-t*dy));
    }return inside?0:best;
  }
  function trunk(x,y,p){
    if(p.sit===0)return [x,y+p.bob];
    const rear=smooth((x-48)/18)*smooth((headDistance(x,y)-1)/5)*p.sit;
    // Lower the pelvis, then shorten and tuck the same hindleg beneath it.
    // Positive vertical scale below the hip avoids an inverted/folded mesh.
    const lower=y<=74?5*smooth((y-50)/24):5-(y-74)*.55;
    const tuck=4-10*clamp((y-74)/8);
    return [x+tuck*rear,y+p.bob+lower*rear];
  }
  function pose({distance=0,walk=0,sit=0}={}){
    if(![distance,walk,sit].every(Number.isFinite))throw Error('Finite pose inputs required');
    sit=clamp(sit);walk=clamp(walk)*(1-sit);
    const phase=((distance/cycle)%1+1)%1;
    const p={sit,walk,distance,bob:walk?-.55*walk*(1-Math.cos(phase*4*Math.PI)):0,roll:walk?.009*walk*Math.sin(phase*2*Math.PI-.45):0,legs:[]};
    for(let i=0;i<rest.length;i++){
      // Two alternating diagonal pairs: far fore + near hind / the reverse.
      const q=(phase+(i%2===0?0:.5))%1,stance=.6,z=(q-stance)/(1-stance);
      const dx=q<stance?-3+cycle*q:3-6*smooth(z);
      const dy=q<stance?0:-2.8*Math.sin(Math.PI*z)**2;
      const r=rest[i],tip=[r.tip[0]+walk*dx,r.tip[1]+walk*dy];
      if(i>=2){const seated=trunk(...r.tip,{sit,bob:0});tip[0]+=seated[0]-r.tip[0];tip[1]+=seated[1]-r.tip[1];}
      p.legs.push({root:trunk(...r.root,p),tip});
    }
    return p;
  }
  function limb(x,y,index,p){
    const a=rest[index],b=p.legs[index],u=smooth((y-a.root[1])/(a.tip[1]-a.root[1]));
    const base=trunk(x,y,p),end=trunk(...a.tip,p);
    return [base[0]+u*(b.tip[0]-end[0]),base[1]+u*(b.tip[1]-end[1])];
  }
  function body(x,y,p){
    const base=trunk(x,y,p);let dx=0,dy=0,total=0;
    // Only the three authored visible limbs belong to this continuous texture.
    // The far hind limb moves on its own layer, underneath this field.
    for(let i=0;i<3;i++){
      const a=rest[i],u=clamp((y-a.root[1])/(a.tip[1]-a.root[1]));
      const center=a.root[0]+(a.tip[0]-a.root[0])*u;
      const w=1-smooth((Math.abs(x-center)-4)/5);
      if(w<=0)continue;
      const moved=limb(x,y,i,p);dx+=w*(moved[0]-base[0]);dy+=w*(moved[1]-base[1]);total+=w;
    }
    return [base[0]+dx/Math.max(1,total),base[1]+dy/Math.max(1,total)];
  }
  function scenario(t,mode='sequence'){
    if(mode==='sit')return {distance:0,walk:0,sit:smooth(t/.65),position:0,label:t<.65?'앞발 고정 · 뒷다리 접기':'앉아서 쉬기'};
    if(mode==='stand')return {distance:0,walk:0,sit:1-smooth(t/.65),position:0,label:t<.65?'다시 일어나기':'서 있기'};
    if(mode==='walk')return {distance:t*14,walk:1,sit:0,position:0,label:'걷기 · 바닥이 이동하는 접지 비교',belt:t*28};
    const lap=Math.floor(t/8),u=t%8,dir=lap%2?-1:1;
    let distance=0,walk=0,sit=0,label='서 있기';
    // Integrate the same smooth speed envelope used to blend in the gait.
    if(u<.4){const v=u/.4;walk=smooth(v);distance=5.6*(v**3-.5*v**4);label='살짝 출발';}
    else if(u<3.2){distance=2.8+14*(u-.4);walk=1;label='걷기';}
    else if(u<3.8){const v=(u-3.2)/.6;walk=1-smooth(v);distance=42+8.4*(v-v**3+.5*v**4);label='발 모으며 멈춤';}
    else{distance=46.2;if(u<4.2)label='멈춤';else if(u<4.8){sit=smooth((u-4.2)/.6);label='뒷다리 접고 앉기';}else if(u<6.8){sit=1;label='앉아서 쉬기';}else if(u<7.4){sit=1-smooth((u-6.8)/.6);label='다시 일어나기';}}
    return {distance,walk,sit,position:dir*(46.2-distance*2),flip:dir<0,label};
  }
  const api={pose,cycle,trunk,limb,body,rest,smooth,foreground,headDistance,scenario};if(typeof module!=='undefined')module.exports=api;else root.locomotion=api;
})(globalThis);
