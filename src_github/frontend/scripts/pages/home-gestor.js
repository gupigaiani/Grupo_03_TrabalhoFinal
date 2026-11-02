const app = window.TechHelpApp;
let canDeleteTickets = false;
let managerTickets = [];
let deletingTicket = false;
const SLA_HOURS_LIMIT = 24;
const STATUS_COLORS = [
  "rgb(37 99 235)",
  "rgb(16 185 129)",
  "rgb(245 158 11)",
  "rgb(239 68 68)",
  "rgb(139 92 246)",
  "rgb(20 184 166)"
];
const SECTOR_COLORS = [
  "rgb(30 64 175)",
  "rgb(59 130 246)",
  "rgb(14 116 144)",
  "rgb(79 70 229)",
  "rgb(147 51 234)",
  "rgb(5 150 105)"
];

function formatDurationFromMinutes(totalMinutes) {
  if (!totalMinutes || totalMinutes < 1) {
    return "0min";
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = Math.round(totalMinutes % 60);

  if (hours > 0 && minutes > 0) {
    return `${hours}h ${minutes}min`;
  }
  if (hours > 0) {
    return `${hours}h`;
  }
  return `${minutes}min`;
}

function setText(id, value) {
  document.getElementById(id).textContent = value;
}

function setWidth(id, value) {
  document.getElementById(id).style.width = `${value}%`;
}

function normalizeName(value, fallback) {
  return value && String(value).trim() ? String(value).trim() : fallback;
}

function getStartOfDay(date) {
  const normalized = new Date(date);
  normalized.setHours(0, 0, 0, 0);
  return normalized;
}

function formatShortDay(date) {
  return new Intl.DateTimeFormat("pt-BR", {
    weekday: "short",
    day: "2-digit"
  }).format(date);
}

function createPercent(value, total) {
  if (!total) {
    return 0;
  }

  return Math.round((value / total) * 100);
}

function showEmptyState(emptyId, contentId, message) {
  const emptyElement = document.getElementById(emptyId);
  emptyElement.textContent = message;
  emptyElement.classList.remove("hidden");

  if (contentId) {
    document.getElementById(contentId).classList.add("hidden");
  }
}

function showContentState(emptyId, contentId) {
  document.getElementById(emptyId).classList.add("hidden");
  document.getElementById(contentId).classList.remove("hidden");
}

function renderMetrics(chamados) {
  const total = chamados.length;
  const resolved = chamados.filter((chamado) => {
    const status = (chamado.status?.nome || "").toLowerCase();
    return status.includes("final") || status.includes("resolvido");
  });
  const openTickets = chamados.filter((chamado) => {
    const status = (chamado.status?.nome || "").toLowerCase();
    return !status.includes("final") && !status.includes("resolvido");
  });
  const urgent = chamados.filter((chamado) => (chamado.prioridade?.nome || "").toLowerCase().includes("alta"));

  const resolvedDurationsInMinutes = resolved
    .filter((chamado) => chamado.dataAbertura && chamado.dataFechamento)
    .map((chamado) => {
      const openedAt = new Date(chamado.dataAbertura).getTime();
      const closedAt = new Date(chamado.dataFechamento).getTime();
      return Math.max(0, (closedAt - openedAt) / 60000);
    });

  const avgResolutionMinutes = resolvedDurationsInMinutes.length
    ? resolvedDurationsInMinutes.reduce((sum, value) => sum + value, 0) / resolvedDurationsInMinutes.length
    : 0;

  const avgResponseMinutes = openTickets.length
    ? openTickets.reduce((sum, chamado) => {
        const openedAt = new Date(chamado.dataAbertura).getTime();
        const now = Date.now();
        return sum + Math.max(0, (now - openedAt) / 60000);
      }, 0) / openTickets.length
    : avgResolutionMinutes;

  const slaMet = resolved.filter((chamado) => {
    if (!chamado.dataAbertura || !chamado.dataFechamento) {
      return false;
    }

    const openedAt = new Date(chamado.dataAbertura).getTime();
    const closedAt = new Date(chamado.dataFechamento).getTime();
    const hoursToResolve = (closedAt - openedAt) / 3600000;
    return hoursToResolve <= SLA_HOURS_LIMIT;
  }).length;

  const slaRate = resolved.length ? Math.round((slaMet / resolved.length) * 100) : 0;
  const resolutionRate = total ? Math.round((resolved.length / total) * 100) : 0;
  const urgentRate = total ? Math.round((urgent.length / total) * 100) : 0;

  setText("metric-total", String(total));
  setText("metric-open", String(openTickets.length));
  setText("metric-avg-time", formatDurationFromMinutes(avgResolutionMinutes));
  setText("metric-sla", `${slaRate}%`);

  setText("resolution-rate-label", `${resolutionRate}%`);
  setWidth("resolution-rate-bar", resolutionRate);

  setText("urgent-rate-label", `${urgent.length} / ${total}`);
  setWidth("urgent-rate-bar", urgentRate);

  setText("response-time-label", formatDurationFromMinutes(avgResponseMinutes));
  setText("resolution-time-label", formatDurationFromMinutes(avgResolutionMinutes));
}

function renderTicketsByDay(chamados) {
  if (!chamados.length) {
    showEmptyState("tickets-by-day-empty", "tickets-by-day-chart", "Nenhum chamado encontrado no periodo.");
    return;
  }

  const today = getStartOfDay(new Date());
  const days = Array.from({ length: 7 }, (_, index) => {
    const date = new Date(today);
    date.setDate(today.getDate() - (6 - index));
    return {
      key: date.toISOString().slice(0, 10),
      date,
      count: 0
    };
  });

  const countsByDay = new Map(days.map((day) => [day.key, day]));
  chamados.forEach((chamado) => {
    const openedAt = new Date(chamado.dataAbertura);
    if (Number.isNaN(openedAt.getTime())) {
      return;
    }

    const key = getStartOfDay(openedAt).toISOString().slice(0, 10);
    const dayEntry = countsByDay.get(key);
    if (dayEntry) {
      dayEntry.count += 1;
    }
  });

  const maxCount = Math.max(...days.map((day) => day.count), 1);
  const barsContainer = document.getElementById("tickets-by-day-bars");
  barsContainer.replaceChildren(...days.map((day) => {
    const height = day.count ? Math.max((day.count / maxCount) * 100, 10) : 6;
    const card = document.createElement("div");
    card.className = "manager-bar-card";
    card.innerHTML = `
        <span class="manager-bar-value"></span>
        <div class="manager-bar-track">
          <div class="manager-bar-fill"></div>
        </div>
        <span class="manager-bar-label"></span>
    `;
    card.querySelector(".manager-bar-value").textContent = day.count;
    card.querySelector(".manager-bar-fill").style.height = `${height}%`;
    card.querySelector(".manager-bar-label").textContent = formatShortDay(day.date);
    return card;
  }));

  const totalLast7Days = days.reduce((sum, day) => sum + day.count, 0);
  const busiestDay = [...days].sort((left, right) => right.count - left.count)[0];
  setText(
    "tickets-by-day-legend",
    totalLast7Days
      ? `${totalLast7Days} chamados nos últimos 7 dias. Pico em ${formatShortDay(busiestDay.date)} com ${busiestDay.count}.`
      : "Sem chamados registrados nos últimos 7 dias."
  );

  showContentState("tickets-by-day-empty", "tickets-by-day-chart");
}

function renderTicketsBySector(chamados) {
  if (!chamados.length) {
    showEmptyState("tickets-by-sector-empty", "tickets-by-sector-list", "Nenhum chamado encontrado para distribuir por setor.");
    return;
  }

  const sectorMap = chamados.reduce((accumulator, chamado) => {
    const label = normalizeName(chamado.categoria?.nome, "Nao informado");
    accumulator.set(label, (accumulator.get(label) || 0) + 1);
    return accumulator;
  }, new Map());

  const sectors = [...sectorMap.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((left, right) => right.count - left.count);
  const maxCount = Math.max(...sectors.map((sector) => sector.count), 1);
  const list = document.getElementById("tickets-by-sector-list");

  list.replaceChildren(...sectors.map((sector, index) => {
    const percent = createPercent(sector.count, chamados.length);
    const width = Math.max((sector.count / maxCount) * 100, 8);
    const color = SECTOR_COLORS[index % SECTOR_COLORS.length];

    const row = document.createElement("div");
    row.className = "manager-row";
    row.innerHTML = `
        <div>
          <div class="manager-row-header">
            <span class="manager-row-label"></span>
            <span class="manager-row-meta"></span>
          </div>
          <div class="manager-row-track">
            <div class="manager-row-fill"></div>
          </div>
        </div>
    `;
    row.querySelector(".manager-row-label").textContent = sector.name;
    row.querySelector(".manager-row-meta").textContent = `${sector.count} chamado${sector.count === 1 ? "" : "s"} - ${percent}%`;
    const fill = row.querySelector(".manager-row-fill");
    fill.style.width = `${width}%`;
    fill.style.backgroundColor = color;
    return row;
  }));

  showContentState("tickets-by-sector-empty", "tickets-by-sector-list");
}

function renderStatusDistribution(chamados) {
  if (!chamados.length) {
    showEmptyState("status-distribution-empty", "status-distribution-content", "Nenhum chamado encontrado para distribuir por status.");
    return;
  }

  const statusMap = chamados.reduce((accumulator, chamado) => {
    const label = normalizeName(chamado.status?.nome, "Sem status");
    accumulator.set(label, (accumulator.get(label) || 0) + 1);
    return accumulator;
  }, new Map());

  const statuses = [...statusMap.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((left, right) => right.count - left.count);

  let currentAngle = 0;
  const gradientParts = statuses.map((status, index) => {
    const angle = (status.count / chamados.length) * 360;
    const start = currentAngle;
    const end = currentAngle + angle;
    currentAngle = end;
    return `${STATUS_COLORS[index % STATUS_COLORS.length]} ${start}deg ${end}deg`;
  });

  const chart = document.getElementById("status-distribution-chart");
  chart.style.background = `conic-gradient(${gradientParts.join(", ")})`;
  chart.innerHTML = `
    <div class="manager-donut-center">
      <span class="manager-donut-total"></span>
      <span class="manager-donut-subtitle">chamados</span>
    </div>
  `;
  chart.querySelector(".manager-donut-total").textContent = chamados.length;

  const legend = document.getElementById("status-distribution-legend");
  legend.replaceChildren(...statuses.map((status, index) => {
    const color = STATUS_COLORS[index % STATUS_COLORS.length];
    const percent = createPercent(status.count, chamados.length);

    const item = document.createElement("div");
    item.className = "manager-legend-item";
    item.innerHTML = `
        <div class="manager-legend-label">
          <span class="manager-legend-swatch"></span>
          <span class="manager-legend-name"></span>
        </div>
        <span class="manager-legend-value"></span>
    `;
    item.querySelector(".manager-legend-swatch").style.backgroundColor = color;
    item.querySelector(".manager-legend-name").textContent = status.name;
    item.querySelector(".manager-legend-value").textContent = `${status.count} - ${percent}%`;
    return item;
  }));

  showContentState("status-distribution-empty", "status-distribution-content");
}

function renderDashboard(chamados) {
  renderMetrics(chamados);
  renderTicketsByDay(chamados);
  renderTicketsBySector(chamados);
  renderStatusDistribution(chamados);
}

function showDeleteMessage(message, isError = false) {
  const element = document.getElementById("admin-tickets-message");
  element.textContent = message;
  element.className = `form-message ${isError ? "form-message--error" : "form-message--success"}`;
}

function renderDeletableTickets() {
  const panel = document.getElementById("admin-tickets-panel");
  const list = document.getElementById("admin-tickets-list");
  list.replaceChildren();
  if (!canDeleteTickets) {
    panel.classList.add("hidden");
    return;
  }

  panel.classList.remove("hidden");
  document.getElementById("admin-tickets-empty").classList[managerTickets.length ? "add" : "remove"]("hidden");
  managerTickets.forEach((chamado) => {
    const row = document.createElement("article");
    row.className = "manager-ticket-row";
    const summary = document.createElement("div");
    summary.className = "manager-ticket-summary";
    const title = document.createElement("h3");
    title.className = "manager-ticket-title";
    title.textContent = `#${chamado.id} — ${chamado.titulo || "Sem título"}`;
    const meta = document.createElement("p");
    meta.className = "manager-ticket-meta";
    meta.textContent = `${chamado.status?.nome || "Sem status"} • ${app.formatDate(chamado.dataAbertura)}`;
    summary.append(title, meta);
    const button = document.createElement("button");
    button.setAttribute("type", "button");
    button.className = "button button--danger-text";
    button.setAttribute("data-delete-ticket", String(chamado.id));
    button.setAttribute("aria-label", `Excluir chamado #${chamado.id}: ${chamado.titulo || "Sem título"}`);
    button.textContent = "Excluir";
    button.disabled = deletingTicket;
    button.addEventListener("click", () => deleteTicket(chamado));
    row.append(summary, button);
    list.append(row);
  });
}

function deleteErrorMessage(status) {
  if (status === 401) return "Sessão expirada. Faça login novamente.";
  if (status === 403) return "Você não tem permissão para excluir chamados.";
  if (status === 404) return "Chamado não encontrado. Recarregue a página para atualizar a lista.";
  return "Não foi possível excluir o chamado. Tente novamente mais tarde.";
}

async function deleteTicket(chamado) {
  if (!canDeleteTickets || deletingTicket) return;
  if (!window.confirm(`Excluir o chamado #${chamado.id}: "${chamado.titulo || "Sem título"}"? Esta ação não pode ser desfeita.`)) return;

  deletingTicket = true;
  renderDeletableTickets();
  try {
    const response = await app.apiFetch(`/api/chamados/${encodeURIComponent(String(chamado.id))}`, { method: "DELETE" });
    if (!response.ok) {
      showDeleteMessage(deleteErrorMessage(response.status), true);
      return;
    }

    // Atualiza localmente mesmo se a consulta de sincronizacao posterior falhar.
    managerTickets = managerTickets.filter((ticket) => ticket.id !== chamado.id);
    renderDashboard(managerTickets);
    renderDeletableTickets();
    try {
      await loadManagerDashboard();
      showDeleteMessage(`Chamado #${chamado.id} excluído com sucesso.`);
    } catch {
      showDeleteMessage(`Chamado #${chamado.id} excluído. Não foi possível sincronizar os indicadores com o servidor. Recarregue a página.`, true);
    }
  } catch {
    // apiFetch ja limpa a sessao e redireciona em 401. Nunca exibe o corpo SQL/HTTP.
    showDeleteMessage(app.getStoredUser()
      ? "Não foi possível confirmar a exclusão. Verifique sua conexão e recarregue a página antes de tentar novamente."
      : deleteErrorMessage(401), true);
  } finally {
    deletingTicket = false;
    renderDeletableTickets();
  }
}

async function loadManagerDashboard() {
  const response = await app.apiFetch("/api/chamados");
  const data = await app.readResponse(response);

  if (!response.ok) {
    throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar os indicadores.");
  }

  const chamados = Array.isArray(data) ? data : [];
  managerTickets = chamados;
  renderDashboard(chamados);
  renderDeletableTickets();
  setText("manager-message", `Indicadores carregados com ${chamados.length} chamados.`);
}

(async () => {
  const user = await app.requireAuth(["Admin", "Gestor"]);
  if (!user) {
    return;
  }

  // requireAuth normaliza Gestor para navegacao; DELETE exige o perfil original Admin.
  canDeleteTickets = app.getStoredUser()?.role === "Admin";
  app.attachLogout();

  try {
    await loadManagerDashboard();
  } catch (error) {
    setText("manager-message", error.message || "Erro ao carregar o dashboard do gestor.");
    showEmptyState("tickets-by-day-empty", "tickets-by-day-chart", "Nao foi possivel carregar os chamados por dia.");
    showEmptyState("tickets-by-sector-empty", "tickets-by-sector-list", "Nao foi possivel carregar os chamados por setor.");
    showEmptyState("status-distribution-empty", "status-distribution-content", "Nao foi possivel carregar a distribuição por status.");
  }
})();
