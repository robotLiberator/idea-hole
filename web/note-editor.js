(()=>{
  const sheet=document.createElement('section');sheet.className='note-editor';sheet.hidden=true;sheet.setAttribute('role','dialog');sheet.setAttribute('aria-label','展开便签');
  sheet.innerHTML=`<button class="editor-close" aria-label="收起便签"><svg viewBox="0 0 24 24"><path d="m5 5 14 14M19 5 5 19"/></svg></button><div class="editor-scroll"><textarea class="editor-title" rows="1" maxlength="60" aria-label="标题" placeholder="标题"></textarea><textarea class="editor-summary" rows="1" maxlength="1200" aria-label="一句话描述" placeholder="一句话描述这个想法…"></textarea><div class="editor-content" contenteditable="true" role="textbox" aria-label="详细内容" aria-multiline="true" spellcheck="false"></div></div>`;
  const bar=document.createElement('div');bar.className='editor-tools';bar.hidden=true;bar.setAttribute('role','toolbar');bar.setAttribute('aria-label','文字格式');
  bar.innerHTML=`<button data-format="bold" aria-label="加粗"><b>B</b></button><button data-format="italic" aria-label="斜体"><i>I</i></button><select aria-label="标题级别"><option value="p">正文</option><option value="h1">标题 1</option><option value="h2">标题 2</option><option value="h3">标题 3</option><option value="h4">标题 4</option></select><button data-format="highlight" aria-label="高亮"><svg viewBox="0 0 24 24"><path d="m9 15 8-10 4 4-8 9-4-3ZM9 15l-4 5h7M3 22h14"/></svg></button><button data-format="image" aria-label="插入图片"><svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="m4 17 5-5 4 4 3-3 4 4"/><circle cx="15" cy="9" r="1"/></svg></button>`;
  document.body.append(sheet,bar);
  const title=sheet.querySelector('.editor-title'),summary=sheet.querySelector('.editor-summary'),content=sheet.querySelector('.editor-content'),scroll=sheet.querySelector('.editor-scroll');
  let current=null,range=null,importing=false,drag=null;
  const positions=new Map();
  const pending=new Map();
  function resizeField(el){el.style.height='auto';el.style.height=el.scrollHeight+'px'}
  function sync(){if(!current)return;current.Title=title.value;current.Body=summary.value;current.Markdown=NoteMarkdown.toMarkdown(content);content.dataset.empty=(!content.textContent.trim()&&!content.querySelector('img')).toString();saveSoon()}
  function close(){if(!current)return;sync();current=null;range=null;sheet.hidden=bar.hidden=true;clearTimeout(saveTimer);host({type:'saveNotes',notes});renderNotes()}
  function open(note){if(current)close();const origin=Array.from(board.querySelectorAll('.note')).find(el=>el.dataset.id===note.Id)?.getBoundingClientRect();selectNote(null);current=note;sheet.style.setProperty('--editor-paper',colors[note.Color]||colors.cream);title.value=note.Title||'';summary.value=note.Body||'';content.innerHTML=NoteMarkdown.toHtml(note.Markdown||'');sheet.hidden=false;
    sheet.dataset.color=note.Color;
    const saved=positions.get(note.Id),rect=sheet.getBoundingClientRect();
    sheet.style.left=Math.max(8,Math.min(innerWidth-rect.width-8,saved?.x??origin?.left??rect.left))+'px';
    sheet.style.top=Math.max(8,Math.min(innerHeight-rect.height-8,saved?.y??origin?.top??rect.top))+'px';
    renderNotes();resizeField(title);resizeField(summary);scroll.scrollTop=0;content.dataset.empty=(!content.textContent.trim()&&!content.querySelector('img')).toString();sheet.querySelector('.editor-close').focus({preventScroll:true});range=null;bar.hidden=true}
  // The unprinted paper margins are the drag surface; text remains selectable.
  sheet.addEventListener('pointerdown',e=>{
    if(e.button!==0||e.target.closest('button,textarea,[contenteditable]'))return;
    const r=sheet.getBoundingClientRect();
    if(e.clientX>=r.right-16)return; // Preserve the scrollbar.
    drag={id:e.pointerId,x:e.clientX,y:e.clientY,left:r.left,top:r.top};
    sheet.setPointerCapture(e.pointerId);sheet.classList.add('dragging');bar.hidden=true;e.preventDefault();
  });
  sheet.addEventListener('pointermove',e=>{
    if(!drag||drag.id!==e.pointerId)return;
    const x=drag.left+e.clientX-drag.x,y=drag.top+e.clientY-drag.y;
    sheet.style.left=x+'px';sheet.style.top=y+'px';positions.set(current.Id,{x,y});
  });
  function endDrag(){drag=null;sheet.classList.remove('dragging')}
  sheet.addEventListener('pointerup',endDrag);sheet.addEventListener('pointercancel',endDrag);sheet.addEventListener('lostpointercapture',endDrag);
  function remember(){const sel=getSelection();if(!current||!sel.rangeCount||!content.contains(sel.anchorNode)||!content.contains(sel.focusNode))return false;range=sel.getRangeAt(0).cloneRange();return true}
  function restore(){content.focus({preventScroll:true});const sel=getSelection();sel.removeAllRanges();if(range&&content.contains(range.commonAncestorContainer))sel.addRange(range);else{const r=document.createRange();r.selectNodeContents(content);r.collapse(false);sel.addRange(r);range=r}}
  function showToolsAt(x,y){if(!current||importing||!remember()){bar.hidden=true;return}bar.hidden=false;const sr=sheet.getBoundingClientRect();bar.style.left=Math.max(sr.left+8,Math.min(sr.right-bar.offsetWidth-8,x+10))+'px';bar.style.top=Math.max(sr.top+8,Math.min(sr.bottom-bar.offsetHeight-8,y+10))+'px';
    bar.querySelector('select').value=(document.queryCommandValue('formatBlock')||'p').toLowerCase().replace(/[<>]/g,'');for(const f of ['bold','italic'])bar.querySelector(`[data-format="${f}"]`).setAttribute('aria-pressed',document.queryCommandState(f));
  }
  function format(action,value){restore();document.execCommand('styleWithCSS',false,false);if(action==='highlight'){const sel=getSelection(),parent=sel.anchorNode?.parentElement;const marked=parent?.closest('mark,[style*="background-color"]');document.execCommand('hiliteColor',false,marked?'transparent':'#ffeb9a')}else document.execCommand(action,false,value||null);remember();sync();bar.hidden=true}
  function requestImage(file){if(!current||importing)return;remember();const key=crypto.randomUUID();pending.set(key,{note:current,range:range?.cloneRange()});importing=true;bar.hidden=true;
    if(file){if(!/^image\/(png|jpeg|webp|gif|bmp)$/.test(file.type)||file.size>20*1024*1024){importing=false;pending.delete(key);toast('请选择 20 MB 以内的常见图片。');return}const reader=new FileReader();reader.onload=()=>host({type:'importNoteImage',requestId:key,data:String(reader.result)});reader.onerror=()=>{pending.delete(key);importing=false;toast('图片读取失败。')};reader.readAsDataURL(file)}else host({type:'pickNoteImage',requestId:key});
  }
  function receive(data){if(data.type!=='noteImage')return false;const item=pending.get(data.requestId);pending.delete(data.requestId);importing=false;if(!item)return true;if(data.error){toast(data.error);return true}if(!NoteMarkdown.imagePath(data.path||'')){bar.hidden=true;return true}
    if(current===item.note){range=item.range;restore();document.execCommand('insertHTML',false,`<p><img src="https://noteimages.desktopgrowth.local/${data.path.split('/')[1]}" data-note-image="${data.path}" alt="图片"></p><p><br></p>`);remember();sync();bar.hidden=true}
    else{item.note.Markdown=(item.note.Markdown||'')+'\n\n![图片]('+data.path+')';saveSoon()}return true;
  }
  title.oninput=()=>{resizeField(title);sync()};summary.oninput=()=>{resizeField(summary);sync()};
  content.addEventListener('input',sync);
  content.addEventListener('contextmenu',e=>{e.preventDefault();e.stopPropagation();showToolsAt(e.clientX,e.clientY)});
  content.addEventListener('paste',e=>{e.preventDefault();const file=Array.from(e.clipboardData.files).find(f=>f.type.startsWith('image/'));if(file){requestImage(file);return}const text=e.clipboardData.getData('text/plain');document.execCommand('insertText',false,text);sync()});
  content.addEventListener('dragover',e=>e.preventDefault());content.addEventListener('drop',e=>{e.preventDefault();const file=Array.from(e.dataTransfer.files).find(f=>f.type.startsWith('image/'));if(file)requestImage(file)});
  bar.addEventListener('pointerdown',e=>{remember();if(e.target.closest('button'))e.preventDefault()});
  bar.addEventListener('click',e=>{const action=e.target.closest('button')?.dataset.format;if(action==='image')requestImage();else if(action)format(action)});
  bar.querySelector('select').onchange=e=>format('formatBlock',e.target.value);
  document.addEventListener('pointerdown',e=>{if(!bar.contains(e.target))bar.hidden=true});scroll.addEventListener('scroll',()=>{bar.hidden=true});window.addEventListener('resize',()=>{bar.hidden=true;if(current){const r=sheet.getBoundingClientRect();sheet.style.left=Math.max(0,Math.min(innerWidth-r.width,r.left))+'px';sheet.style.top=Math.max(0,Math.min(innerHeight-r.height,r.top))+'px';resizeField(title);resizeField(summary)}});
  sheet.querySelector('.editor-close').onclick=close;
  document.addEventListener('keydown',e=>{if(current&&e.key==='Escape'){e.preventDefault();e.stopImmediatePropagation();close()}if(current&&(e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='s'){e.preventDefault();sync();clearTimeout(saveTimer);host({type:'saveNotes',notes})}},true);
  window.NoteEditor={open,close,receive,sync,isOpen:()=>!!current,isExpanded:id=>current?.Id===id};
  window.flushForClose=()=>{sync();clearTimeout(saveTimer);return notes};
})();
