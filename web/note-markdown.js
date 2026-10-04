/* A deliberately small Markdown dialect for the note editor. No HTML execution,
   remote images or automatic links. Unsupported syntax remains readable text. */
(()=>{
  const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const literal=s=>s.replace(/([\\*_=~`\[\]<>#])/g,'\\$1');
  function imagePath(path){return /^(?:便签图片\/)[a-f0-9]{32}\.png$/.test(path)?path:null}
  function inline(text){
    let out='';
    for(let i=0;i<text.length;){
      if(text[i]==='\\'&&i+1<text.length){out+=esc(text[i+1]);i+=2;continue}
      const token=text.startsWith('***',i)?'***':text.startsWith('**',i)?'**':text.startsWith('==',i)?'==':text[i]==='*'?'*':null;
      if(token){let end=i+token.length;while((end=text.indexOf(token,end))>=0&&text[end-1]==='\\')end+=token.length;
        if(end>i+token.length){const tag=token==='**'?'strong':token==='=='?'mark':'em';const value=inline(text.slice(i+token.length,end));out+=token==='***'?'<strong><em>'+value+'</em></strong>':'<'+tag+'>'+value+'</'+tag+'>';i=end+token.length;continue}}
      out+=esc(text[i++]);
    }return out;
  }
  function toHtml(md){
    const lines=String(md||'').replace(/\r/g,'').split('\n');let html='',paragraph=[],list=null;
    const flush=()=>{if(paragraph.length){html+='<p>'+paragraph.map(inline).join('<br>')+'</p>';paragraph=[]}if(list){html+='</'+list+'>';list=null}};
    for(const line of lines){
      if(!line.trim()){flush();continue}
      const heading=line.match(/^(#{1,4}) (.*)$/),img=line.match(/^!\[(.*?)\]\((.*?)\)$/),item=line.match(/^([-*]|\d+\.) (.*)$/);
      if(heading){flush();html+=`<h${heading[1].length}>${inline(heading[2])}</h${heading[1].length}>`}
      else if(img&&imagePath(img[2])){flush();html+=`<p><img src="https://noteimages.desktopgrowth.local/${img[2].split('/')[1]}" data-note-image="${esc(img[2])}" alt="${esc(img[1])}"></p>`}
      else if(item){if(paragraph.length)flush();const type=/\d/.test(item[1])?'ol':'ul';if(list!==type){if(list)html+='</'+list+'>';html+='<'+type+'>';list=type}html+='<li>'+inline(item[2])+'</li>'}
      else{if(list)flush();paragraph.push(line)}
    }flush();return html||'<p><br></p>';
  }
  function toMarkdown(root){
    function children(el){return Array.from(el.childNodes).map(walk).join('')}
    function walk(node){
      if(node.nodeType===3)return literal(node.textContent.replace(/\u00a0/g,' '));
      if(node.nodeType!==1)return '';const tag=node.tagName,content=()=>children(node);
      if(['SCRIPT','STYLE','IFRAME','OBJECT'].includes(tag))return '';
      if(tag==='BR')return '\n';
      if(tag==='IMG'){const path=imagePath(node.getAttribute('data-note-image')||'');return path?'\n\n!['+(node.getAttribute('alt')||'图片').replace(/[\[\]\\\r\n]/g,'')+']('+path+')\n\n':''}
      if(['STRONG','B','EM','I','MARK','SPAN'].includes(tag)){
        let value=content();
        if(tag==='STRONG'||tag==='B')value='**'+value+'**';
        if(tag==='EM'||tag==='I')value='*'+value+'*';
        const background=node.style.backgroundColor;
        if((tag==='MARK'&&!background)||(background&&background!=='transparent'&&background!=='rgba(0, 0, 0, 0)'))value='=='+value+'==';
        return value;
      }
      if(/^H[1-4]$/.test(tag))return '\n\n'+'#'.repeat(Number(tag[1]))+' '+content().trim()+'\n\n';
      if(tag==='LI'){const ordered=node.parentElement.tagName==='OL',index=Array.from(node.parentElement.children).indexOf(node)+1;return (ordered?index+'. ':'- ')+content().trim()+'\n'}
      if(tag==='UL'||tag==='OL')return '\n\n'+content()+'\n';
      if(tag==='P'||tag==='DIV')return '\n\n'+content()+'\n\n';
      return content();
    }
    return children(root).replace(/[ \t]+\n/g,'\n').replace(/\n{3,}/g,'\n\n').trim();
  }
  window.NoteMarkdown={toHtml,toMarkdown,imagePath,escape:esc};
})();
