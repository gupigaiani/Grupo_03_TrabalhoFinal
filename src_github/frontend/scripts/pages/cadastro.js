const API_BASE_URL = "http://localhost:5266";

const form = document.getElementById("register-form");
const submitButton = document.getElementById("register-submit");
const message = document.getElementById("register-message");

function showMessage(text, type) {
  message.textContent = text;
  message.className = `form-message ${type === "error" ? "form-message--error" : "form-message--success"}`;
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  message.className = "form-message hidden";

  const nome = document.getElementById("nome").value.trim();
  const email = document.getElementById("email").value.trim();
  const tipo = document.getElementById("tipo").value;
  const senha = document.getElementById("senha").value;
  const confirmarSenha = document.getElementById("confirmar-senha").value;

  if (senha !== confirmarSenha) {
    showMessage("As senhas não conferem.", "error");
    return;
  }

  submitButton.disabled = true;
  submitButton.textContent = "Cadastrando...";

  try {
    const response = await fetch(`${API_BASE_URL}/api/usuarios`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ nome, email, senha, tipo })
    });

    const rawText = await response.text();

    if (!response.ok) {
      throw new Error(rawText || "Não foi possível concluir o cadastro.");
    }

    showMessage("Cadastro realizado com sucesso. Você será redirecionado para o login.", "success");
    form.reset();

    setTimeout(() => {
      window.location.href = "./login.html";
    }, 1500);
  } catch (error) {
    showMessage(error.message || "Erro ao cadastrar usuário.", "error");
  } finally {
    submitButton.disabled = false;
    submitButton.textContent = "Cadastrar";
  }
});
