const app = window.TechHelpApp;
const priorityFilter = document.getElementById("priority-filter");
const statusFilter = document.getElementById("status-filter");
const techMessage = document.getElementById("tech-message");
const techTicketList = document.getElementById("tech-ticket-list");
const techTotal = document.getElementById("tech-total");

let prioritiesMap = new Map();
let statusMap = new Map();

function getPriorityClasses(priorityName) {
  const normalized = (priorityName || "").toLowerCase();
  if (normalized.includes("alta")) {
    return "badge priority-badge--high";
  }
  if (normalized.includes("média") || normalized.includes("media")) {
    return "badge priority-badge--medium";
  }
  return "badge priority-badge--low";
}

function isOpenStatus(statusName) {
  return (statusName || "").trim().toLowerCase().includes("aberto");
}

function getStatusClasses(statusName) {
  const normalized = (statusName || "").toLowerCase();
  if (normalized.includes("andamento")) {
    return "badge status-badge--progress";
  }
  if (normalized.includes("final") || normalized.includes("resolvido")) {
    return "badge status-badge--closed";
  }
  return "badge status-badge--open";
}

function showMessage(text, isError = false) {
  techMessage.textContent = text;
  techMessage.className = isError
    ? "list-panel__message form-message form-message--error"
    : "list-panel__message";
  techMessage.classList.remove("hidden");
  techTicketList.classList.add("hidden");
}

function fillSelect(select, items, defaultLabel) {
  select.replaceChildren();
  const placeholder = document.createElement("option");
  placeholder.value = "";
  placeholder.textContent = defaultLabel;
  select.append(placeholder);
  items.forEach((item) => {
    const option = document.createElement("option");
    option.value = String(item.id);
    option.textContent = item.nome;
    select.append(option);
  });
}

async function loadFilters() {
  const [prioritiesResponse, statusResponse] = await Promise.all([
    app.apiFetch("/api/catalogos/prioridades"),
    app.apiFetch("/api/catalogos/status")
  ]);

  const priorities = await app.readResponse(prioritiesResponse);
  const statuses = await app.readResponse(statusResponse);

  if (!prioritiesResponse.ok || !statusResponse.ok) {
    throw new Error("Nao foi possivel carregar os filtros.");
  }

  prioritiesMap = new Map((priorities || []).map((item) => [String(item.id), item.nome]));
  statusMap = new Map((statuses || []).map((item) => [String(item.id), item.nome]));

  fillSelect(priorityFilter, priorities || [], "Todas");
  fillSelect(statusFilter, statuses || [], "Todos");
}

