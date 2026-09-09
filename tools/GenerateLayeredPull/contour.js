// Signed silhouette correspondence, not alpha crossfade. Color is transported
// to matching contour depth in each source before blending, so old outline
// locations cannot remain as translucent parallel strokes.
(function(root){
 const clamp=v=>Math.max(0,Math.min(1,v));
 function scalar(p,x,y){
  const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;
  const at=(x,y)=>x<0||y<0||x>=96||y>=96?-96:p[y*96+x];
  return (at(ix,iy)*(1-fx)+at(ix+1,iy)*fx)*(1-fy)+(at(ix,iy+1)*(1-fx)+at(ix+1,iy+1)*fx)*fy;
 }
 function prepare(p){
  const alpha=(x,y)=>x<0||y<0||x>=96||y>=96?0:p[(y*96+x)*4+3]/255;
  const edges=[];
  for(let y=-1;y<96;y++)for(let x=-1;x<96;x++)for(const [dx,dy]of[[1,0],[0,1]]){
   const a=alpha(x,y),b=alpha(x+dx,y+dy);
   if((a>=.5)!==(b>=.5)){const t=(.5-a)/(b-a);edges.push([x+dx*t,y+dy*t]);}
  }
  const field=new Float32Array(96*96);
  for(let y=0;y<96;y++)for(let x=0;x<96;x++){
   const a=alpha(x,y);
   if(a>0&&a<1){field[y*96+x]=a-.5;continue;}
   let d=96;
   for(const e of edges)d=Math.min(d,Math.hypot(x-e[0],y-e[1]));
   field[y*96+x]=(a>=.5?1:-1)*Math.max(.5,d);
  }
  return {pixels:p,field};
 }
 function pixel(p,x,y,c){
  const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;
  const at=(x,y)=>x<0||y<0||x>=96||y>=96?0:p[(y*96+x)*4+c];
  return (at(ix,iy)*(1-fx)+at(ix+1,iy)*fx)*(1-fy)+(at(ix,iy+1)*(1-fx)+at(ix+1,iy+1)*fx)*fy;
 }
 function transport(layer,p,depth){
  let [x,y]=p;
  for(let iteration=0;iteration<3;iteration++){
   const distance=scalar(layer.field,x,y),delta=depth-distance;
   if(Math.abs(delta)<.01)break;
   const gx=(scalar(layer.field,x+.5,y)-scalar(layer.field,x-.5,y));
   const gy=(scalar(layer.field,x,y+.5)-scalar(layer.field,x,y-.5));
   const norm=gx*gx+gy*gy;
   if(norm<.01)break;
   // Bound actual spatial displacement, not just the scalar depth error.
   // Near a medial axis a small gradient otherwise amplifies an 8px error
   // into an out-of-image lookup. Accept only residual-reducing steps.
   let step=Math.max(-8,Math.min(8,delta/Math.sqrt(norm)))/Math.sqrt(norm);
   for(let backtrack=0;backtrack<8;backtrack++,step*=.5){
    const nx=x+gx*step,ny=y+gy*step;
    if(nx<0||ny<0||nx>95||ny>95)continue;
    if(Math.abs(depth-scalar(layer.field,nx,ny))<Math.abs(delta)){
     x=nx;y=ny;break;
    }
   }
  }
  return [x,y];
 }
 function sample(a,b,pa,pb,t){
  const da=scalar(a.field,...pa),db=scalar(b.field,...pb),depth=da*(1-t)+db*t;
  const alpha=clamp(depth+.5);
  if(alpha===0)return [0,0,0,0];
  const qa=transport(a,pa,depth),qb=transport(b,pb,depth);
  const aa=pixel(a.pixels,...qa,3),ab=pixel(b.pixels,...qb,3);
  const result=[0,0,0,Math.round(alpha*255)];
  for(let c=0;c<3;c++){
   const ca=aa>.01?pixel(a.pixels,...qa,c)/aa:1;
   const cb=ab>.01?pixel(b.pixels,...qb,c)/ab:1;
   result[c]=Math.round(clamp(ca*(1-t)+cb*t)*result[3]);
  }
  return result;
 }
 const api={prepare,sample};
 if(typeof module!=='undefined')module.exports=api;else root.contourTween=api;
})(globalThis);
