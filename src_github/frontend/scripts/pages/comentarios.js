const app = window.TechHelpApp;
const ticketId = new URLSearchParams(window.location.search).get("id");
const commentsMessage = document.getElementById("comments-message");
const commentsList = document.getElementById("comments-list");
const commentForm = document.getElementById("comment-form");
const commentInput = document.getElementById("comment-message");
const submitButton = document.getElementById("submit-button");
const formMessage = document.getElementById("form-message");
const headerHomeLink = document.getElementById("header-home-link");

let currentUser = null;

function getHomePath() {
  return currentUser?.role === "Tecnico" ? "./home-tecnico.html" : "./home-usuario.html";
}

function goBackHome() {
  window.location.href = getHomePath();
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

function roleLabel(role) {
  if (role === "Tecnico") {
    return "Técnico";
  }
  if (role === "Admin") {
    return "Gestor";
  }
  return "Usuário";
}

function showFormMessage(text, type) {
  formMessage.textContent = text;
  formMessage.className = `form-message ${type === "error" ? "form-message--error" : "form-message--success"}`;
}

function renderTicket(chamado) {
  document.getElementById("ticket-number").textContent = `Chamado #${chamado.id}`;
  document.getElementById("ticket-title").textContent = chamado.titulo || "Sem titulo";
  document.getElementById("ticket-description").textContent = chamado.descricao || "";
  document.getElementById("ticket-category").textContent = chamado.categoria?.nome || "-";
  document.getElementById("ticket-priority").textContent = chamado.prioridade?.nome || "-";
  document.getElementById("ticket-priority").className = getPriorityClasses(chamado.prioridade?.nome);
  document.getElementById("ticket-status").textContent = chamado.status?.nome || "-";
  document.getElementById("ticket-status").className = getStatusClasses(chamado.status?.nome);
}

function renderComments(comentarios) {
  if (!comentarios.length) {
    commentsMessage.textContent = "Nenhum comentário enviado ainda.";
    commentsMessage.classList.remove("hidden");
    commentsList.classList.add("hidden");
    return;
  }

  commentsMessage.classList.add("hidden");
  commentsList.classList.remove("hidden");
  commentsList.innerHTML = comentarios.map((comentario) => {
    const isMine = String(comentario.usuario?.id) === String(currentUser?.userId);
    return `
      <div class="comment-thread__item ${isMine ? "comment-thread__item--mine" : "comment-thread__item--other"}">
        <div class="comment-thread__bubble ${isMine ? "comment-thread__bubble--mine" : "comment-thread__bubble--other"}">
          <div class="comment-thread__meta ${isMine ? "comment-thread__meta--mine" : "comment-thread__meta--other"}">
            <span class="comment-thread__author">${comentario.usuario?.nome || "Usuário"} • ${roleLabel(comentario.usuario?.tipo)}</span>
            <span>${app.formatDate(comentario.dataEnvio)}</span>
          </div>
          <p class="comment-thread__text">${comentario.mensagem}</p>
        </div>
      </div>
    `;
  }).join("");
}

async function loadComments() {
  const response = await app.apiFetch(`/api/chamados/${ticketId}/comentarios`);
  const data = await app.readResponse(response);

  if (!response.ok) {
    throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar os comentarios.");
  }

  renderComments(Array.isArray(data) ? data : []);
}

async function loadTicket() {
  const response = await app.apiFetch(`/api/chamados/${ticketId}`);
  const data = await app.readResponse(response);

  if (!response.ok || !data) {
    throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar o chamado.");
  }

  renderTicket(data);
}

commentForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  formMessage.className = "form-message hidden";
  submitButton.disabled = true;
  submitButton.querySelector("span").textContent = "Enviando...";

  try {
    const response = await app.apiFetch(`/api/chamados/${ticketId}/comentarios`, {
      method: "POST",
      body: JSON.stringify({
        mensagem: commentInput.value.trim()
      })
    });

    const data = await app.readResponse(response);
    if (!response.ok) {
      throw new Error(typeof data === "string" ? data : "Nao foi possivel enviar o comentario.");
    }

    commentInput.value = "";
    showFormMessage("Comentário enviado com sucesso.", "success");
    await loadComments();
  } catch (error) {
    showFormMessage(error.message || "Erro ao enviar comentario.", "error");
  } finally {
    submitButton.disabled = false;
    submitButton.querySelector("span").textContent = "Enviar comentário";
  }
});

document.getElementById("back-button").addEventListener("click", goBackHome);

(async () => {
  if (!ticketId) {
    window.location.href = "./home-usuario.html";
    return;
  }

  const user = await app.requireAuth(["Usuario", "Tecnico"]);
  if (!user) {
    return;
  }

  currentUser = user;
  headerHomeLink.href = getHomePath();
  app.attachLogout();

  try {
    await Promise.all([loadTicket(), loadComments()]);
  } catch (error) {
    commentsMessage.textContent = error.message || "Erro ao carregar comentarios.";
    commentsMessage.classList.remove("hidden");
    commentsList.classList.add("hidden");
  }
})();