function renderTickets(chamados) {
  techTotal.textContent = `${chamados.length} ${chamados.length === 1 ? "item" : "itens"}`;

  if (!chamados.length) {
    showMessage("Nenhum chamado encontrado para os filtros selecionados.");
    return;
  }

  techMessage.classList.add("hidden");
  techTicketList.classList.remove("hidden");
  techTicketList.replaceChildren(...chamados.map((chamado) => {
    const canEdit = isOpenStatus(chamado.status?.nome);
    const article = document.createElement("article");
    article.className = "tech-ticket-item";
    // Template estatico; dados da API nunca sao inseridos como HTML.
    article.innerHTML = `
        <div class="tech-ticket-item__content">
          <div class="tech-ticket-item__meta">
            <span class="tech-ticket-item__number"></span>
            <span data-priority></span>
            <span class="tech-ticket-item__category"></span>
            <span data-status></span>
          </div>

          <h3 class="tech-ticket-item__title"></h3>
          <p class="tech-ticket-item__description"></p>

          <div class="tech-ticket-item__details">
            <span></span>
            <span></span>
            <span></span>
          </div>
        </div>

        <div class="tech-ticket-item__actions">
          <a data-comments class="button button--soft">
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 17a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 21.286V5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2z"/></svg>
            <span>Abrir Comentários</span>
          </a>
          <button data-action="assumir"  class="button button--primary" >
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21.801 10A10 10 0 1 1 17 3.335"></path>
              <path d="m9 11 3 3L22 4"></path>
            </svg>
            <span></span>
          </button>
          <button data-action="editar"   class="button" >
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
              <path d="M18.375 2.625a1 1 0 0 1 3 3l-9.013 9.014a2 2 0 0 1-.853.505l-2.873.84a.5.5 0 0 1-.62-.62l.84-2.873a2 2 0 0 1 .506-.852z" />
            </svg>
            <span>Editar</span>
          </button>
          <button data-action="finalizar"  class="button button--success" >
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21.801 10A10 10 0 1 1 17 3.335"></path>
              <path d="m9 11 3 3L22 4"></path>
            </svg>
            <span>Finalizar</span>
          </button>
        </div>
    `;
    article.querySelector(".tech-ticket-item__number").textContent = `#${chamado.id}`;
    const priority = article.querySelector("[data-priority]");
    priority.className = getPriorityClasses(chamado.prioridade?.nome);
    priority.textContent = chamado.prioridade?.nome || "-";
    article.querySelector(".tech-ticket-item__category").textContent = chamado.categoria?.nome || "-";
    const status = article.querySelector("[data-status]");
    status.className = getStatusClasses(chamado.status?.nome);
    status.textContent = chamado.status?.nome || "Sem status";
    article.querySelector(".tech-ticket-item__title").textContent = chamado.titulo;
    article.querySelector(".tech-ticket-item__description").textContent = chamado.descricao;
    const details = article.querySelector(".tech-ticket-item__details").children;
    details[0].textContent = `Criado: ${app.formatDate(chamado.dataAbertura)}`;
    details[1].textContent = `Solicitante: ${chamado.usuario?.nome || "-"}`;
    details[2].textContent = `Tecnico: ${chamado.tecnico?.nome || "Não atribuido"}`;
    article.querySelector("[data-comments]").href = `./comentarios.html?id=${encodeURIComponent(String(chamado.id))}`;
    const assume = article.querySelector('[data-action="assumir"]');
    assume.disabled = Boolean(chamado.tecnicoId);
    assume.querySelector("span").textContent = chamado.tecnicoId ? "Assumido" : "Assumir";
    const edit = article.querySelector('[data-action="editar"]');
    edit.disabled = !canEdit;
    edit.className = `button ${canEdit ? "button--secondary" : "button--disabled"}`;
    edit.title = canEdit ? "Editar chamado" : "Somente chamados com status Aberto podem ser editados";
    const statusName = (chamado.status?.nome || "").toLowerCase();
    article.querySelector('[data-action="finalizar"]').disabled = !chamado.tecnicoId || statusName.includes("final") || statusName.includes("resolvido");
    article.querySelectorAll("button[data-action]").forEach((button) => {
      button.dataset.id = String(chamado.id);
    });
    return article;
  }));
}

async function loadTickets() {
  const params = new URLSearchParams();
  if (priorityFilter.value) {
    params.set("prioridade", priorityFilter.value);
  }
  if (statusFilter.value) {
    params.set("status", statusFilter.value);
  }

  const path = params.toString() ? `/api/chamados?${params.toString()}` : "/api/chamados";
  const response = await app.apiFetch(path);
  const data = await app.readResponse(response);

  if (!response.ok) {
    throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar os chamados.");
  }

  renderTickets(Array.isArray(data) ? data : []);
}

async function handleAction(action, chamadoId) {
  if (action === "editar") {
    window.location.href = `./abrir-chamado.html?id=${encodeURIComponent(chamadoId)}`;
    return;
  }

  const endpoint = action === "assumir"
    ? `/api/chamados/${encodeURIComponent(chamadoId)}/assumir`
    : `/api/chamados/${encodeURIComponent(chamadoId)}/finalizar`;

  const response = await app.apiFetch(endpoint, { method: "PUT" });
  if (!response.ok) {
    const data = await app.readResponse(response);
    throw new Error(typeof data === "string" ? data : "Nao foi possivel executar a acao.");
  }
}

priorityFilter.addEventListener("change", async () => {
  try {
    await loadTickets();
  } catch (error) {
    showMessage(error.message || "Erro ao filtrar chamados.", true);
  }
});

statusFilter.addEventListener("change", async () => {
  try {
    await loadTickets();
  } catch (error) {
    showMessage(error.message || "Erro ao filtrar chamados.", true);
  }
});

techTicketList.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) {
    return;
  }

  const action = button.dataset.action;
  const chamadoId = button.dataset.id;
  const originalLabel = button.textContent.trim();

  if (action === "editar") {
    await handleAction(action, chamadoId);
    return;
  }

  try {
    button.disabled = true;
    button.textContent = "Processando...";
    await handleAction(action, chamadoId);
    await loadTickets();
  } catch (error) {
    alert(error.message || "Erro ao processar a acao.");
    button.disabled = false;
    button.textContent = originalLabel;
  }
});

(async () => {
  const user = await app.requireAuth(["Tecnico"]);
  if (!user) {
    return;
  }

  app.attachLogout();

  try {
    await loadFilters();
    await loadTickets();
  } catch (error) {
    showMessage(error.message || "Erro ao carregar o dashboard do tecnico.", true);
  }
})();
