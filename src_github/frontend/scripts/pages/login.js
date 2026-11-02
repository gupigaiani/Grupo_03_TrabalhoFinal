const app = window.TechHelpApp;
const form = document.getElementById("login-form");
const submitButton = document.getElementById("login-submit");
const message = document.getElementById("login-message");

function showMessage(text, type) {
  message.textContent = text;
  message.className = `form-message ${type === "error" ? "form-message--error" : "form-message--success"}`;
}

const existingUser = app.getStoredUser();
if (existingUser) {
  app.redirectToHome(existingUser.role);
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  message.className = "form-message hidden";
  submitButton.disabled = true;
  submitButton.textContent = "Entrando...";

  try {
    const response = await fetch(`${app.API_BASE_URL}/api/auth/login`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        email: document.getElementById("email").value.trim(),
        senha: document.getElementById("password").value
      })
    });

    const data = await app.readResponse(response);
    if (!response.ok || !data?.token || !data?.usuario) {
      throw new Error(typeof data === "string" ? data : "Nao foi possivel fazer login.");
    }

    app.saveSession(data.token, {
      userId: data.usuario.id,
      nome: data.usuario.nome,
      email: data.usuario.email,
      role: data.usuario.tipo
    });

    showMessage("Login realizado com sucesso. Redirecionando...", "success");
    app.redirectToHome(data.usuario.tipo);
  } catch (error) {
    showMessage(error.message || "Erro ao fazer login.", "error");
  } finally {
    submitButton.disabled = false;
    submitButton.textContent = "Entrar";
  }
});
