window.TechHelpApp = (() => {
  const API_BASE_URL = "http://localhost:5266";
  const STORAGE_TOKEN_KEY = "techhelp_token";
  const STORAGE_USER_KEY = "techhelp_user";

  function getToken() {
    return localStorage.getItem(STORAGE_TOKEN_KEY) || "";
  }

  function getStoredUser() {
    try {
      const raw = localStorage.getItem(STORAGE_USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  function saveSession(token, usuario) {
    localStorage.setItem(STORAGE_TOKEN_KEY, token);
    localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(usuario));
  }

  function clearSession() {
    localStorage.removeItem(STORAGE_TOKEN_KEY);
    localStorage.removeItem(STORAGE_USER_KEY);
  }

  function normalizeRole(role) {
    if (!role) {
      return "";
    }

    return role === "Gestor" ? "Admin" : role;
  }

  function getHomePageByRole(role) {
    const normalizedRole = normalizeRole(role);

    if (normalizedRole === "Tecnico") {
      return "./home-tecnico.html";
    }

    if (normalizedRole === "Admin") {
      return "./home-gestor.html";
    }

    return "./home-usuario.html";
  }

  function redirectToHome(role) {
    window.location.href = getHomePageByRole(role);
  }

  function redirectToLogin() {
    window.location.href = "./login.html";
  }

  function getInitials(name) {
    if (!name) {
      return "TH";
    }

    return name
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() || "")
      .join("");
  }

  function formatDate(value) {
    if (!value) {
      return "-";
    }

    return new Intl.DateTimeFormat("pt-BR", {
      dateStyle: "short",
      timeStyle: "short"
    }).format(new Date(value));
  }

  function applyUserToHeader(user) {
    document.querySelectorAll("[data-user-name]").forEach((element) => {
      element.textContent = user?.nome || "Usuario";
    });

    document.querySelectorAll("[data-user-email]").forEach((element) => {
      element.textContent = user?.email || "";
    });

    document.querySelectorAll("[data-user-role]").forEach((element) => {
      element.textContent = normalizeRole(user?.role) || "Usuario";
    });

    document.querySelectorAll("[data-user-initials]").forEach((element) => {
      element.textContent = getInitials(user?.nome);
    });
  }

  async function apiFetch(path, options = {}) {
    const headers = new Headers(options.headers || {});
    const token = getToken();

    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    if (!headers.has("Content-Type") && options.body && !(options.body instanceof FormData)) {
      headers.set("Content-Type", "application/json");
    }

    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers
    });

    if (response.status === 401) {
      clearSession();
      redirectToLogin();
      throw new Error("Sessao expirada. Faca login novamente.");
    }

    return response;
  }

  async function readResponse(response) {
    const rawText = await response.text();

    try {
      return rawText ? JSON.parse(rawText) : null;
    } catch {
      return rawText;
    }
  }

  async function loadCurrentUser() {
    const storedUser = getStoredUser();
    if (!getToken()) {
      return storedUser;
    }

    try {
      const response = await apiFetch("/api/usuarios/me");
      if (!response.ok) {
        return storedUser;
      }

      const usuario = await readResponse(response);
      const mergedUser = {
        userId: usuario.id,
        nome: usuario.nome,
        email: usuario.email,
        role: usuario.tipo
      };

      saveSession(getToken(), mergedUser);
      return mergedUser;
    } catch {
      return storedUser;
    }
  }

  async function requireAuth(expectedRoles = []) {
    if (!getToken()) {
      redirectToLogin();
      return null;
    }

    const user = await loadCurrentUser();
    if (!user) {
      clearSession();
      redirectToLogin();
      return null;
    }

    const normalizedRole = normalizeRole(user.role);
    if (expectedRoles.length > 0 && !expectedRoles.map(normalizeRole).includes(normalizedRole)) {
      redirectToHome(normalizedRole);
      return null;
    }

    applyUserToHeader({ ...user, role: normalizedRole });
    return { ...user, role: normalizedRole };
  }

  function attachLogout(selector = "[data-logout]") {
    document.querySelectorAll(selector).forEach((button) => {
      button.addEventListener("click", () => {
        clearSession();
        redirectToLogin();
      });
    });
  }

  return {
    API_BASE_URL,
    apiFetch,
    applyUserToHeader,
    attachLogout,
    clearSession,
    formatDate,
    getHomePageByRole,
    getInitials,
    getStoredUser,
    loadCurrentUser,
    normalizeRole,
    readResponse,
    redirectToHome,
    redirectToLogin,
    requireAuth,
    saveSession
  };
})();
