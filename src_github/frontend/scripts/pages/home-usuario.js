const app = window.TechHelpApp;
const ticketsMessage = document.getElementById("tickets-message");
const ticketsList = document.getElementById("tickets-list");
const ticketsTotal = document.getElementById("tickets-total");
const countOpen = document.getElementById("count-open");
const countProgress = document.getElementById("count-progress");
const countClosed = document.getElementById("count-closed");

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

function getPriorityClasses(priorityName) {
  const normalized = (priorityName || "").toLowerCase();
  if (normalized.includes("alta")) {
    return "badge priority-badge--high";
  }

  if (normalized.includes("media") || normalized.includes("média")) {
    return "badge priority-badge--medium";
  }

  return "badge priority-badge--low";
}

function isOpenStatus(statusName) {
  return (statusName || "").trim().toLowerCase().includes("aberto");
}

function getStatusIcon(statusName) {
  const normalized = (statusName || "").toLowerCase();
  if (normalized.includes("final") || normalized.includes("resolvido")) {
    return `
      <svg width="20" height="20" class="ticket-item__icon ticket-item__icon--closed" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path d="M21.801 10A10 10 0 1 1 17 3.335"></path>
        <path d="m9 11 3 3L22 4"></path>
      </svg>
    `;
  }

  if (normalized.includes("andamento")) {
    return `
      <svg width="20" height="20" class="ticket-item__icon ticket-item__icon--progress" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <circle cx="12" cy="12" r="10"></circle>
        <polyline points="12 6 12 12 16 14"></polyline>
      </svg>
    `;
  }

  return `
    <svg width="20" height="20" class="ticket-item__icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <circle cx="12" cy="12" r="10"></circle>
      <path d="M12 8v4"></path>
      <path d="M12 16h.01"></path>
    </svg>
  `;
}

function renderTickets(chamados) {
  ticketsTotal.textContent = `${chamados.length} ${chamados.length === 1 ? "item" : "itens"}`;

  if (!chamados.length) {
    ticketsMessage.textContent = "Você ainda não abriu nenhum chamado.";
    ticketsMessage.classList.remove("hidden");
    ticketsList.classList.add("hidden");
    return;
  }

  ticketsMessage.classList.add("hidden");
  ticketsList.classList.remove("hidden");
  ticketsList.innerHTML = chamados.map((chamado) => {
    const canEdit = isOpenStatus(chamado.status?.nome);
    const editClasses = canEdit
      ? "button button--secondary user-ticket-button"
      : "button button--disabled user-ticket-button";
    const editTitle = canEdit
      ? "Editar chamado"
      : "Somente chamados com status Aberto podem ser editados";

    return `
      <article class="ticket-item">
        <div class="ticket-item__layout">
          <div>${getStatusIcon(chamado.status?.nome)}</div>
          <div class="ticket-item__content">
            <div class="ticket-item__headline">
              <span class="ticket-item__number">#${chamado.id}</span>
              <h3 class="ticket-item__title">${chamado.titulo}</h3>
            </div>
            <p class="ticket-item__description">${chamado.descricao}</p>
            <div class="ticket-item__footer">
              <div class="ticket-item__meta">
                <span class="badge badge--neutral">${chamado.categoria?.nome || "-"}</span>
                <span class="${getPriorityClasses(chamado.prioridade?.nome)}">${chamado.prioridade?.nome || "-"}</span>
                <span>Criado em: ${app.formatDate(chamado.dataAbertura)}</span>
              </div>
              <div class="ticket-item__actions">
                <span class="${getStatusClasses(chamado.status?.nome)}">${chamado.status?.nome || "Sem status"}</span>
                <a href="./comentarios.html?id=${chamado.id}" class="button button--soft user-ticket-button">
                  <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 17a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 21.286V5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2z"/></svg>
                  <span>Abrir Comentários</span>
                </a>
                <a href="./abrir-chamado.html?id=${chamado.id}" class="${editClasses}" ${canEdit ? "" : "aria-disabled=\"true\" tabindex=\"-1\""} title="${editTitle}">
                  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                    <path d="M18.375 2.625a1 1 0 0 1 3 3l-9.013 9.014a2 2 0 0 1-.853.505l-2.873.84a.5.5 0 0 1-.62-.62l.84-2.873a2 2 0 0 1 .506-.852z" />
                  </svg>
                  <span>Editar Chamado</span>
                </a>
              </div>
            </div>
          </div>
        </div>
      </article>
    `;
  }).join("");

  const countByStatus = chamados.reduce((acc, chamado) => {
    const normalized = (chamado.status?.nome || "").toLowerCase();
    if (normalized.includes("andamento")) {
      acc.progress += 1;
    } else if (normalized.includes("final") || normalized.includes("resolvido")) {
      acc.closed += 1;
    } else {
      acc.open += 1;
    }
    return acc;
  }, { open: 0, progress: 0, closed: 0 });

  countOpen.textContent = countByStatus.open;
  countProgress.textContent = countByStatus.progress;
  countClosed.textContent = countByStatus.closed;
}

async function loadTickets() {
  try {
    const response = await app.apiFetch("/api/chamados/meus");
    const data = await app.readResponse(response);
    if (!response.ok) {
      throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar os chamados.");
    }

    renderTickets(Array.isArray(data) ? data : []);
  } catch (error) {
    ticketsMessage.textContent = error.message || "Erro ao carregar chamados.";
    ticketsMessage.classList.remove("hidden");
    ticketsList.classList.add("hidden");
  }
}

(async () => {
  const user = await app.requireAuth(["Usuario"]);
  if (!user) {
    return;
  }

  app.attachLogout();
  await loadTickets();
})();
