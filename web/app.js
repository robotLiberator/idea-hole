const colors=window.SoftTheme.colors;
const paletteColors=window.SoftTheme.swatches;
const visualTest=new URLSearchParams(location.search).has('visual-test');
const pinnedIds=new Set();
let notes=[],selectedId=null,menuOpen=false,saveTimer=null,tasks=[],viewScale=1,notesLoaded=false;
const requestedTaskPage=new URLSearchParams(location.search).get('schedule-page');
let taskPage=['morning','afternoon'].includes(requestedTaskPage)?requestedTaskPage:(localStorage.getItem('schedulePage')||'morning');
const board=document.querySelector('#board'),tools=document.querySelector('#note-tools');

function host(message){if(visualTest){if(message.type==='toggleTask'){const t=tasks.find(t=>t.Id===message.id);if(t)t.Done=message.done;renderTasks()}return}if(window.chrome&&window.chrome.webview)window.chrome.webview.postMessage(message)}
function saveSoon(){clearTimeout(saveTimer);saveTimer=setTimeout(()=>host({type:'saveNotes',notes}),220)}
function maxZ(){return notes.reduce((m,n)=>Math.max(m,n.Z||0),0)}
function minZ(){return notes.reduce((m,n)=>Math.min(m,n.Z||0),0)}
function noteById(id){return notes.find(n=>n.Id===id)}
function swatches(target,onPick,compact=false){
  target.innerHTML='';Object.entries(paletteColors).forEach(([name,value])=>{const b=document.createElement('button');b.className='swatch';b.dataset.color=name;b.style.background=value;b.setAttribute('aria-label',name);b.onclick=e=>{e.stopPropagation();onPick(name)};target.appendChild(b)})
}
swatches(document.querySelector('#palette'),color=>{const n=noteById(selectedId);if(n){n.Color=color;renderNotes();saveSoon()}else addNote(color)});
document.querySelector('#add-note').onclick=()=>addNote('coral');

