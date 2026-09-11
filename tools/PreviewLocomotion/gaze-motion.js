(function(root){
  const clamp=x=>Math.max(-1,Math.min(1,x));
  function create(){
    let ex=0,ey=0,hx=0,hy=0;
    return {step(dt,target,flip=false,active=true){
      if(!Number.isFinite(dt)||dt<0)throw Error('Nonnegative finite delta required');
      const valid=active&&target&&target.length===2&&target.every(Number.isFinite);
      const x=valid?clamp(target[0]/110)*(flip?-1:1):0,y=valid?clamp(target[1]/85):0;
      const eye=1-Math.exp(-dt/.075),head=1-Math.exp(-dt/.23);
      ex+=(x-ex)*eye;ey+=(y-ey)*eye;hx+=(x-hx)*head;hy+=(y-hy)*head;
      return {eyeX:.85*ex,eyeY:.65*ey,headX:0,headY:0,roll:(.4375*hx-.5625*hy)*(Math.PI/9)};
    }};
  }
  const api={create};if(typeof module!=='undefined')module.exports=api;else root.gazeMotion=api;
})(globalThis);
