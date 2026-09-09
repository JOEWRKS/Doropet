(function(root){
  const baseFactory=typeof module!=='undefined'?require('./morph-v1.js'):root.createLegacyMorph;
  const detailFactory=typeof module!=='undefined'?require('./morph.js'):root.createMorph;
  const contour=typeof module!=='undefined'?require('./contour.js'):root.contourTween;
  const jaw=[64,62,58,54,52,50,50,50];
  const shoulders=[[[24,65],[42,67]],[[25,63],[43,65]],[[26,59],[43,61]],[[27,55],[42,57]],[[30,53],[45,55]],[[32,51],[49,53]],[[34,51],[51,53]],[[36,51],[54,53]]];
  const tips=[[[24,80],[43,85]],[[25,79],[43,83]],[[26,76],[43,79]],[[27,71],[41,71]],[[30,67],[45,68]],[[32,64],[49,64]],[[34,64],[51,64]],[[36,65],[54,65]]];
  const hips=[[53,74],[51,75],[50,74],[47,72],[47,72],[47,72],[46,72],[45,72]];
  const rearTips=[[53,75],[49,84],[48,83],[46,83],[46,83],[46,83],[46,83],[45,83]];
  const masks=[
    [[[16,64],[32,64],[32,73],[29,79],[26,83],[21,83],[17,80]],[[31,65],[53,65],[54,75],[50,80],[50,86],[46,89],[40,88],[34,82]]],
    [[[17,62],[32,62],[32,73],[29,79],[26,82],[21,82],[18,79]],[[32,63],[54,63],[54,74],[49,75],[49,80],[48,83],[46,86],[40,86],[34,80]]],
    [[[18,58],[34,58],[35,69],[34,73],[31,77],[28,79],[23,80],[19,76]],[[34,59],[55,59],[55,71],[51,76],[50,79],[48,81],[42,81],[37,80],[34,74]]],
    [[[19,54],[35,54],[36,62],[34,68],[30,74],[25,75],[21,71]],[[35,55],[57,55],[56,62],[50,69],[44,75],[39,75],[35,70]]],
    [[[21,52],[38,52],[38,60],[35,66],[31,70],[27,71],[23,67]],[[38,53],[59,53],[59,58],[54,65],[48,71],[43,71],[39,66]]],
    [[[24,50],[41,50],[41,57],[38,63],[34,67],[29,68],[26,64]],[[41,51],[62,51],[61,58],[57,63],[52,67],[47,67],[43,63]]],
    [[[26,50],[43,50],[43,57],[40,63],[36,67],[31,68],[28,64]],[[43,51],[64,51],[63,58],[59,63],[54,67],[49,67],[45,63]]],
    [[[28,50],[45,50],[46,57],[43,63],[39,68],[34,68],[30,64]],[[45,52],[65,52],[64,58],[60,64],[56,68],[51,68],[47,64]]]
  ];
  // Measured right contour rows. The polygon approximation must not leave
  // the actual inner stroke behind in the body, or take the adjacent near paw.
  const farRight=[null,null,null,null,
    {60:38,61:38,62:38,63:38,64:37,65:36,66:36,67:35,68:33},
    {58:41,59:41,60:40,61:39,62:39,63:38,64:37,65:36},
    {58:43,59:43,60:42,61:42,62:41,63:40,64:39,65:38},
    {58:46,59:46,60:45,61:45,62:44,63:44,64:43,65:42}
  ];
  const lerp=(a,b,t)=>a+(b-a)*t;
  function inside(poly,x,y){let yes=false;for(let i=0,j=poly.length-1;i<poly.length;j=i++){
    const a=poly[i],b=poly[j];if((a[1]>y)!=(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0])yes=!yes;
  }return yes;}
  function createLayered(data,decode){
    const frames=data.frames.map(decode);
    const layers=frames.map((frame,k)=>{
      const head=new Uint8Array(frame.length),body=frame.slice(),arms=[new Uint8Array(frame.length),new Uint8Array(frame.length)];
      const h=data.heads[k],pinkBottom=new Int16Array(96).fill(-1);
      for(let y=0;y<96;y++)for(let x=0;x<96;x++){
        const i=(y*96+x)*4;if(frame[i+3]>64&&frame[i+2]>frame[i]+12&&frame[i]>frame[i+1]+12)
          for(let dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<96)pinkBottom[x+dx]=Math.max(pinkBottom[x+dx],y+2);
      }
      for(let y=0;y<96;y++)for(let x=0;x<96;x++){
        const i=(y*96+x)*4;
        // Full hair-tip ownership replaces the horizontal centroid gate.
        // Diagonal rear boundary includes ribbon, excludes the rump behind it.
        const edge=x<=h.x+18?jaw[k]+1:h.y+20-Math.max(0,x-h.x-23)*.9;
        const isHead=y<=Math.max(edge,pinkBottom[x]);
        const end=farRight[k]?.[y];
        const far=inside(masks[k][0],x+.5,y+.5)||(end!==undefined&&x>=end-2&&x<end)||
          (k===0&&x===16&&y===72)||(k===1&&x===17&&y>=71&&y<=73);
        const hindfoot=(k===1&&y>=84&&x>=46)||(k===2&&y>=81);
        const arm=isHead?-1:far?0:!hindfoot&&inside(masks[k][1],x+.5,y+.5)?1:-1;
        if(isHead)head.set(frame.subarray(i,i+4),i);
        else if(arm>=0)arms[arm].set(frame.subarray(i,i+4),i);
        if(isHead||arm>=0){
          body.fill(0,i,i+4);
          // A local hidden-torso closure only inside removed masks. It is not
          // source art: early poses leave a belly gap; later ones reveal torso.
          const y0=jaw[k]-1,depth=y-y0;
          const left=k<3?lerp(18+k,56,Math.min(1,Math.max(0,depth/15))):lerp(20+(k-3)*3,45,Math.min(1,Math.max(0,depth/30)));
          const floor=k<3?jaw[k]+12:86;
          if(y>=y0&&y<=floor&&x>=left&&frame[i+3]===255)body[i]=body[i+1]=body[i+2]=body[i+3]=255;
        }
      }
      const rear=new Uint8Array(frame.length);
      // The small far hindpaw is behind the trunk and both forelegs. Separate
      // its visible patch so torso mapping cannot fade it in at a second place.
      for(let y=hips[k][1];y<96;y++)for(let x=38;x<57;x++){
        const i=(y*96+x)*4;rear.set(body.subarray(i,i+4),i);body.fill(0,i,i+4);
      }
      return {head,body,arms,rear};
    });
    const bodyModel=baseFactory({...data,frames:layers.map(l=>l.body),contours:true},x=>x);
    const armFields=layers.map(l=>l.arms.map(contour.prepare));
    const rearFields=layers.map(l=>contour.prepare(l.rear));
    const headModel=detailFactory({...data,frames:layers.map(l=>l.head),headOnly:true},x=>x);
    function sample(progress){
      if(!Number.isFinite(progress))throw Error('Non-finite progress');
      const position=Math.max(0,Math.min(1,progress))*7;
      if(Math.abs(position-Math.round(position))<1e-12)return frames[Math.round(position)];
      const k=Math.floor(position),t=position-k,output=new Uint8Array(96*96*4);
      function overlay(a,b,mapA,mapB){
        for(let y=0;y<96;y++)for(let x=0;x<96;x++){
          const pa=mapA(x,y),pb=mapB(x,y),i=(y*96+x)*4;
          const pixel=contour.sample(a,b,pa,pb,t),alpha=pixel[3];
          for(let c=0;c<4;c++)output[i+c]=Math.max(0,Math.min(255,Math.round(pixel[c]+output[i+c]*(1-alpha/255))));
          for(let c=0;c<3;c++)output[i+c]=Math.min(output[i+c],output[i+3]);
        }
      }
      function articulate(ra,rb,ta,tb,a,b){
        const r=ra.map((v,i)=>lerp(v,rb[i],t)),tip=ta.map((v,i)=>lerp(v,tb[i],t));
        const vx=tip[0]-r[0],vy=tip[1]-r[1],length=Math.hypot(vx,vy);
        function map(sr,st){return (x,y)=>{
          const along=((x-r[0])*vx+(y-r[1])*vy)/(length*length),cross=((x-r[0])*vy-(y-r[1])*vx)/length;
          const sx=st[0]-sr[0],sy=st[1]-sr[1],sl=Math.hypot(sx,sy);
          return [sr[0]+along*sx+cross*sy/sl,sr[1]+along*sy-cross*sx/sl];
        };}
        overlay(a,b,map(ra,ta),map(rb,tb));
      }
      function over(pixels){
        for(let i=0;i<output.length;i+=4)for(let c=0;c<4;c++)
          output[i+c]=Math.min(255,Math.round(pixels[i+c]+output[i+c]*(1-pixels[i+3]/255)));
      }
      articulate(hips[k],hips[k+1],rearTips[k],rearTips[k+1],rearFields[k],rearFields[k+1]);
      over(bodyModel.sample(progress));
      // Explicit depth order: rear paw, trunk, far foreleg, near foreleg, head.
      for(let arm=0;arm<2;arm++)articulate(shoulders[k][arm],shoulders[k+1][arm],tips[k][arm],tips[k+1][arm],armFields[k][arm],armFields[k+1][arm]);
      // Detail registration is confined to the isolated head texture. There is
      // no body mesh gate here; even the distal hair tips move as head pixels.
      const head=headModel.sample(progress);
      over(head);
      return output;
    }
    return {sample,frames,heads:data.heads,layers};
  }
  if(typeof module!=='undefined')module.exports=createLayered;else root.createLayered=createLayered;
})(globalThis);