function addNote(color){
  const offset=(notes.length%5)*24;const n={Id:crypto.randomUUID().replaceAll('-',''),Title:'新便签',Body:'',Color:color||'coral',X:55+offset,Y:55+offset,Size:245,Height:0,Z:maxZ()+1};
  notes.push(n);selectedId=n.Id;menuOpen=false;renderNotes();saveSoon();setTimeout(()=>document.querySelector(`[data-id="${n.Id}"] .note-title`).select(),0)
}
function selectNote(id,openMenu=false){
  selectedId=id;menuOpen=!!id&&openMenu;
  board.querySelectorAll('.note').forEach(el=>el.classList.toggle('selected',el.dataset.id===id));
  positionTools()
}
function renderNotes(){
  board.querySelectorAll('.note').forEach(x=>x.remove());
  [...notes].sort((a,b)=>(a.Z||0)-(b.Z||0)).forEach(n=>{
    if(pinnedIds.has(n.Id)||window.NoteEditor?.isExpanded(n.Id))return;
    const el=document.createElement('article');el.className='note'+(n.Id===selectedId?' selected':'');el.dataset.id=n.Id;el.style.cssText=`left:${n.X}px;top:${n.Y}px;width:${n.Size}px;height:${n.Height||n.Size}px;z-index:${n.Z};--note-size:${n.Size}px;--note-bg:${colors[n.Color]||colors.yellow}`;
    el.dataset.color=n.Color;
    const dragStrip=document.createElement('div');dragStrip.className='drag-strip';
    const title=document.createElement('input');title.className='note-title';title.value=n.Title||'';title.placeholder='标题';title.maxLength=60;
    const body=document.createElement('textarea');body.className='note-body';body.value=n.Body||'';body.placeholder='写点什么…';body.maxLength=1200;
    const resize=document.createElement('div');resize.className='resize-handle';
    title.oninput=()=>{n.Title=title.value;saveSoon()};body.oninput=()=>{n.Body=body.value;saveSoon()};
    title.onpointerdown=body.onpointerdown=e=>{e.stopPropagation();if(e.button===0)selectNote(n.Id)};
    el.oncontextmenu=e=>{e.preventDefault();e.stopPropagation();selectNote(n.Id,true)};
    el.ondblclick=e=>{if(e.target.closest('.resize-handle'))return;e.preventDefault();window.NoteEditor.open(n)};
    el.append(dragStrip,title,body,resize);el.onpointerdown=e=>beginDrag(e,n,el);resize.onpointerdown=e=>beginResize(e,n,el);board.appendChild(el)
  });positionTools()
}
function beginDrag(e,n,el){
  if(e.button!==0||e.target.closest('input,textarea,.resize-handle'))return;e.preventDefault();selectNote(n.Id);
  const startX=e.clientX,startY=e.clientY,ox=n.X,oy=n.Y;el.setPointerCapture(e.pointerId);
  el.onpointermove=ev=>{const b=screenBounds(),grip=48;n.X=Math.max(b.left-n.Size+grip,Math.min(b.right-grip,ox+(ev.clientX-startX)/viewScale));n.Y=Math.max(b.top-(n.Height||n.Size)+grip,Math.min(b.bottom-grip,oy+(ev.clientY-startY)/viewScale));el.style.left=n.X+'px';el.style.top=n.Y+'px';positionTools()};
  el.onpointerup=el.onpointercancel=()=>{el.onpointermove=null;saveSoon()}
}
function beginResize(e,n,el){
  if(e.button!==0)return;e.stopPropagation();e.preventDefault();selectNote(n.Id);const start=e.clientX,base=n.Size,ratio=(n.Height||n.Size)/n.Size;el.setPointerCapture(e.pointerId);
  el.onpointermove=ev=>{n.Size=Math.max(180,Math.min(440,base+(ev.clientX-start)/viewScale));n.Height=n.Size*ratio;el.style.width=n.Size+'px';el.style.height=n.Height+'px';el.style.setProperty('--note-size',n.Size+'px');positionTools()};
  el.onpointerup=el.onpointercancel=()=>{el.onpointermove=null;saveSoon()}
}
function screenBounds(){const r=board.getBoundingClientRect();return{left:-r.left/viewScale,top:-r.top/viewScale,right:(innerWidth-r.left)/viewScale,bottom:(innerHeight-r.top)/viewScale}}
function positionTools(){
  document.querySelectorAll('.swatch').forEach(b=>b.classList.toggle('selected',b.dataset.color===noteById(selectedId)?.Color));
  const el=selectedId&&board.querySelector(`[data-id="${selectedId}"]`);if(!el||!menuOpen){tools.hidden=true;return}tools.hidden=false;
  const b=screenBounds(),x=parseFloat(el.style.left),y=parseFloat(el.style.top),right=x+el.offsetWidth-2;
  const side=right+tools.offsetWidth<=b.right-6?'right':'left';
  const left=Math.max(b.left+6,Math.min(b.right-tools.offsetWidth-6,side==='right'?right:x-tools.offsetWidth+2));
  const top=Math.max(b.top+6,Math.min(b.bottom-tools.offsetHeight-6,y+16));
  tools.dataset.side=side;tools.style.setProperty('--tab-color',colors[noteById(selectedId)?.Color]||colors.cream);tools.style.top=top+'px';tools.style.left=left+'px';
  document.querySelectorAll('.swatch').forEach(b=>b.classList.toggle('selected',b.dataset.color===noteById(selectedId)?.Color))
}
tools.onclick=e=>{
  const action=e.target.closest('button')?.dataset.action;if(!action)return;const n=noteById(selectedId);if(!n)return;
  if(action==='pin'){
    clearTimeout(saveTimer);
    const el=board.querySelector(`[data-id="${n.Id}"]`),r=el.getBoundingClientRect();
    host({type:'pinNote',id:n.Id,notes,x:r.left,y:r.top,width:r.width,height:r.height,viewportWidth:innerWidth,viewportHeight:innerHeight});
    selectNote(null);return
  }
  if(action==='archive'){
    clearTimeout(saveTimer);menuOpen=false;tools.hidden=true;
    host({type:'archiveNote',id:n.Id,notes});return
  }
  if(action==='top')n.Z=maxZ()+1;
  if(action==='bottom')n.Z=minZ()-1;
  if(action==='delete'){notes=notes.filter(x=>x.Id!==selectedId);selectedId=null}renderNotes();saveSoon()
};
board.onclick=e=>{if(e.target===board)selectNote(null)};
document.addEventListener('pointerdown',e=>{if(!e.target.closest('.note,.note-tools,.note-bar'))selectNote(null)});
function fitViewport(){viewScale=Math.min(innerWidth/1586,innerHeight/992);const app=document.querySelector('.app'),offset=(innerWidth-1586*viewScale)/2;app.style.transform=`scale(${viewScale})`;app.style.left=offset+'px';app.style.top=(innerHeight-992*viewScale)/2+'px';document.querySelector('.schedule').style.left=((innerWidth-offset)/viewScale-570)+'px';positionTools()}
window.addEventListener('resize',fitViewport);fitViewport();

