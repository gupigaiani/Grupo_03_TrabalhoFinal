const app = window.TechHelpApp;
const form = document.getElementById("ticket-form");
const categorySelect = document.getElementById("category");
const prioritySelect = document.getElementById("priority");
const submitButton = document.getElementById("submit-button");
const submitLabel = document.getElementById("submit-label");
const cancelButton = document.getElementById("cancel-button");
const backButton = document.getElementById("back-button");
const formMessage = document.getElementById("form-message");
const pageTitle = document.getElementById("page-title");
const headerHomeLink = document.getElementById("header-home-link");
const ticketId = new URLSearchParams(window.location.search).get("id");

let currentUser = null;

function getHomePath() {
  return currentUser?.role === "Tecnico" ? "./home-tecnico.html" : "./home-usuario.html";
}

function goBackHome() {
  window.location.href = getHomePath();
}

function showMessage(text, type) {
  formMessage.textContent = text;
  formMessage.className = `form-message ${type === "error" ? "form-message--error" : "form-message--success"}`;
}

function isOpenStatus(statusName) {
  return (statusName || "").trim().toLowerCase().includes("aberto");
}

function disableEditing(message) {
  showMessage(message, "error");
  submitButton.disabled = true;
  submitLabel.textContent = "Salvar Alterações";
  Array.from(form.elements).forEach((field) => {
    if (field instanceof HTMLElement && field !== backButton && field !== cancelButton) {
      field.disabled = true;
    }
  });
}

function fillSelect(select, items, placeholder) {
  if (!items.length) {
    select.innerHTML = `<option value="">${placeholder}</option>`;
    select.disabled = true;
    return;
  }

  select.disabled = false;
  select.innerHTML = `
    <option value="">Selecione</option>
    ${items.map((item) => `<option value="${item.id}">${item.nome}</option>`).join("")}
  `;
}

async function loadCatalogs() {
  const [categoriesResponse, prioritiesResponse] = await Promise.all([
    app.apiFetch("/api/catalogos/categorias"),
    app.apiFetch("/api/catalogos/prioridades")
  ]);

  const categories = await app.readResponse(categoriesResponse);
  const priorities = await app.readResponse(prioritiesResponse);

  if (!categoriesResponse.ok || !prioritiesResponse.ok) {
    throw new Error("Nao foi possivel carregar as opcoes do formulario.");
  }

  fillSelect(categorySelect, Array.isArray(categories) ? categories : [], "Nenhuma categoria disponivel");
  fillSelect(prioritySelect, Array.isArray(priorities) ? priorities : [], "Nenhuma prioridade disponivel");
}

async function loadTicketForEdit(id) {
  const response = await app.apiFetch(`/api/chamados/${id}`);
  const data = await app.readResponse(response);

  if (!response.ok || !data) {
    throw new Error(typeof data === "string" ? data : "Nao foi possivel carregar o chamado para edicao.");
  }

  if (!isOpenStatus(data.status?.nome)) {
    disableEditing("Somente chamados com status Aberto podem ser editados.");
    return;
  }

  document.getElementById("title").value = data.titulo || "";
  document.getElementById("description").value = data.descricao || "";
  categorySelect.value = String(data.categoriaId || "");
  prioritySelect.value = String(data.prioridadeId || "");
}

backButton.addEventListener("click", goBackHome);
cancelButton.addEventListener("click", goBackHome);

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  formMessage.className = "form-message hidden";
  submitButton.disabled = true;
  submitLabel.textContent = ticketId ? "Salvando..." : "Enviando...";

  try {
    const payload = {
      titulo: document.getElementById("title").value.trim(),
      descricao: document.getElementById("description").value.trim(),
      categoriaId: Number(categorySelect.value),
      prioridadeId: Number(prioritySelect.value)
    };

    const response = await app.apiFetch(ticketId ? `/api/chamados/${ticketId}` : "/api/chamados", {
      method: ticketId ? "PUT" : "POST",
      body: JSON.stringify(payload)
    });

    const data = await app.readResponse(response);
    if (!response.ok) {
      throw new Error(typeof data === "string" ? data : (ticketId ? "Nao foi possivel atualizar o chamado." : "Nao foi possivel abrir o chamado."));
    }

    showMessage(ticketId ? "Chamado atualizado com sucesso. Redirecionando..." : "Chamado aberto com sucesso. Redirecionando...", "success");
    setTimeout(goBackHome, 1200);
  } catch (error) {
    showMessage(error.message || (ticketId ? "Erro ao atualizar chamado." : "Erro ao abrir chamado."), "error");
  } finally {
    submitButton.disabled = false;
    submitLabel.textContent = ticketId ? "Salvar Alterações" : "Enviar";
  }
});

(async () => {
  const user = await app.requireAuth(["Usuario", "Tecnico"]);
  if (!user) {
    return;
  }

  currentUser = user;
  headerHomeLink.href = getHomePath();
  app.attachLogout();

  try {
    await loadCatalogs();

    if (!ticketId && user.role !== "Usuario") {
      pageTitle.textContent = "Abrir Chamado";
      showMessage("Somente usuarios podem abrir novos chamados.", "error");
      submitButton.disabled = true;
      return;
    }

    if (ticketId) {
      pageTitle.textContent = "Editar Chamado";
      submitLabel.textContent = "Salvar Alterações";
      await loadTicketForEdit(ticketId);
    }
  } catch (error) {
    showMessage(error.message || "Erro ao carregar o formulario.", "error");
  }
})();
