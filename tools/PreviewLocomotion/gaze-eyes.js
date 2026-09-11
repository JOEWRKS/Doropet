(function(root){
 const clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x)};
 const bump=(v,a,b,c,d)=>smooth((v-a)/(b-a))*smooth((d-v)/(d-c));
 // Full feature translation plateaus include the lashes and lower outline.
 // The displacement blends out through nearby skin, so no duplicate eye or
 // painted sclera is left behind. Outside these neighborhoods is unchanged.
 const eyes=[{bounds:[15,43,30,60],x:[15,17,27,30],y:[43,46,57,60]},
   {bounds:[32,46,46,63],x:[32,34,43,46],y:[46,49,60,63]}];
 function weight(x,y,e){
  if(x>=25&&x<34&&y>=57&&y<62)return 0;
  return bump(x,...e.x)*bump(y,...e.y);
 }
 function create(image,makeCanvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c}){
  const native=makeCanvas(96,96),nc=native.getContext('2d');nc.drawImage(image,0,0,96,96);
  const source=nc.getImageData(0,0,96,96).data,base=makeCanvas(384,384),bc=base.getContext('2d');bc.drawImage(native,0,0,384,384);
  const output=makeCanvas(384,384),ctx=output.getContext('2d');
  const patches=eyes.map(e=>{const [l,t,r,b]=e.bounds;return {e,l,t,w:(r-l)*4,h:(b-t)*4,original:bc.getImageData(l*4,t*4,(r-l)*4,(b-t)*4)}});
  function sample(x,y,c){const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;let v=0;
   for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++)v+=source[((iy+dy)*96+ix+dx)*4+c]*(dx?fx:1-fx)*(dy?fy:1-fy);return v;
  }
  function paint(dx=0,dy=0){
   if(![dx,dy].every(Number.isFinite))throw Error('Finite eye offsets required');
   dx=Math.max(-.85,Math.min(.85,dx));dy=Math.max(-.65,Math.min(.65,dy));
   ctx.clearRect(0,0,384,384);ctx.drawImage(base,0,0);
   if(Math.abs(dx)+Math.abs(dy)<1e-8)return output;
   for(const p of patches){const patch=ctx.createImageData(p.w,p.h);patch.data.set(p.original.data);
    for(let y=0;y<p.h;y++)for(let x=0;x<p.w;x++){
     const sx=p.l+(x+.5)/4-.5,sy=p.t+(y+.5)/4-.5,m=weight(sx+.5,sy+.5,p.e),i=(y*p.w+x)*4;
     if(!m)continue;
     // One rigid offset over the complete eye; the surrounding falloff fills
     // vacated space from original pixels without replacing or scaling the eye.
     for(let c=0;c<3;c++)patch.data[i+c]+=sample(sx-dx*m,sy-dy*m,c)-sample(sx,sy,c);
    }ctx.putImageData(patch,p.l*4,p.t*4);
   }return output;
  }
  return {paint};
 }
 const api={create};if(typeof module!=='undefined')module.exports=api;else root.gazeEyes=api;
})(globalThis);
