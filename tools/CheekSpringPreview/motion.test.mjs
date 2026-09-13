import {test} from 'node:test';
import assert from 'node:assert/strict';
import {releaseRemaining} from './motion.mjs';
test('release snaps through rest and rebounds with a diminishing amplitude',()=>{
  assert.equal(releaseRemaining(0),1);
  assert.ok(releaseRemaining(60)<.4);
  assert.ok(Math.abs(releaseRemaining(100))<.08);
  assert.ok(releaseRemaining(170)<-.2);
  assert.ok(releaseRemaining(350)>.04);
  assert.ok(releaseRemaining(350)<.1);
  assert.equal(releaseRemaining(480),0);
  assert.equal(releaseRemaining(2000),0);
});
test('after the initial snap there are two diminishing recoil peaks, no long tail',()=>{
  const peaks=[];
  for(let t=101;t<480;t++){
    const a=releaseRemaining(t-1),b=releaseRemaining(t),c=releaseRemaining(t+1);
    if((b<a&&b<c)||(b>a&&b>c))peaks.push(b);
  }
  assert.equal(peaks.length,2);
  assert.ok(peaks[0]<-.2&&peaks[1]>.04&&peaks[1]<.1);
  for(let t=480;t<=2000;t+=10)assert.equal(releaseRemaining(t),0);
});
test('release has no jump at400ms and is sampled independently of frame rate',()=>{
  assert.ok(Math.abs(releaseRemaining(399)-releaseRemaining(401))<.01);
  for(let t=0;t<=2000;t+=5)assert.ok(Number.isFinite(releaseRemaining(t)));
});
