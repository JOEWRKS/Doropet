// Frozen comparison renderer/input; regenerate the embedded Pbgra32 bank with Node.
const fs=require('node:fs'), path=require('node:path'), zlib=require('node:zlib');
const data=JSON.parse(fs.readFileSync(path.join(__dirname,'poses.json'),'utf8'));
const model=require('./layered.js')(data,s=>new Uint8Array(Buffer.from(s,'base64')));
const frames=Array.from({length:113},(_,i)=>Buffer.from(model.sample(i/112)));
fs.writeFileSync(path.join(__dirname,'../../src/Dororong.App/Assets/layered-pull.pbgra.gz'),zlib.gzipSync(Buffer.concat(frames)));
console.log('Packaged 113 layered 96x96 Pbgra32 frames. Original art unchanged.');