function renderTasks(){
  const timeline=document.querySelector('#timeline'),scroll=timeline.scrollTop,focused=document.activeElement?.closest('.task')?.dataset.id;timeline.innerHTML='';
  const pageTasks=tasks.filter(t=>taskPageFor(t)===taskPage),active=pageTasks.find(t=>!t.Done)?.Id;
  const nextPage=taskPage==='morning'?'下午':'早晨';
  document.querySelector('.page-corner').setAttribute('aria-label',`翻到${nextPage}页`);
  document.querySelector('.schedule').setAttribute('aria-label',`日程活页本：${taskPage==='morning'?'早晨':'下午'}页`);
  pageTasks.forEach((t,i)=>{const row=document.createElement('button');row.type='button';row.dataset.id=t.Id;row.className='task'+(t.Done?' done':'')+(t.Id===active?' active':'');
    row.setAttribute('aria-label',`${t.Time} ${t.Title}：${t.Done?'标为未完成':'标为完成'}`);row.setAttribute('aria-pressed',!!t.Done);
    row.innerHTML=`<span class="task-time ${String(t.Time).length>5?'range':''}">${escapeHtml(t.Time)}</span><span class="task-title">${escapeHtml(t.Title)}</span><svg class="hand-check" viewBox="0 0 36 36" aria-hidden="true" style="--check-tilt:${i%2?-5:2}deg"><path class="check-ink" d="M4.2 19.4 Q4 17.8 5.6 18.7 Q9.6 21.5 12.4 25.1 Q19.1 13.1 31.8 6.1 Q33 5.5 31.8 7 Q21.6 15.4 14.6 28.4 Q13.3 30.5 11.7 28.5 Q7.7 23.3 4.2 19.4Z"/></svg><svg class="pencil-rule" viewBox="0 0 360 6" preserveAspectRatio="none" aria-hidden="true"><path d="M2 3 Q65 1.5 125 3 T245 2.5 T358 3"/></svg>`;
    row.onclick=()=>host({type:'toggleTask',id:t.Id,done:!t.Done});timeline.appendChild(row);
    if(t.Id===focused)row.focus({preventScroll:true});
  });timeline.scrollTop=scroll;
}
function taskPageFor(task){const hour=parseInt(String(task.Time||'').slice(0,2),10);return Number.isFinite(hour)&&hour>=14?'afternoon':'morning'}
let pageTurning=false;
document.querySelector('.page-corner').addEventListener('click',async()=>{
  if(pageTurning)return;
  pageTurning=true;
  const schedule=document.querySelector('.schedule'),timeline=document.querySelector('#timeline'),sheet=document.querySelector('.page-turn-sheet');
  const changePage=()=>{taskPage=taskPage==='morning'?'afternoon':'morning';localStorage.setItem('schedulePage',taskPage);timeline.scrollTop=0;renderTasks()};
  schedule.classList.add('is-turning');
  try{
    if(matchMedia('(prefers-reduced-motion: reduce)').matches){changePage();return}
    await timeline.animate([{opacity:1,transform:'translateX(0)'},{opacity:0,transform:'translateX(-10px)'}],{duration:120,easing:'ease-in',fill:'forwards'}).finished;
    sheet.style.visibility='visible';
    const turn=sheet.animate([{transform:'rotateY(0deg)',opacity:.95},{transform:'rotateY(-38deg)',opacity:.85,offset:.45},{transform:'rotateY(-102deg)',opacity:0}],{duration:420,easing:'cubic-bezier(.3,.05,.25,1)',fill:'forwards'});
    changePage();
    const reveal=timeline.animate([{opacity:0,transform:'translateX(8px)'},{opacity:1,transform:'translateX(0)'}],{duration:280,delay:110,easing:'ease-out',fill:'forwards'});
    await Promise.all([turn.finished,reveal.finished]);
  }finally{
    timeline.getAnimations().forEach(animation=>animation.cancel());
    sheet.getAnimations().forEach(animation=>animation.cancel());
    sheet.style.visibility='hidden';schedule.classList.remove('is-turning');pageTurning=false;
  }
});
function escapeHtml(v){const d=document.createElement('div');d.textContent=v??'';return d.innerHTML.replaceAll('"','&quot;').replaceAll("'",'&#39;')}
function toast(text){const e=document.querySelector('#toast');e.textContent=text;e.classList.add('show');setTimeout(()=>e.classList.remove('show'),1800)}
document.addEventListener('keydown',e=>{if(e.key==='Escape'){selectNote(null);document.activeElement?.blur()}});
// Host task refreshes must not replace an in-progress note edit or its caret.
window.hostReceive=data=>{
  if(window.NoteEditor?.receive(data))return;
  if(data.type==='openNoteEditor'){const n=noteById(data.id);if(n)window.NoteEditor.open(n);return}
  if(data.type==='prepareUnpin'){
    pinnedIds.delete(data.id);
    let i=notes.findIndex(n=>n.Id===data.id);if(data.note){if(i>=0)notes[i]=data.note;else{notes.push(data.note);i=notes.length-1}}
    const n=notes[i];if(n){const r=board.getBoundingClientRect();n.X=(data.x-r.left)/viewScale;n.Y=(data.y-r.top)/viewScale}
    renderNotes();let sent=false;const done=()=>{if(sent)return;sent=true;host({type:'unpinReady',id:data.id,notes})};requestAnimationFrame(()=>requestAnimationFrame(done));setTimeout(done,120)
  }else if(data.type==='pinState'){if(data.pinned)pinnedIds.add(data.id);else pinnedIds.delete(data.id);if(data.note){const i=notes.findIndex(n=>n.Id===data.id);if(i>=0)notes[i]=data.note}renderNotes()}
  else if(data.type==='archivedNote'){notes=notes.filter(n=>n.Id!==data.id);if(selectedId===data.id)selectedId=null;renderNotes();toast('已完成并归档')}
  else if(data.type==='state'){tasks=data.tasks||[];renderTasks();if(!notesLoaded){notes=data.notes||[];notesLoaded=true;renderNotes()}}
  else if(data.type==='error')toast(data.message)
};
if(visualTest){
  const titles=['整理本周想法','做一个小作品','读书','出去走走','录一段视频','以后想做的事'];
  const bodies=['把零散的想法写下来，\n留一点空间慢慢想。','先做喜欢的部分，\n再把它打磨好。','读几页，\n记下一句喜欢的话。','晒晒太阳，\n让脑袋休息一下。','把今天的新发现，\n讲得简单一点。','不急着开始，\n先好好收在这里。'];
  const shades=['yellow','blue','coral','cream','peach','lavender'];
  notes=titles.map((Title,i)=>({Id:'fixture-'+i,Title,Body:bodies[i],Color:shades[i],X:(i%3)*334,Y:Math.floor(i/3)*360,Size:310,Height:324,Z:i+1}));
  tasks=[['09:00','起床'],['09:30','出门吃早餐，同时开始今日开局'],['10:00','开始居家健身'],['11:00','买菜和洗澡'],['11:30','开始做午饭'],['13:00','开始午睡'],['14:30','开始下午工作'],['17:30','出门散步、买东西'],['18:00','开始吃晚饭'],['19:00','开始晚上工作'],['22:00','洗澡，开始睡前活动'],['23:30','睡觉']].map(([Time,Title],i)=>({Id:'task-'+i,Title,Time,Done:false}));
  selectedId='fixture-1';notesLoaded=true;renderNotes();renderTasks();
}else host({type:'ready'});
