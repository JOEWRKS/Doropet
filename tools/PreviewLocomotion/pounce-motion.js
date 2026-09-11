(function(root){
 const clamp=x=>Math.max(0,Math.min(1,x)),ease=x=>{x=clamp(x);return x*x*(3-2*x)};
 const dwellTime=1.5,flightTime=.34,landingTime=.28,trackingTime=3;
 function create(){
  let phase='watch',elapsed=0,x=0,origin=0,distance=0,direction=-1,dwell=0,count=0;
  function pose(){
   let y=0,angle=0,scaleX=1,scaleY=1;
   if(phase==='flight'){
    const u=clamp(elapsed/flightTime);
    x=origin+distance*ease(u);y=-12*4*u*(1-u);
    angle=.12*Math.sin(Math.PI*u)-.10*ease((u-.5)/.5);
    scaleX=1+.035*Math.sin(Math.PI*u);
   }else if(phase==='landing'){
    const u=clamp(elapsed/landingTime);
    angle=-.10*(1-ease(u/.55));const compression=Math.sin(Math.PI*clamp(u/.8));
    scaleY=1-.065*compression;scaleX=1+.025*compression;
   }
   const age=phase==='flight'?elapsed:phase==='landing'?flightTime+elapsed:0;
   return {phase,x,y,angle,scaleX,scaleY,direction,dwell,age,count,
    trackingRemaining:phase==='track'?Math.max(0,trackingTime-elapsed):0};
  }
  return {step(dt,near,targetX){
   if(!Number.isFinite(dt)||dt<0)throw Error('Nonnegative finite delta required');
   const valid=Number.isFinite(targetX);near=!!near&&valid;
   let remaining=dt;
   // Consume boundaries separately: tracking gets a full3s and no dwell
   // from that interval leaks into the next preparation.
   for(;;){
    if((phase==='track'||phase==='watch'&&near)&&valid&&Math.abs(targetX-x)>4)direction=targetX>x?1:-1;
    if(phase==='watch'){
     if(!near){dwell=0;return pose();}
     const used=Math.min(remaining,dwellTime-dwell);dwell+=used;remaining-=used;
     if(dwell<dwellTime-1e-9)return pose();
     origin=x;distance=direction*Math.min(28,Math.max(0,Math.abs(targetX-x)-8));
     phase='flight';elapsed=0;dwell=dwellTime;count++;
    }else{
     const duration=phase==='flight'?flightTime:phase==='landing'?landingTime:trackingTime;
     const used=Math.min(remaining,duration-elapsed);elapsed+=used;remaining-=used;
     if(elapsed<duration-1e-9)return pose();
     elapsed=0;
     if(phase==='flight'){x=origin+distance;phase='landing';}
     else if(phase==='landing'){phase='track';dwell=0;}
     else{phase='watch';dwell=0;}
    }
    if(remaining<=1e-9)return pose();
   }
  }};
 }
 const api={create,dwellTime,flightTime,landingTime,trackingTime};
 if(typeof module!=='undefined')module.exports=api;else root.pounceMotion=api;
})(globalThis);
