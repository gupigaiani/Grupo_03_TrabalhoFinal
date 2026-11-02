using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using TechHelpSolutions.Data;
using TechHelpSolutions.DTOs;
using TechHelpSolutions.Models;

namespace TechHelpSolutions.Services
{
    // Numeros de erro constituem o contrato interno com 04_procedures.sql.
    public enum ResultadoOperacaoChamado
    {
        Sucesso = 0,
        NaoEncontrado = 51101,
        TecnicoInvalido = 51102,
        SemResponsavel = 51103,
        OutroResponsavel = 51104,
        Conflito = 51105,
        ParametroInvalido = 51106
    }

    public class ChamadoService
    {
        private readonly AppDbContext _context;

        public ChamadoService(AppDbContext context)
        {
            _context = context;
        }

        // A projecao fica em IQueryable: filtros e ordenacao continuam executados no SQL.
        public IQueryable<ChamadoResponseDTO> ConsultarChamadosDetalhados()
            => _context.ChamadosDetalhados.Select(c => new ChamadoResponseDTO
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Descricao = c.Descricao,
                UsuarioId = c.UsuarioId,
                TecnicoId = c.TecnicoId,
                CategoriaId = c.CategoriaId,
                PrioridadeId = c.PrioridadeId,
                StatusId = c.StatusId,
                DataAbertura = c.DataAbertura,
                DataFechamento = c.DataFechamento,
                Usuario = new UsuarioResumoDTO
                {
                    Id = c.UsuarioId, Nome = c.UsuarioNome, Email = c.UsuarioEmail, Tipo = c.UsuarioTipo
                },
                Tecnico = c.TecnicoId == null ? null : new UsuarioResumoDTO
                {
                    Id = c.TecnicoId.Value, Nome = c.TecnicoNome!, Email = c.TecnicoEmail!, Tipo = c.TecnicoTipo!
                },
                Categoria = new ItemCatalogoDTO { Id = c.CategoriaId, Nome = c.CategoriaNome },
                Prioridade = new ItemCatalogoDTO { Id = c.PrioridadeId, Nome = c.PrioridadeNome },
                Status = new ItemCatalogoDTO { Id = c.StatusId, Nome = c.StatusNome },
                Comentarios = null
            });

        public IQueryable<ComentarioResponseDTO> ConsultarComentariosDetalhados()
            => _context.ComentariosDetalhados.Select(c => new ComentarioResponseDTO
            {
                Id = c.Id,
                ChamadoId = c.ChamadoId,
                Mensagem = c.Mensagem,
                DataEnvio = c.DataEnvio,
                Usuario = new UsuarioResumoDTO
                {
                    Id = c.UsuarioId, Nome = c.UsuarioNome, Email = c.UsuarioEmail, Tipo = c.UsuarioTipo
                }
            });

        public Task<ResultadoOperacaoChamado> AssumirChamado(Chamado estadoEsperado, int tecnicoId)
            => ExecutarOperacaoChamado(estadoEsperado, tecnicoId, finalizar: false);

        public Task<ResultadoOperacaoChamado> FinalizarChamado(Chamado estadoEsperado, int tecnicoId)
            => ExecutarOperacaoChamado(estadoEsperado, tecnicoId, finalizar: true);

        private async Task<ResultadoOperacaoChamado> ExecutarOperacaoChamado(
            Chamado estadoEsperado, int tecnicoId, bool finalizar)
        {
            var parametros = new List<object>
            {
                new SqlParameter("@ChamadoId", SqlDbType.Int) { Value = estadoEsperado.Id },
                new SqlParameter("@TecnicoId", SqlDbType.Int) { Value = tecnicoId },
                new SqlParameter("@TecnicoIdEsperado", SqlDbType.Int) { Value = (object?)estadoEsperado.TecnicoId ?? DBNull.Value },
                new SqlParameter("@StatusIdEsperado", SqlDbType.Int) { Value = estadoEsperado.StatusId },
                new SqlParameter("@DataFechamentoEsperada", SqlDbType.DateTime2) { Scale = 7, Value = (object?)estadoEsperado.DataFechamento ?? DBNull.Value }
            };
            // Comandos fixos e valores sempre parametrizados; preserva DateTime.Now da NP1.
            var comando = finalizar
                ? "EXEC dbo.usp_FinalizarChamado @ChamadoId, @TecnicoId, @TecnicoIdEsperado, @StatusIdEsperado, @DataFechamentoEsperada, @DataFechamento"
                : "EXEC dbo.usp_AssumirChamado @ChamadoId, @TecnicoId, @TecnicoIdEsperado, @StatusIdEsperado, @DataFechamentoEsperada";
            if (finalizar)
                parametros.Add(new SqlParameter("@DataFechamento", SqlDbType.DateTime2) { Scale = 7, Value = DateTime.Now });

            try
            {
                await _context.Database.ExecuteSqlRawAsync(comando, parametros);
                return ResultadoOperacaoChamado.Sucesso;
            }
            catch (SqlException ex) when (ex.Number >= 51101 && ex.Number <= 51106)
            {
                return (ResultadoOperacaoChamado)ex.Number;
            }
            finally
            {
                // SQL direto nao sincroniza o tracker. Invalida somente o chamado afetado.
                foreach (var entry in _context.ChangeTracker.Entries<Chamado>()
                    .Where(e => e.Entity.Id == estadoEsperado.Id).ToList())
                    entry.State = EntityState.Detached;
            }
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

        public async Task<bool> AtualizarStatus(int id, int statusId)
        {
            var chamado = await _context.Chamados.FindAsync(id);

            if (chamado == null)
                return false;

            chamado.StatusId = statusId;

            if (statusId == 3)
                chamado.DataFechamento = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
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

        public async Task<ComentarioResponseDTO> CriarComentario(int chamadoId, int usuarioId, CriarComentarioDTO dto)
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

            // Lista explicita de campos publicos; nao serializa o grafo rastreado pelo EF.
            return new ComentarioResponseDTO
            {
                Id = comentario.Id,
                ChamadoId = comentario.ChamadoId,
                Mensagem = comentario.Mensagem,
                DataEnvio = comentario.DataEnvio,
                Usuario = new UsuarioResumoDTO
                {
                    Id = usuario.Id,
                    Nome = usuario.Nome,
                    Email = usuario.Email,
                    Tipo = usuario.Tipo
                }
            };
        }
    }
}
