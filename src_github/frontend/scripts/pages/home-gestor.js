const app = window.TechHelpApp;
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
  barsContainer.innerHTML = days.map((day) => {
    const height = day.count ? Math.max((day.count / maxCount) * 100, 10) : 6;
    return `
      <div class="manager-bar-card">
        <span class="manager-bar-value">${day.count}</span>
        <div class="manager-bar-track">
          <div class="manager-bar-fill" style="height: ${height}%"></div>
        </div>
        <span class="manager-bar-label">${formatShortDay(day.date)}</span>
      </div>
    `;
  }).join("");

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

  list.innerHTML = sectors.map((sector, index) => {
    const percent = createPercent(sector.count, chamados.length);
    const width = Math.max((sector.count / maxCount) * 100, 8);
    const color = SECTOR_COLORS[index % SECTOR_COLORS.length];

    return `
      <div class="manager-row">
        <div>
          <div class="manager-row-header">
            <span class="manager-row-label">${sector.name}</span>
            <span class="manager-row-meta">${sector.count} chamado${sector.count === 1 ? "" : "s"} - ${percent}%</span>
          </div>
          <div class="manager-row-track">
            <div class="manager-row-fill" style="width: ${width}%; background-color: ${color};"></div>
          </div>
        </div>
      </div>
    `;
  }).join("");

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
      <span class="manager-donut-total">${chamados.length}</span>
      <span class="manager-donut-subtitle">chamados</span>
    </div>
  `;

  const legend = document.getElementById("status-distribution-legend");
  legend.innerHTML = statuses.map((status, index) => {
    const color = STATUS_COLORS[index % STATUS_COLORS.length];
    const percent = createPercent(status.count, chamados.length);

    return `
      <div class="manager-legend-item">
        <div class="manager-legend-label">
          <span class="manager-legend-swatch" style="background-color: ${color};"></span>
          <span class="manager-legend-name">${status.name}</span>
        </div>
        <span class="manager-legend-value">${status.count} - ${percent}%</span>
      </div>
    `;
  }).join("");

  showContentState("status-distribution-empty", "status-distribution-content");
}

function renderDashboard(chamados) {
  renderMetrics(chamados);
  renderTicketsByDay(chamados);
  renderTicketsBySector(chamados);
  renderStatusDistribution(chamados);
}

async function loadManagerDashboard() {
  const response = await app.apiFetch("/api/chamados");
  const data = await app.readResponse(response);

  if (!response.ok) {
    throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar os indicadores.");
  }

  const chamados = Array.isArray(data) ? data : [];
  renderDashboard(chamados);
  setText("manager-message", `Indicadores carregados com ${chamados.length} chamados.`);
}

(async () => {
  const user = await app.requireAuth(["Admin", "Gestor"]);
  if (!user) {
    return;
  }

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
