// Node nativo, sem bibliotecas. DOM simulado: nao substitui navegador nem QA visual.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../../frontend');
const source = file => fs.readFileSync(path.join(root, file), 'utf8').replace(/^\uFEFF/, '');
const decode = text => text.replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');

class Element {
  constructor(tag) {
    this.tagName = tag.toLowerCase(); this.childNodes = []; this.attributes = {}; this.parentNode = null;
    this.listeners = new Map(); this.style = {}; this._value = undefined;
    this.classList = {
      contains: name => this.className.split(/\s+/).includes(name),
      add: (...names) => { this.className = [...new Set([...this.className.split(/\s+/).filter(Boolean), ...names])].join(' '); },
      remove: (...names) => { this.className = this.className.split(/\s+/).filter(n => !names.includes(n)).join(' '); }
    };
    this.dataset = new Proxy({}, {
      get: (_, key) => this.getAttribute('data-' + String(key).replace(/[A-Z]/g, c => '-' + c.toLowerCase())) ?? undefined,
      set: (_, key, value) => { this.setAttribute('data-' + String(key).replace(/[A-Z]/g, c => '-' + c.toLowerCase()), value); return true; }
    });
  }
  get children() { return this.childNodes.filter(n => n instanceof Element); }
  get className() { return this.attributes.class || ''; }
  set className(value) { this.attributes.class = String(value); }
  get textContent() { return this.childNodes.map(n => n instanceof Element ? n.textContent : n).join(''); }
  set textContent(value) { this.childNodes = [String(value ?? '')]; }
  append(...nodes) { for (const n of nodes) { if (n instanceof Element) n.parentNode = this; this.childNodes.push(n instanceof Element ? n : String(n)); } }
  replaceChildren(...nodes) { this.childNodes = []; this.append(...nodes); }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  getAttribute(name) { return this.attributes[name] ?? null; }
  get disabled() { return 'disabled' in this.attributes; }
  set disabled(value) { if (value) this.attributes.disabled = ''; else delete this.attributes.disabled; }
  get value() {
    if (this._value !== undefined) return this._value;
    if (this.tagName === 'select') return (this.children.find(c => 'selected' in c.attributes) || this.children[0])?.value || '';
    return this.attributes.value ?? (this.tagName === 'option' ? this.textContent : '');
  }
  set value(value) { this._value = String(value); }
  get elements() { return this.querySelectorAll('input,select,textarea,button'); }
  reset() { for (const e of this.elements) e._value = undefined; }
  set innerHTML(html) {
    // Rejeita payload chegando a qualquer parser HTML, mesmo sem um motor JS de navegador.
    assert.ok(!String(html).includes('__THS_XSS'), 'Dados nao confiaveis chegaram ao innerHTML');
    this.replaceChildren(...parse(String(html)).childNodes);
  }
  matches(selector) {
    const attr = selector.match(/\[([^=\]]+)(?:=["']?([^\]"']*)["']?)?\]/);
    const tag = selector.match(/^[a-z][\w-]*/i)?.[0];
    const cls = selector.match(/\.([\w-]+)/)?.[1];
    const id = selector.match(/#([\w-]+)/)?.[1];
    return (!tag || this.tagName === tag) && (!cls || this.classList.contains(cls)) && (!id || this.attributes.id === id)
      && (!attr || (this.getAttribute(attr[1]) !== null && (attr[2] === undefined || this.getAttribute(attr[1]) === attr[2])));
  }
  querySelectorAll(selector) {
    const selectors = selector.split(',').map(s => s.trim()); const found = [];
    const visit = element => { for (const child of element.children) { if (selectors.some(s => child.matches(s))) found.push(child); visit(child); } };
    visit(this); return found;
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
  closest(selector) { return this.matches(selector) ? this : this.parentNode?.closest(selector) || null; }
  addEventListener(type, handler) { if (!this.listeners.has(type)) this.listeners.set(type, []); this.listeners.get(type).push(handler); }
  async emit(type, target = this) { for (const handler of this.listeners.get(type) || []) await handler({ target, preventDefault() {} }); }
}
for (const property of ['href', 'title', 'tabIndex']) Object.defineProperty(Element.prototype, property, {
  get() { return this.getAttribute(property); }, set(value) { this.setAttribute(property, value); }
});
function parse(html) {
  const document = new Element('document'); const stack = [document];
  for (const token of html.match(/<!--[\s\S]*?-->|<![^>]*>|<\/?[^>]+>|[^<]+/g) || []) {
    if (token.startsWith('<!')) continue;
    if (token.startsWith('</')) { if (stack.length > 1) stack.pop(); continue; }
    if (!token.startsWith('<')) { stack.at(-1).append(decode(token)); continue; }
    const tag = token.match(/^<([\w-]+)/)?.[1]; if (!tag) continue;
    const element = new Element(tag);
    const rest = token.slice(tag.length + 1).replace(/\/?\s*>$/, '');
    for (const attr of rest.matchAll(/([^\s=]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s]+)))?/g))
      element.setAttribute(attr[1], decode(attr[2] ?? attr[3] ?? attr[4] ?? ''));
    stack.at(-1).append(element);
    if (!/\/>$/.test(token) && !['input', 'meta', 'link', 'br', 'img', 'hr'].includes(element.tagName)) stack.push(element);
  }
  document.createElement = tag => new Element(tag);
  document.getElementById = id => document.querySelector('#' + id);
  return document;
}
function assertNoActiveContent(container) {
  for (const element of [container, ...container.querySelectorAll('*')]) {
    assert.ok(!['script', 'img', 'iframe', 'object', 'embed'].includes(element.tagName));
    assert.ok(!Object.keys(element.attributes).some(name => /^on/i.test(name)), 'Atributo de evento foi criado a partir de dados');
    const href = element.getAttribute('href');
    if (href) assert.ok(!/^\s*(javascript|data):/i.test(href));
  }
}
const payloads = [
  '<img src=x onerror="globalThis.__THS_XSS=1">',
  '<svg onload="globalThis.__THS_XSS=2"></svg>',
  '</p><script>globalThis.__THS_XSS=3</script>',
  '" autofocus onfocus="globalThis.__THS_XSS=4" data-x="',
  '<b>João & "Ana" \' < > &amp;</b>',
  'javascript:globalThis.__THS_XSS=5'
];
function tickets(text = 'Titulo de teste') {
  return [{ id: 1, titulo: text, descricao: text, usuarioId: 1, tecnicoId: null, categoriaId: 1, prioridadeId: 1, statusId: 1,
    dataAbertura: '2026-10-27T09:00:00', dataFechamento: null, usuario: { id: 1, nome: text, email: 'teste@example.test', tipo: 'Usuario' }, tecnico: null,
    categoria: { id: 1, nome: text }, prioridade: { id: 1, nome: text }, status: { id: 1, nome: 'Aberto ' + text }, comentarios: [] }];
}
async function page(name, options = {}) {
  const document = parse(source('pages/' + name + '.html'));
  const role = options.role || 'Usuario';
  const user = { id: 1, nome: options.text || 'João', email: 'teste@example.test', tipo: role };
  const data = options.tickets || tickets(options.text);
  const comments = [{ id: 1, chamadoId: 1, mensagem: options.text || 'Mensagem', dataEnvio: '2026-10-27T09:00:00', usuario: user }];
  const calls = [], timers = [], alerts = [], confirmations = [];
  const storage = new Map(options.noSession ? [] : [['techhelp_token', 'test-token'], ['techhelp_user', JSON.stringify({ userId: user.id, nome: user.nome, email: user.email, role })]]);
  const location = { search: options.search ?? (name === 'comentarios' ? '?id=1' : ''), href: './' + name + '.html' };
  const response = (value, status = 200) => ({ ok: status < 400, status, text: async () => typeof value === 'string' ? value : JSON.stringify(value) });
  const fetch = async (url, request = {}) => {
    calls.push({ url: String(url), ...request });
    if (options.fetch) { const custom = await options.fetch(url, request); if (custom) return custom; }
    const route = new URL(url, 'http://localhost:5266').pathname;
    if (route === '/api/auth/login') return response({ token: 'test-token', usuario: user });
    if (route === '/api/usuarios/me') return response(user);
    if (route.startsWith('/api/catalogos/')) return response([{ id: 1, nome: options.text || 'Catalogo' }]);
    if (route.endsWith('/comentarios')) {
      if (request.method === 'POST') comments.push({ ...comments[0], id: comments.length + 1, mensagem: JSON.parse(request.body).mensagem });
      return response(request.method === 'POST' ? comments.at(-1) : comments);
    }
    if (route === '/api/usuarios') return response({ ...user, tipo: 'Usuario' });
    if (/\/api\/chamados\/[^/]+$/.test(route) && !route.endsWith('/meus') && !route.endsWith('/meus-atendimentos')) return response(data[0]);
    return response(data);
  };
  const window = { location, confirm: message => { confirmations.push(message); return options.confirm === true; } };
  const context = vm.createContext({ window, document, HTMLElement: Element, URLSearchParams, Headers, FormData, console, fetch,
    localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, String(value)), removeItem: key => storage.delete(key) },
    setTimeout: (callback, delay) => { timers.push({ callback, delay }); }, alert: message => alerts.push(message) });
  const run = code => vm.runInContext(code, context);
  if (name !== 'cadastro') run(source('scripts/app.js'));
  await run(source('scripts/pages/' + name + '.js'));
  return { document, context, calls, timers, alerts, confirmations, storage, location, run, response };
}

