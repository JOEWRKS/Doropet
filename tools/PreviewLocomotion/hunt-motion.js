(function(root){
  const clamp=x=>Math.max(0,Math.min(1,x)),ease=x=>{x=clamp(x);return x*x*(3-2*x)};
  const duration=2.6;
  function sample(t){
    if(!Number.isFinite(t))throw Error('Finite time required');
    const amount=ease((t-.25)/.45)*(1-ease((t-2.05)/.55));
    const envelope=ease((t-.7)/.18)*(1-ease((t-1.65)/.25));
    return {amount,headBob:6.3*amount,headRoll:-.07*amount,sway:1.425*Math.sin((t-.75)*Math.PI*5)*envelope,
      label:t<.25?'발견 · 잠깐 집중':t<.7?'앞발 뻗고 가슴 낮추기':t<1.9?'엉덩이 씰룩씰룩':t<2.05?'잠깐 노려보기':t<duration?'자세 풀기':'다시 쉬기'};
  }
  function head(x,y,p){
    const a=p.headRoll,dx=x-43,dy=y-48;
    return [43+dx*Math.cos(a)-dy*Math.sin(a),48+dx*Math.sin(a)+dy*Math.cos(a)+p.headBob];
  }
  function floor(x){
    const points=[[23,79],[43,85],[54.5,78.5],[66,82]];
    for(let i=1;i<points.length;i++)if(x<=points[i][0]){
      const a=points[i-1],b=points[i],u=clamp((x-a[0])/(b[0]-a[0]));return a[1]+(b[1]-a[1])*u;
    }return 82;
  }
  function map(x,y,p){
    const h=head(x,y,p),rear=ease((x-48)/23),contact=1-ease((y-60)/(floor(x)-60));
    // Begin compression across the torso, not just the final short foreleg
    // segment: deeper lowering must not fold the limb mesh onto itself.
    // Forefeet slide forward during entry, then stay planted during the loop.
    // Distribute the reach from the upper foreleg to the wrist. Concentrating
    // eleven pixels of reach in a five-pixel wrist made a hooked S-bend;
    // the longer attachment keeps the capsule-shaped toe joined smoothly.
    const fore=1-ease((x-46)/8),rootY=66+8*ease((x-24)/12),reach=ease((y-rootY)/11);
    // Round the leading shoulder of each pad instead of letting the old
    // outer ankle corner become a pointed fin. Pad centers/soles stay fixed.
    const toeCenter=23+20*ease((x-27)/5),rounding=1-ease((x-toeCenter+7)/7);
    const px=x+((h[0]-x)*(1-rear)+p.sway*rear)*contact-(11-2.2*rounding)*p.amount*fore*reach;
    const py=y+((h[1]-y)*(1-rear)-1.6*p.amount*rear+.22*p.sway*rear)*contact;
    // Add pad volume in destination space, with a monotone vertical warp.
    // Applying it before the large wrist shear could fold the ankle cells.
    const farToe=23-11*p.amount;
    const padFloor=79+6*ease((px-farToe-3)/9),padZone=1-ease((px-44)/9);
    const padLift=1.15*p.amount*padZone*ease((py-padFloor+9)/5)*ease((padFloor-py)/3);
    return [px,py-padLift];
  }
  function createSession(){
    let start=null,releaseAt=null;
    return {update(now,near){
      if(!Number.isFinite(now))throw Error('Finite session time required');
      if(start===null){if(!near)return duration;start=now;}
      const elapsed=Math.max(0,now-start);
      // 1.25 and1.65 are the same settled sway peak (one full period).
      // Keep replaying that middle span, never the crouch entry or recovery.
      // On departure join the source clip's eased tail at the next matching
      // peak, where velocity is zero, instead of snapping the rump to center.
      if(!near&&releaseAt===null)releaseAt=start+1.65+Math.max(0,Math.ceil((elapsed-1.65)/.4-1e-9))*.4;
      if(near&&releaseAt!==null&&now<releaseAt)releaseAt=null;
      if(releaseAt!==null&&now>=releaseAt){
        const age=1.65+now-releaseAt;
        if(age>=duration){start=null;releaseAt=null;return duration;}
        return age;
      }
      return elapsed<1.65?elapsed:1.25+(elapsed-1.25)%.4;
    }};
  }
  const api={sample,map,head,duration,createSession};
  if(typeof module!=='undefined')module.exports=api;else root.huntMotion=api;
})(globalThis);
