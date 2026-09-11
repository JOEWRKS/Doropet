// Isolated, scalable seated-contour proof; not a product bank generator.
const {foreground} = require('./rig.js');
const fmt = n => Number(n.toFixed(4));
const xy = p => p.map(fmt).join(' ');

// Measured landmarks from the approved seated endpoint, in its logical96
// coordinates. Fit the visible stroke (including the cleft's dark stem),
// not alpha=.5: the PNG contains partly transparent ink inside that stem.
// A bitmap alpha trace mistakes that ink for holes and leaves detached dots.
const body = `M 17.9 68.8
  C 18.4 74.0 19.4 81.5 22.8 85.5
  C 24.4 87.5 27.5 88.0 29.5 85.4
  C 31.5 82.7 31.9 79.1 32.8 77.25
  C 34.1 80.0 34.5 84.9 37.0 86.7
  C 38.5 88.2 42.3 87.8 44.05 85.3
  C 44.8 84.3 45.1 83.2 45.45 82.1
  C 45.1 85.4 46.1 87.2 49.0 87.25
  C 54.7 87.45 61.0 87.8 65.0 85.1
  C 69.5 82.3 71.0 78.6 70.9 74.5
  C 70.8 68.7 67.6 63.3 64.6 59.1
  L 58 55 L 20 55 Z`;
const seam = 'M 45.45 82.1 C 45.8 80.5 46.5 78.7 47.7 77.8 C 49 77.0 50.4 76.8 51.7 76.2';
function createSvg(before) {
  const protectedHead=`M ${foreground.map(xy).join(' L ')} Z`;
  const bitmap=before.toBuffer('image/png').toString('base64');
  return `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="96" height="96" viewBox="0 0 96 96">
  <defs>
    <path id="silhouette" d="${body}"/>
    <clipPath id="inside"><use xlink:href="#silhouette"/></clipPath>
    <mask id="body" maskUnits="userSpaceOnUse" x="0" y="0" width="96" height="96" style="mask-type:luminance">
      <rect width="96" height="96" fill="white"/>
      <path d="${protectedHead}" fill="black" stroke="black" stroke-width="4.5" stroke-linejoin="round"/>
    </mask>
    <mask id="original" maskUnits="userSpaceOnUse" x="0" y="0" width="96" height="96" style="mask-type:luminance">
      <path d="${protectedHead}" fill="white" stroke="white" stroke-width="5.5" stroke-linejoin="round"/>
    </mask>
  </defs>
  <g mask="url(#body)">
    <use xlink:href="#silhouette" fill="white"/>
    <use xlink:href="#silhouette" fill="none" stroke="#5c5c5c" stroke-width="1.9" stroke-linejoin="round" clip-path="url(#inside)"/>
    <path d="${seam}" fill="none" stroke="#5c5c5c" stroke-width=".95" stroke-linecap="round"/>
  </g>
  <image width="96" height="96" xlink:href="data:image/png;base64,${bitmap}" mask="url(#original)"/>
</svg>`;
}
module.exports={createSvg};
