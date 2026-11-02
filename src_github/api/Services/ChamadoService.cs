using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Models;

namespace TechHelpSolutions.Services
{
    public class ChamadoService
    {
        private readonly AppDbContext _context;

        public ChamadoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Chamado> CriarChamado(CriarChamadoDTO dto)
        {
            var chamado = new Chamado
            {
                Titulo = dto.Titulo,
                Descricao = dto.Descricao,
                CategoriaId = dto.CategoriaId,
                PrioridadeId = dto.PrioridadeId,
                UsuarioId = dto.UsuarioId,
                StatusId = 1 // Aberto
            };

            _context.Chamados.Add(chamado);
            await _context.SaveChangesAsync();

            return chamado;
        }

        public async Task AtualizarStatus(int id, int statusId)
        {
            var chamado = await _context.Chamados.FindAsync(id);

            if (chamado == null)
                throw new Exception("Chamado não encontrado");

            chamado.StatusId = statusId;

            if (statusId == 3)
                chamado.DataFechamento = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task<Chamado> AtualizarChamado(int id, AtualizarChamadoDTO dto)
        {
            var chamado = await _context.Chamados.FindAsync(id);

            if (chamado == null)
                throw new Exception("Chamado não encontrado");

            chamado.Titulo = dto.Titulo;
            chamado.Descricao = dto.Descricao;
            chamado.CategoriaId = dto.CategoriaId;
            chamado.PrioridadeId = dto.PrioridadeId;

            await _context.SaveChangesAsync();

            return chamado;
        }

        public async Task<Comentario> CriarComentario(int chamadoId, int usuarioId, CriarComentarioDTO dto)
        {
            var chamado = await _context.Chamados.FindAsync(chamadoId);
            if (chamado == null)
                throw new Exception("Chamado não encontrado");

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null)
                throw new Exception("Usuário não encontrado");

            var comentario = new Comentario
            {
                ChamadoId = chamadoId,
                UsuarioId = usuarioId,
                Mensagem = dto.Mensagem
            };

            _context.Comentarios.Add(comentario);
            await _context.SaveChangesAsync();

            return comentario;
        }
    }
}
