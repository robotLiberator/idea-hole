// Deterministic static paper tiles: generated once, with no per-frame filters.
// Grain is behind, not over, editable text.
(()=>{
  let seed=30721;const random=()=>{seed=(seed*1664525+1013904223)>>>0;return seed/4294967296};
  function grain(size,strength,fibers){
    const canvas=document.createElement('canvas');canvas.width=canvas.height=size;
    const ctx=canvas.getContext('2d'),pixels=ctx.createImageData(size,size);
    for(let i=0;i<pixels.data.length;i+=4){const n=(random()+random()+random()-1.5);const light=n>0;pixels.data[i]=pixels.data[i+1]=pixels.data[i+2]=light?255:0;pixels.data[i+3]=Math.round(Math.abs(n)*strength)}
    ctx.putImageData(pixels,0,0);
    for(let i=0;i<fibers;i++){
      const x=random()*size,y=random()*size,len=3+random()*12;
      ctx.strokeStyle=i%2?'rgba(255,255,255,.018)':'rgba(67,45,32,.007)';ctx.lineWidth=.5+random()*.5;
      ctx.beginPath();ctx.moveTo(x,y);ctx.bezierCurveTo(x+len*.25,y-len*.1,x+len*.6,y-len*.7,x+len,y-len*.6);ctx.stroke();
    }
    // Low-frequency paper variation, rather than strong salt-and-pepper noise.
    for(let i=0;i<100;i++){const x=random()*size,y=random()*size,r=8+random()*30;const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,i%2?'rgba(255,255,255,.025)':'rgba(100,73,46,.006)');g.addColorStop(1,'transparent');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2)}
    return `url("${canvas.toDataURL()}")`;
  }
  document.documentElement.style.setProperty('--paper-grain',grain(384,1.5,3000));
  document.documentElement.style.setProperty('--panel-grain',grain(256,1.5,0));
  document.documentElement.style.setProperty('--back-grain',grain(384,1.5,0));
})();
