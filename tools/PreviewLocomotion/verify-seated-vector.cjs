// Regression gates for the displayed SVG, not just its control points.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const canvas = require(path.join(process.env.USERPROFILE, '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
const {current} = require('./verify-seated-contour-proof.cjs');
const rig = require('./rig.js');
const sharp = require(path.join(process.env.USERPROFILE, '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp'));

async function render(svg, size) {
  const png = await sharp(Buffer.from(svg.replace('width="96" height="96"', `width="${size}" height="${size}"`))).png().toBuffer();
  const source = await canvas.loadImage(png);
  const out = canvas.createCanvas(size, size);
  out.getContext('2d').drawImage(source, 0, 0, size, size);
  return out;
}
async function run() {
  const before = current(), modulePath = path.join(__dirname, 'seated-vector.cjs');
  const useOld = process.argv.includes('--rejected') || !fs.existsSync(modulePath);
  const svg = useOld
    ? `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="96" height="96" viewBox="0 0 96 96"><image width="96" height="96" xlink:href="data:image/png;base64,${fs.readFileSync(path.resolve(__dirname, '../../artifacts/repro/seated-contour-proof-20260910/candidate.png')).toString('base64')}"/></svg>`
    : require(modulePath).createSvg(before);
  const failures = [], metrics = [];
  function check(condition, message) { if (!condition) failures.push(message); }
  const original = before.getContext('2d').getImageData(0, 0, 96, 96).data;
  for (const scale of [1, 2, 4]) {
    const size = scale * 96, image = await render(svg, size);
    const pixels = image.getContext('2d').getImageData(0, 0, size, size).data;
    if (scale === 1) {
      // The rejected Gaussian filled these hand-checked cleft samples.
      for (const [x,y,max] of [[33,81,118],[32,82,40],[33,83,30]]) {
        check(pixels[(y*96+x)*4+3] <= max, `front-paw cleft filled at ${x},${y}: alpha ${pixels[(y*96+x)*4+3]} > ${max}`);
      }
      let changedHead = 0, areaA = 0, areaB = 0;
      for (let y=0;y<96;y++) for (let x=0;x<96;x++) {
        const i=(y*96+x)*4;
        if (rig.headDistance(x+.5,y+.5)<=1) {
          if (original.subarray(i,i+4).some((v,k)=>Math.abs(v-pixels[i+k])>2)) changedHead++;
        } else { areaA+=original[i+3];areaB+=pixels[i+3]; }
      }
      check(changedHead===0, `protected head/ribbon altered: ${changedHead} pixels`);
      check(Math.abs(areaB/areaA-1)<.02, `body silhouette area changed >2%: ${areaB/areaA}`);
      metrics.push({scale,changedHead,areaRatio:areaB/areaA,
        cleftAlpha:[[33,81],[32,82],[33,83]].map(([x,y])=>pixels[(y*96+x)*4+3])});
    }
    // At 4x a real curve still gets ~1 device pixel of coverage, not four
    // enlarged native-pixel alpha steps. Measure several editable rump rows.
    const widths=[];
    for(const y of [71,74,77]) {
      const row=Math.floor((y+.5)*scale),alpha=x=>pixels[(row*size+x)*4+3];
      const crossing=level=>{
        for(let x=85*scale;x>65*scale;x--) if(alpha(x)>=level) {
          const a=alpha(x),b=alpha(x+1);return x+(level-a)/(b-a);
        }
        throw Error(`Missing rump crossing scale=${scale} y=${y} level=${level}; alphas=${Array.from({length:20},(_,n)=>alpha((65+n)*scale))}`);
      };
      widths.push(crossing(26)-crossing(230));
    }
    if(scale===4) check(Math.max(...widths)<1.8, `outline enlarged from coarse pixels: 10-90% edge widths ${widths}`);
    metrics.push({scale,edgeWidths:widths});
  }
  console.log(JSON.stringify({useOld,metrics,failures},null,2));
  assert.equal(failures.length,0,failures.join('\n'));
  console.log('PASS: cleft, head, body area and display-resolution edge');
  return {svg,before};
}
module.exports={render,run};
if(require.main===module) run().catch(error=>{console.error(error.message);process.exitCode=1;});
