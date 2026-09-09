(function(root){
  function createMorph(data,decode){
    const frames=data.frames.map(decode);
    const contour=data.contours?(typeof module!=='undefined'?require('./contour.js'):root.contourTween):null;
    const fields=contour?frames.map(contour.prepare):null;
    function sample(progress){
      if(!Number.isFinite(progress))throw new Error('Non-finite progress');
      const position=Math.max(0,Math.min(1,progress))*7;
      if(Math.abs(position-Math.round(position))<1e-12)return frames[Math.round(position)];
      const key=Math.floor(position),t=position-key,pair=data.pairs[key];
      const a=frames[key],b=frames[key+1],ha=data.heads[key],hb=data.heads[key+1];
      const head={x:ha.x+(hb.x-ha.x)*t,y:ha.y+(hb.y-ha.y)*t};
      const vertices=pair.low.map((p,i)=>({x:p.x+(pair.high[i].x-p.x)*t,y:p.y+(pair.high[i].y-p.y)*t}));
      const mapping=new Float64Array(96*96*4),covered=new Uint8Array(96*96);
      function triangle(ia,ib,ic){
        const p=vertices[ia],q=vertices[ib],r=vertices[ic];
        const det=(q.x-p.x)*(r.y-p.y)-(q.y-p.y)*(r.x-p.x);
        if(det<=.0001)throw new Error('Folded anatomical triangle');
        const left=Math.max(0,Math.floor(Math.min(p.x,q.x,r.x))),right=Math.min(95,Math.ceil(Math.max(p.x,q.x,r.x)));
        const top=Math.max(0,Math.floor(Math.min(p.y,q.y,r.y))),bottom=Math.min(95,Math.ceil(Math.max(p.y,q.y,r.y)));
        for(let y=top;y<=bottom;y++)for(let x=left;x<=right;x++){
          const u=((x-p.x)*(r.y-p.y)-(y-p.y)*(r.x-p.x))/det;
          const v=((q.x-p.x)*(y-p.y)-(q.y-p.y)*(x-p.x))/det;
          if(u < -1e-8 || v < -1e-8 || u+v>1+1e-8)continue;
          const at=(y*96+x)*4;covered[y*96+x]=1;
          for(let side=0;side<2;side++){
            const s=side?pair.high:pair.low;
            mapping[at+side*2]=s[ia].x+(s[ib].x-s[ia].x)*u+(s[ic].x-s[ia].x)*v;
            mapping[at+side*2+1]=s[ia].y+(s[ib].y-s[ia].y)*u+(s[ic].y-s[ia].y)*v;
          }
        }
      }
      for(let row=0;row<3;row++)for(let col=0;col<12;col++){
        const i=row*13+col;triangle(i,i+1,i+14);triangle(i,i+14,i+13);
      }
      const clamp=x=>Math.max(0,Math.min(1,x));
      function map(x,y,h){
        const neck=Math.min(86,head.y+20),sourceNeck=Math.min(86,h.y+20),body=clamp((y-neck)/(87-neck));
        return [x+(h.x-head.x)*(1-body),y<=neck?y+h.y-head.y:y>=87?y:sourceNeck+(y-neck)*(87-sourceNeck)/(87-neck)];
      }
      function read(p,x,y,c){
        const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;
        const pixel=(x,y)=>x<0||y<0||x>=96||y>=96?0:p[(y*96+x)*4+c];
        return (pixel(ix,iy)*(1-fx)+pixel(ix+1,iy)*fx)*(1-fy)+(pixel(ix,iy+1)*(1-fx)+pixel(ix+1,iy+1)*fx)*fy;
      }
      const output=new Uint8Array(96*96*4);
      for(let y=0;y<96;y++)for(let x=0;x<96;x++){
        const at=(y*96+x)*4,ma=map(x,y,ha),mb=map(x,y,hb);
        if(covered[y*96+x]){
          const weight=clamp((y-head.y-20)/5);
          for(let c=0;c<2;c++){ma[c]+=(mapping[at+c]-ma[c])*weight;mb[c]+=(mapping[at+2+c]-mb[c])*weight;}
        }
        if(contour)output.set(contour.sample(fields[key],fields[key+1],ma,mb,t),at);
        else for(let c=0;c<4;c++)output[at+c]=Math.max(0,Math.min(255,Math.round(read(a,ma[0],ma[1],c)*(1-t)+read(b,mb[0],mb[1],c)*t)));
      }
      return output;
    }
    return {sample,frames,heads:data.heads};
  }
  if(typeof module!=='undefined')module.exports=createMorph;else root.createMorph=createMorph;
})(globalThis);
