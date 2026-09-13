// User-reviewed revision: immediate inward velocity, first rest crossing92ms,
// strong recoil160ms and smaller recoil345ms. Fade out before a third lobe.
// This deliberately supersedes the original Anime.js .65/400 parameter study.
export const settlingMs=440;
export function releaseRemaining(ms) {
  if(!Number.isFinite(ms))throw new TypeError('Finite elapsed time required');
  if(ms<=0)return 1;
  if(ms>=settlingMs)return 0;
  const t=ms/1000,tail=Math.max(0,Math.min(1,(ms-360)/80));
  return Math.exp(-7.5*t)*Math.cos(17*t)*(1-tail*tail*(3-2*tail));
}

// Native-pixel presentation offset only: keep the cheek/rig pose bank unchanged.
// Light pulls get a readable recoil; the final quarter adds a stronger backward bounce.
export function releaseOffset(ms,pull,carriedOffset=0) {
  if(![ms,pull,carriedOffset].every(Number.isFinite))throw new TypeError('Finite motion inputs required');
  const smooth=x=>x*x*(3-2*x),clamp=x=>Math.max(0,Math.min(1,x));
  const strength=clamp(pull/20),amplitude=.6*smooth(clamp(strength/.25))+1.4*smooth(clamp((strength-.75)/.25));
  const pulse=ms<=0||ms>=300?0:Math.sin(Math.PI*ms/300)**2;
  // A new kick uses only the unoccupied offset range after a regrab.
  const carried=2*clamp(carriedOffset/2);
  return amplitude*pulse*(1-carried/2)+carried*(1-smooth(clamp(ms/settlingMs)));
}