test('referencias locais, IDs e scripts/estilos das sete paginas', () => {
  const pages = fs.readdirSync(path.join(root, 'pages')).filter(f => f.endsWith('.html'));
  assert.equal(pages.length, 7);
  for (const file of pages) {
    const html = source('pages/' + file); const document = parse(html);
    for (const match of html.matchAll(/(?:src|href)="([^"?#]+)"/g)) {
      if (/^https?:/.test(match[1])) continue;
      assert.ok(fs.existsSync(path.resolve(root, 'pages', match[1])), match[1]);
    }
    const script = source('scripts/pages/' + file.replace('.html', '.js'));
    for (const match of script.matchAll(/getElementById\("([^"]+)"\)/g)) assert.ok(document.getElementById(match[1]), match[1]);
  }
});
test('sinks HTML restantes: somente literais estaticos ou SVGs fixos', () => {
  const files = [source('scripts/app.js'), ...fs.readdirSync(path.join(root, 'scripts/pages')).map(f => source('scripts/pages/' + f))];
  for (const script of files) {
    assert.doesNotMatch(script, /outerHTML|insertAdjacentHTML|document\.write|createContextualFragment|\beval\(/);
    for (const match of script.matchAll(/\.innerHTML\s*=\s*([^;]+);/g)) {
      const rhs = match[1].trim();
      assert.ok((rhs.startsWith('`') && !rhs.includes('${')) || rhs === 'getStatusIcon(chamado.status?.nome)', rhs);
    }
  }
});
for (const [index, attack] of payloads.entries()) {
  test(`texto literal e ausencia de elementos ativos - payload ${index + 1}`, async () => {
    const user = await page('home-usuario', { text: attack });
    const userList = user.document.getElementById('tickets-list');
    assert.equal(userList.querySelector('.ticket-item__title').textContent, attack);
    assert.equal(userList.querySelector('.ticket-item__description').textContent, attack);
    assertNoActiveContent(userList);
    const tech = await page('home-tecnico', { text: attack, role: 'Tecnico' });
    const techList = tech.document.getElementById('tech-ticket-list');
    assert.equal(techList.querySelector('.tech-ticket-item__title').textContent, attack);
    assert.ok(techList.querySelector('.tech-ticket-item__details').textContent.includes(attack));
    assert.equal(tech.document.getElementById('priority-filter').children[1].textContent, attack);
    assertNoActiveContent(techList);
    const comments = await page('comentarios', { text: attack });
    const thread = comments.document.getElementById('comments-list');
    assert.equal(thread.querySelector('.comment-thread__text').textContent, attack);
    assert.ok(thread.querySelector('.comment-thread__author').textContent.startsWith(attack));
    assertNoActiveContent(thread);
    const manager = await page('home-gestor', { text: attack, role: 'Admin' });
    const sectors = manager.document.getElementById('tickets-by-sector-list');
    const legend = manager.document.getElementById('status-distribution-legend');
    assert.equal(sectors.querySelector('.manager-row-label').textContent, attack);
    assert.ok(legend.querySelector('.manager-legend-name').textContent.includes(attack));
    assertNoActiveContent(sectors); assertNoActiveContent(legend);
    const form = await page('abrir-chamado', { text: attack });
    assert.equal(form.document.getElementById('category').children[1].textContent, attack);
    assertNoActiveContent(form.document.getElementById('category'));
    for (const p of [user, tech, comments, manager, form]) assert.equal(p.run('globalThis.__THS_XSS'), undefined);
  });
}
test('cadastro oferece apenas Usuario e envia valor fixo mesmo com DOM manipulado', async () => {
  const p = await page('cadastro'); const d = p.document;
  assert.deepEqual(d.getElementById('tipo').children.map(o => o.value), ['Usuario']);
  assert.ok(d.getElementById('tipo').disabled);
  for (const id of ['nome', 'email', 'senha', 'confirmar-senha']) assert.notEqual(d.getElementById(id).getAttribute('required'), null);
  assert.equal(d.getElementById('email').getAttribute('type'), 'email');
  for (const id of ['senha', 'confirmar-senha']) {
    assert.equal(d.getElementById(id).getAttribute('type'), 'password');
    assert.equal(d.getElementById(id).getAttribute('minlength'), '4');
  }
  d.getElementById('tipo').value = 'Admin';
  d.getElementById('nome').value = '  João & <Ana>  '; d.getElementById('email').value = ' teste@example.test ';
  d.getElementById('senha').value = d.getElementById('confirmar-senha').value = 'teste';
  await d.getElementById('register-form').emit('submit');
  assert.equal(p.calls.length, 1);
  assert.deepEqual(JSON.parse(p.calls[0].body), { nome: 'João & <Ana>', email: 'teste@example.test', senha: 'teste', tipo: 'Usuario' });
  assert.equal(p.timers[0].delay, 1500); p.timers[0].callback(); assert.equal(p.location.href, './login.html');
});
test('cadastro conserva confirmacao de senha e mensagens de erro como texto', async () => {
  const p = await page('cadastro'); const d = p.document;
  d.getElementById('senha').value = 'teste'; d.getElementById('confirmar-senha').value = 'outra';
  await d.getElementById('register-form').emit('submit'); assert.equal(p.calls.length, 0);
  assert.ok(d.getElementById('register-message').textContent.includes('não conferem'));
  const attack = payloads[0];
  const failed = await page('cadastro', { fetch: async () => ({ ok: false, status: 400, text: async () => attack }) });
  failed.document.getElementById('senha').value = failed.document.getElementById('confirmar-senha').value = 'teste';
  await failed.document.getElementById('register-form').emit('submit');
  assert.equal(failed.document.getElementById('register-message').textContent, attack);
  assertNoActiveContent(failed.document.getElementById('register-message'));
});
test('listagem de usuario conserva ordem, contadores, classes e links', async () => {
  const data = tickets(); data.push({ ...data[0], id: 2, status: { id: 2, nome: 'Em andamento' } }, { ...data[0], id: 3, status: { id: 3, nome: 'Resolvido' } });
  const p = await page('home-usuario', { tickets: data }); const list = p.document.getElementById('tickets-list');
  assert.deepEqual(list.children.map(c => c.querySelector('.ticket-item__number').textContent), ['#1', '#2', '#3']);
  for (const id of ['count-open', 'count-progress', 'count-closed']) assert.equal(p.document.getElementById(id).textContent, '1');
  assert.equal(list.children[0].querySelector('[data-comments]').href, './comentarios.html?id=1');
  assert.equal(list.children[1].querySelector('[data-edit]').getAttribute('aria-disabled'), 'true');
  assert.ok(list.children[0].querySelector('svg').classList.contains('ticket-item__icon'));
});
test('tecnico conserva botoes, delegacao de clique, filtros e navegacao', async () => {
  const p = await page('home-tecnico', { role: 'Tecnico' }); const d = p.document;
  const list = d.getElementById('tech-ticket-list');
  const assume = list.querySelector('[data-action="assumir"]');
  assert.equal(assume.disabled, false); assert.equal(list.querySelector('[data-action="finalizar"]').disabled, true);
  assert.equal(assume.dataset.id, '1');
  await list.emit('click', assume.querySelector('span'));
  assert.ok(p.calls.some(c => c.url.endsWith('/api/chamados/1/assumir') && c.method === 'PUT'));
  d.getElementById('priority-filter').value = '1'; d.getElementById('status-filter').value = '2';
  await d.getElementById('priority-filter').emit('change');
  assert.ok(p.calls.at(-1).url.endsWith('/api/chamados?prioridade=1&status=2'));
  await list.emit('click', list.querySelector('[data-action="editar"]').querySelector('span'));
  assert.equal(p.location.href, './abrir-chamado.html?id=1');
  const assigned = tickets(); assigned[0].tecnicoId = 2;
  const p2 = await page('home-tecnico', { role: 'Tecnico', tickets: assigned });
  const list2 = p2.document.getElementById('tech-ticket-list');
  assert.equal(list2.querySelector('[data-action="assumir"]').disabled, true);
  await list2.emit('click', list2.querySelector('[data-action="finalizar"]').querySelector('span'));
  assert.ok(p2.calls.some(c => c.url.endsWith('/api/chamados/1/finalizar') && c.method === 'PUT'));
});
test('IDs nao viram atributos HTML nem alteram a URL de navegacao', async () => {
  const data = tickets(); data[0].id = '1" onclick="__THS_XSS &outra=1';
  const p = await page('home-usuario', { tickets: data });
  const list = p.document.getElementById('tickets-list');
  assert.equal(list.querySelector('[data-comments]').href, './comentarios.html?id=' + encodeURIComponent(data[0].id));
  assertNoActiveContent(list);
});
test('comentarios conservam autoria visual, envio, atualizacao e retorno', async () => {
  const p = await page('comentarios'); const d = p.document;
  assert.ok(d.getElementById('comments-list').children[0].classList.contains('comment-thread__item--mine'));
  d.getElementById('comment-message').value = '  <b>texto literal</b>  ';
  await d.getElementById('comment-form').emit('submit');
  assert.equal(JSON.parse(p.calls.find(c => c.method === 'POST').body).mensagem, '<b>texto literal</b>');
  assert.equal(d.getElementById('comments-list').children.at(-1).querySelector('.comment-thread__text').textContent, '<b>texto literal</b>');
  assert.equal(d.getElementById('comment-message').value, '');
  p.run('renderComments(' + JSON.stringify([{ id: 3, chamadoId: 1, mensagem: 'Outro autor', dataEnvio: '2026-10-27T09:00:00', usuario: { id: 2, nome: 'Técnico', tipo: 'Tecnico' } }]) + ')');
  assert.ok(d.getElementById('comments-list').children[0].classList.contains('comment-thread__item--other'));
  assert.ok(d.getElementById('comments-list').querySelector('.comment-thread__bubble').classList.contains('comment-thread__bubble--other'));
  await d.getElementById('back-button').emit('click'); assert.equal(p.location.href, './home-usuario.html');
});
test('formulario de chamado conserva catalogos, criacao e edicao', async () => {
  const p = await page('abrir-chamado'); const d = p.document;
  d.getElementById('title').value = ' <b>titulo</b> '; d.getElementById('description').value = ' descricao & especial ';
  d.getElementById('category').value = '1'; d.getElementById('priority').value = '1';
  await d.getElementById('ticket-form').emit('submit');
  const post = p.calls.find(c => c.method === 'POST');
  assert.deepEqual(JSON.parse(post.body), { titulo: '<b>titulo</b>', descricao: 'descricao & especial', categoriaId: 1, prioridadeId: 1 });
  p.timers[0].callback(); assert.equal(p.location.href, './home-usuario.html');
  const edit = await page('abrir-chamado', { search: '?id=1' });
  assert.equal(edit.document.getElementById('title').value, 'Titulo de teste');
  await edit.document.getElementById('ticket-form').emit('submit');
  assert.ok(edit.calls.some(c => c.method === 'PUT' && c.url.endsWith('/api/chamados/1')));
  edit.run('fillSelect(categorySelect, [], "Sem categorias")');
  assert.equal(edit.document.getElementById('category').disabled, true);
  assert.equal(edit.document.getElementById('category').children[0].textContent, 'Sem categorias');
});
test('painel do gestor conserva indicadores, graficos e cores controladas', async () => {
  const data = tickets(); data[0].status = { id: 3, nome: 'Resolvido' }; data[0].dataFechamento = '2026-10-27T09:30:00';
  const p = await page('home-gestor', { role: 'Admin', tickets: data }); const d = p.document;
  assert.equal(d.getElementById('metric-total').textContent, '1'); assert.equal(d.getElementById('metric-open').textContent, '0');
  assert.equal(d.getElementById('metric-avg-time').textContent, '30min'); assert.equal(d.getElementById('metric-sla').textContent, '100%');
  assert.equal(d.getElementById('tickets-by-day-bars').children.length, 7);
  assert.equal(d.getElementById('status-distribution-chart').querySelector('.manager-donut-total').textContent, '1');
  assert.ok(d.getElementById('status-distribution-chart').style.background.startsWith('conic-gradient(rgb('));
});
test('estados vazios e mensagens permanecem funcionais', async () => {
  for (const name of ['home-usuario', 'home-tecnico', 'home-gestor']) {
    const p = await page(name, { role: name === 'home-tecnico' ? 'Tecnico' : name === 'home-gestor' ? 'Admin' : 'Usuario', tickets: [] });
    const id = name === 'home-usuario' ? 'tickets-message' : name === 'home-tecnico' ? 'tech-message' : 'tickets-by-sector-empty';
    assert.ok(!p.document.getElementById(id).classList.contains('hidden'));
  }
});
for (const [role, target] of [['Usuario', 'home-usuario'], ['Tecnico', 'home-tecnico'], ['Gestor', 'home-gestor'], ['Admin', 'home-gestor']]) {
  test(`login e redirecionamento preservados para ${role}`, async () => {
    const p = await page('login', { noSession: true, role }); const d = p.document;
    d.getElementById('email').value = ' teste@example.test '; d.getElementById('password').value = 'teste';
    await d.getElementById('login-form').emit('submit');
    assert.equal(p.location.href, './' + target + '.html');
    assert.equal(JSON.parse(p.storage.get('techhelp_user')).role, role);
    assert.equal(p.storage.get('techhelp_token'), 'test-token');
  });
}
test('cabecalho, logout e autenticacao usam texto e os contratos existentes', async () => {
  const p = await page('home-usuario', { text: payloads[0] });
  for (const element of p.document.querySelectorAll('[data-user-name]')) assert.equal(element.textContent, payloads[0]);
  assert.ok(p.calls.find(c => c.url.endsWith('/api/chamados/meus')).headers.get('Authorization').startsWith('Bearer '));
  const logout = p.document.querySelector('[data-logout]'); await logout.emit('click');
  assert.equal(p.storage.get('techhelp_token'), undefined); assert.equal(p.location.href, './login.html');
});

for (const role of ['Admin', 'Usuario', 'Tecnico', 'Gestor']) {
  test(`exclusao: permissao original ${role}, sem confundir Gestor com Admin`, async () => {
    const p = await page('home-gestor', { role });
    const panel = p.document.getElementById('admin-tickets-panel');
    assert.equal(panel.classList.contains('hidden'), role !== 'Admin');
    assert.equal(panel.querySelectorAll('[data-delete-ticket]').length, role === 'Admin' ? 1 : 0);
    if (role !== 'Admin') {
      await p.run('deleteTicket({id: 1, titulo: "Nao permitido"})');
      assert.equal(p.confirmations.length, 0);
      assert.ok(!p.calls.some(c => c.method === 'DELETE'));
    }
  });
}
test('exclusao: Usuario e Tecnico nao recebem acao em seus paineis', async () => {
  for (const [name, role] of [['home-usuario', 'Usuario'], ['home-tecnico', 'Tecnico']]) {
    const p = await page(name, { role });
    assert.equal(p.document.querySelectorAll('[data-delete-ticket]').length, 0);
  }
});
test('exclusao: cancelar confirmacao identifica chamado e nao envia DELETE', async () => {
  const p = await page('home-gestor', { role: 'Admin', confirm: false });
  const before = p.calls.length;
  await p.document.querySelector('[data-delete-ticket]').emit('click');
  assert.equal(p.calls.length, before);
  assert.ok(p.confirmations[0].includes('#1') && p.confirmations[0].includes('Titulo de teste'));
  assert.equal(p.document.getElementById('metric-total').textContent, '1');
  assert.equal(p.document.querySelector('[data-delete-ticket]').disabled, false);
});
test('exclusao: aceitar usa DELETE parametrizado na URL, bearer e atualiza lista e todos indicadores', async () => {
  const data = tickets(); let removed = false;
  const p = await page('home-gestor', { role: 'Admin', confirm: true, fetch: async (url, request) => {
    if (request.method === 'DELETE') { removed = true; return { ok: true, status: 204, text: async () => '' }; }
    if (new URL(url).pathname === '/api/chamados') return { ok: true, status: 200, text: async () => JSON.stringify(removed ? [] : data) };
  } });
  await p.document.querySelector('[data-delete-ticket]').emit('click');
  const deletes = p.calls.filter(c => c.method === 'DELETE');
  assert.equal(deletes.length, 1); assert.equal(deletes[0].url, 'http://localhost:5266/api/chamados/1');
  assert.equal(deletes[0].headers.get('Authorization'), 'Bearer test-token');
  assert.equal(deletes[0].body, undefined);
  assert.equal(p.document.querySelectorAll('[data-delete-ticket]').length, 0);
  assert.equal(p.document.getElementById('metric-total').textContent, '0');
  assert.equal(p.document.getElementById('metric-open').textContent, '0');
  assert.equal(p.document.getElementById('metric-avg-time').textContent, '0min');
  assert.equal(p.document.getElementById('metric-sla').textContent, '0%');
  for (const id of ['tickets-by-day-empty', 'tickets-by-sector-empty', 'status-distribution-empty', 'admin-tickets-empty'])
    assert.equal(p.document.getElementById(id).classList.contains('hidden'), false);
  assert.ok(p.document.getElementById('admin-tickets-message').textContent.includes('excluído com sucesso'));
  assert.equal(p.calls.filter(c => new URL(c.url).pathname === '/api/chamados').length, 2);
});
for (const [status, expected] of [[401, 'Sessão expirada'], [403, 'permissão'], [404, 'não encontrado'], [500, 'mais tarde']]) {
  test(`exclusao: HTTP ${status} com mensagem segura`, async () => {
    const secret = '<script>detalhes internos SQL e token</script>';
    const p = await page('home-gestor', { role: 'Admin', confirm: true, fetch: async (url, request) =>
      request.method === 'DELETE' ? { ok: false, status, text: async () => secret } : null });
    await p.document.querySelector('[data-delete-ticket]').emit('click');
    const message = p.document.getElementById('admin-tickets-message');
    assert.ok(message.textContent.includes(expected), message.textContent);
    assert.ok(!message.textContent.includes(secret)); assertNoActiveContent(message);
    assert.equal(p.document.getElementById('metric-total').textContent, '1');
    assert.equal(p.document.querySelector('[data-delete-ticket]').disabled, false);
    if (status === 401) { assert.equal(p.location.href, './login.html'); assert.equal(p.storage.has('techhelp_token'), false); }
  });
}
test('exclusao: falha de rede nao expoe excecao nem confirma exclusao', async () => {
  const p = await page('home-gestor', { role: 'Admin', confirm: true, fetch: async (url, request) => {
    if (request.method === 'DELETE') throw new Error('SQL segredo interno');
  } });
  await p.document.querySelector('[data-delete-ticket]').emit('click');
  assert.ok(p.document.getElementById('admin-tickets-message').textContent.includes('Verifique sua conexão'));
  assert.ok(!p.document.getElementById('admin-tickets-message').textContent.includes('segredo'));
  assert.equal(p.document.querySelector('[data-delete-ticket]').disabled, false);
  assert.equal(p.document.getElementById('metric-total').textContent, '1');
});
test('exclusao: sucesso seguido de falha na recarga conserva remocao local e avisa', async () => {
  let removed = false;
  const p = await page('home-gestor', { role: 'Admin', confirm: true, fetch: async (url, request) => {
    if (request.method === 'DELETE') { removed = true; return { ok: true, status: 204 }; }
    if (new URL(url).pathname === '/api/chamados' && removed) throw new Error('Servidor indisponivel');
  } });
  await p.document.querySelector('[data-delete-ticket]').emit('click');
  assert.equal(p.document.querySelectorAll('[data-delete-ticket]').length, 0);
  assert.equal(p.document.getElementById('metric-total').textContent, '0');
  assert.ok(p.document.getElementById('admin-tickets-message').textContent.includes('excluído. Não foi possível sincronizar'));
});
test('exclusao: bloqueia envios duplicados enquanto a requisicao esta em andamento', async () => {
  let finish;
  const p = await page('home-gestor', { role: 'Admin', confirm: true, fetch: async (url, request) => {
    if (request.method === 'DELETE') return new Promise(resolve => { finish = resolve; });
  } });
  const first = p.document.querySelector('[data-delete-ticket]').emit('click');
  assert.equal(p.document.querySelector('[data-delete-ticket]').disabled, true);
  await p.document.querySelector('[data-delete-ticket]').emit('click');
  assert.equal(p.calls.filter(c => c.method === 'DELETE').length, 1);
  assert.equal(p.confirmations.length, 1);
  finish({ ok: false, status: 403 }); await first;
  assert.equal(p.document.querySelector('[data-delete-ticket]').disabled, false);
});
test('exclusao: titulo malicioso permanece texto inclusive na confirmacao e ID e codificado', async () => {
  for (const attack of payloads) {
    const data = tickets(attack); data[0].id = '1/"?x=__THS_XSS';
    const p = await page('home-gestor', { role: 'Admin', tickets: data, confirm: true, fetch: async (url, request) =>
      request.method === 'DELETE' ? { ok: false, status: 404 } : null });
    const list = p.document.getElementById('admin-tickets-list');
    assert.equal(list.querySelector('.manager-ticket-title').textContent, `#${data[0].id} — ${attack}`);
    assertNoActiveContent(list);
    await list.querySelector('[data-delete-ticket]').emit('click');
    assert.ok(p.confirmations[0].includes(attack));
    assert.equal(p.calls.find(c => c.method === 'DELETE').url, 'http://localhost:5266/api/chamados/' + encodeURIComponent(data[0].id));
    assert.equal(p.run('globalThis.__THS_XSS'), undefined);
  }
});

test('GETs reais da API podem alimentar as paginas sem escrita no banco', { skip: !process.env.THS_API_READONLY_URL }, async () => {
  const base = process.env.THS_API_READONLY_URL;
  for (const [email, role, pageName, route] of [
    ['usuario@gmail.com', 'Usuario', 'home-usuario', '/api/chamados/meus'],
    ['tecnico@gmail.com', 'Tecnico', 'home-tecnico', '/api/chamados'],
    ['gestor@gmail.com', 'Admin', 'home-gestor', '/api/chamados']
  ]) {
    const login = await fetch(base + '/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, senha: 'Senha123' }) });
    assert.equal(login.status, 200); const session = await login.json();
    const get = async path => { const r = await fetch(base + path, { headers: { Authorization: 'Bearer ' + session.token } }); assert.equal(r.status, 200); return r.json(); };
    const data = await get(route); assert.ok(Array.isArray(data));
    const categories = await get('/api/catalogos/categorias'); const priorities = await get('/api/catalogos/prioridades'); const statuses = await get('/api/catalogos/status');
    const p = await page(pageName, { role, tickets: data, fetch: async url => {
      const pathname = new URL(url).pathname;
      const value = pathname === '/api/usuarios/me' ? await get(pathname) : pathname === '/api/catalogos/categorias' ? categories : pathname === '/api/catalogos/prioridades' ? priorities : pathname === '/api/catalogos/status' ? statuses : null;
      return value ? { ok: true, status: 200, text: async () => JSON.stringify(value) } : null;
    } });
    const listId = role === 'Usuario' ? 'tickets-list' : role === 'Tecnico' ? 'tech-ticket-list' : 'status-distribution-legend';
    assert.ok(p.document.getElementById(listId).children.length > 0);
    if (role === 'Usuario') {
      const detail = await get('/api/chamados/1'); const comments = await get('/api/chamados/1/comentarios');
      const thread = await page('comentarios', { fetch: async url => {
        const route = new URL(url).pathname;
        const value = route.endsWith('/comentarios') ? comments : route === '/api/chamados/1' ? detail : null;
        return value ? { ok: true, status: 200, text: async () => JSON.stringify(value) } : null;
      } });
      assert.equal(thread.document.getElementById('comments-list').children.length, comments.length);
    }
  }
});

// Opt-in separado: este teste cria e exclui somente seu proprio chamado temporario.
test('exclusao real: handler usa API existente e remove apenas fixture criada neste teste', { skip: !process.env.THS_API_TEMP_DELETE_URL }, async () => {
  const base = process.env.THS_API_TEMP_DELETE_URL;
  const login = async email => {
    const r = await fetch(base + '/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, senha: 'Senha123' }) });
    assert.equal(r.status, 200); return r.json();
  };
  const admin = await login('gestor@gmail.com'); const user = await login('usuario@gmail.com');
  assert.equal(admin.usuario.tipo, 'Admin');
  const request = (route, token, options = {}) => fetch(base + route, { ...options, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) } });
  const initial = await request('/api/chamados', admin.token); assert.equal(initial.status, 200);
  const before = await initial.json(); const marker = 'NP2_DELETE_FRONTEND_' + Date.now();
  let id; let removed = false;
  try {
    const created = await request('/api/chamados', user.token, { method: 'POST', body: JSON.stringify({ titulo: marker + ' <b>literal</b>', descricao: marker, categoriaId: 1, prioridadeId: 1 }) });
    assert.equal(created.status, 200); const ticket = await created.json();
    assert.ok(Number.isInteger(ticket.id) && ticket.id > 3 && !before.some(t => t.id === ticket.id));
    id = ticket.id;
    assert.equal((await request('/api/chamados/' + id, null, { method: 'DELETE' })).status, 401);
    assert.equal((await request('/api/chamados/' + id, user.token, { method: 'DELETE' })).status, 403);
    const p = await page('home-gestor', { role: 'Admin', confirm: true, fetch: async (url, options) => {
      const route = new URL(url).pathname;
      assert.ok(['/api/usuarios/me', '/api/chamados', '/api/chamados/' + id].includes(route));
      if (options.method === 'DELETE') assert.equal(route, '/api/chamados/' + id);
      const headers = new Headers(options.headers); headers.set('Authorization', 'Bearer ' + admin.token);
      const r = await fetch(base + route, { ...options, headers });
      if (options.method === 'DELETE' && r.status === 204) removed = true;
      return r;
    } });
    const button = p.document.querySelectorAll('[data-delete-ticket]').find(b => b.dataset.deleteTicket === String(id));
    assert.ok(button); await button.emit('click');
    assert.equal(removed, true);
    assert.equal(p.document.querySelectorAll('[data-delete-ticket]').length, before.length);
    assert.equal(p.document.getElementById('metric-total').textContent, String(before.length));
    assert.ok(p.document.getElementById('admin-tickets-message').textContent.includes('excluído com sucesso'));
    assert.equal((await request('/api/chamados/' + id, admin.token)).status, 404);
    const remaining = await request('/api/chamados', admin.token); assert.equal(remaining.status, 200);
    assert.deepEqual(await remaining.json(), before);
  } finally {
    if (id && !removed) assert.equal((await request('/api/chamados/' + id, admin.token, { method: 'DELETE' })).status, 204);
  }
});
