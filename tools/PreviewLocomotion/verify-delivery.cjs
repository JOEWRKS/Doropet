const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
(async()=>{
  const response=await fetch('http://127.0.0.1:2785/',{signal:AbortSignal.timeout(5000)});
  assert.equal(response.status,200);
  const html=await response.text();
  // All consumed textures travel in the page without another origin request.
  for(const [id,file] of [['sourceCanonical','dororong-canonical.png'],['sourceClosed','dororong-closed-eyes.png'],['sourceSleep','dororong-sleep.png']]){
    const match=html.match(new RegExp('<img id="'+id+'"[^>]*src="data:image/png;base64,([A-Za-z0-9+/=]+)"'));
    assert.ok(match,`${id}: delivered page depends on a separate image request`);
    assert.deepEqual(Buffer.from(match[1],'base64'),fs.readFileSync(path.resolve(__dirname,'../../src/Dororong.App/Assets',file)),`${id}: delivered texture differs from source`);
  }
  for(const [id,file,mime] of [['sourceFinalSeated','assets/seated-final-10-2.png','image/png'],['sourceFinalBank','assets/seated-final-10-2-bank.png','image/png']]){
    const match=html.match(new RegExp('<img id="'+id+'"[^>]*src="data:'+mime+';base64,([A-Za-z0-9+/=]+)"'));
    assert.ok(match,`${id}: authored source is absent from delivered preview`);
    assert.deepEqual(Buffer.from(match[1],'base64'),fs.readFileSync(path.join(__dirname,file)));
  }
  console.log('PASS: delivered page includes all5 exact source/bank images without separate image requests');
})().catch(error=>{console.error(error);process.exitCode=1;});
