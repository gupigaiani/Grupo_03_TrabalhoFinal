using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using TechHelpSolutions.Models;
using TechHelpSolutions.Data.Queries;

namespace TechHelpSolutions.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Chamado> Chamados { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Prioridade> Prioridades { get; set; }
        public DbSet<Status> Status { get; set; }
        public DbSet<Comentario> Comentarios { get; set; }
        public DbSet<ChamadoDetalhado> ChamadosDetalhados => Set<ChamadoDetalhado>();
        public DbSet<ComentarioDetalhado> ComentariosDetalhados => Set<ComentarioDetalhado>();

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // SQL Server nao permite OUTPUT direto em tabela com trigger habilitada.
            modelBuilder.Entity<Chamado>().ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<ChamadoDetalhado>().HasNoKey().ToView("vw_ChamadosDetalhados", "dbo");
            modelBuilder.Entity<ComentarioDetalhado>().HasNoKey().ToView("vw_ComentariosDetalhados", "dbo");
            modelBuilder.HasDbFunction(typeof(FuncoesSql).GetMethod(nameof(FuncoesSql.DuracaoMinutos))!)
                .HasName("ufn_DuracaoMinutos").HasSchema("dbo");

            modelBuilder.Entity<Usuario>().HasData(
                new { Id = 1, Nome = "João da Silva", Email = "usuario@gmail.com", Senha = "THS$1$PBKDF2-SHA256$600000$Qjrq0hZpiJdV2fv3hvqeTg==$XaMInIG/McJT0t3IG9BNi0mHCkBFs9CTNtlHstxjm8A=", Tipo = "Usuario", DataCriacao = new DateTime(2026, 10, 27, 9, 31, 35).AddTicks(2929494) },
                new { Id = 2, Nome = "Gabriel Costa", Email = "tecnico@gmail.com", Senha = "THS$1$PBKDF2-SHA256$600000$K+TsNXKCgbezgPQ3FR/BRw==$NoBvmFs8ZuGcS8QMJb7/dU+rkfrjfs+cDL+bli3HbT0=", Tipo = "Tecnico", DataCriacao = new DateTime(2026, 10, 27, 9, 32, 2).AddTicks(6711620) },
                new { Id = 3, Nome = "Luiz Silveira", Email = "gestor@gmail.com", Senha = "THS$1$PBKDF2-SHA256$600000$SKJi+0mHtohLEGV4cIo+tA==$OVOmmACoFV6lB4nMWxPNFsRuYtbFmeqddy+kTX0GHqA=", Tipo = "Gestor", DataCriacao = new DateTime(2026, 10, 27, 9, 32, 31).AddTicks(6671228) },
                new { Id = 4, Nome = "Fernando Campos", Email = "admin@gmail.com", Senha = "THS$1$PBKDF2-SHA256$600000$NGeL5tolc85bk6iei7E0AQ==$uFqiawfFwA/6U5GPP05mqy/uSDVnNNE/vZoWyaz/tKQ=", Tipo = "Admin", DataCriacao = new DateTime(2026, 10, 27, 9, 33, 0) }
            );

            modelBuilder.Entity<Chamado>().HasData(
                new
                {
                    Id = 1,
                    Titulo = "Impressora não funciona",
                    Descricao = "A impressora não está funcionando desde ontem.",
                    UsuarioId = 1, TecnicoId = (int?)2, CategoriaId = 1, PrioridadeId = 1, StatusId = 3,
                    DataAbertura = new DateTime(2026, 10, 27, 9, 34, 13).AddTicks(3097614),
                    DataFechamento = (DateTime?)new DateTime(2026, 10, 27, 9, 38, 34).AddTicks(1561336)
                },
                new
                {
                    Id = 2,
                    Titulo = "Portal da empresa está fora",
                    Descricao = "Estou tentando acessar o portal da empresa e dá acesso negado.",
                    UsuarioId = 1, TecnicoId = (int?)2, CategoriaId = 2, PrioridadeId = 2, StatusId = 2,
                    DataAbertura = new DateTime(2026, 10, 27, 9, 36, 26).AddTicks(6679442),
                    DataFechamento = (DateTime?)null
                },
                new
                {
                    Id = 3,
                    Titulo = "Wi-Fi não funciona",
                    Descricao = "Já tem duas horas que a internet não funciona.",
                    UsuarioId = 1, TecnicoId = (int?)null, CategoriaId = 3, PrioridadeId = 3, StatusId = 1,
                    DataAbertura = new DateTime(2026, 10, 27, 9, 37, 16).AddTicks(9176033),
                    DataFechamento = (DateTime?)null
                }
            );

            modelBuilder.Entity<Comentario>().HasData(
                new { Id = 1, ChamadoId = 1, UsuarioId = 2, Mensagem = "Olá João!", DataEnvio = new DateTime(2026, 10, 27, 9, 38, 19).AddTicks(7631189) },
                new { Id = 2, ChamadoId = 1, UsuarioId = 1, Mensagem = "Olá Gabriel!", DataEnvio = new DateTime(2026, 10, 27, 9, 38, 24).AddTicks(3671343) }
            );

            modelBuilder.Entity<Categoria>().HasData(
                new { Id = 1, Nome = "Hardware" },
                new { Id = 2, Nome = "Software" },
                new { Id = 3, Nome = "Rede" },
                new { Id = 4, Nome = "Outros" }
            );

            modelBuilder.Entity<Prioridade>().HasData(
                new { Id = 1, Nome = "Baixa" },
                new { Id = 2, Nome = "Média" },
                new { Id = 3, Nome = "Alta" }
            );

            modelBuilder.Entity<Status>().HasData(
                new { Id = 1, Nome = "Aberto" },
                new { Id = 2, Nome = "Em andamento" },
                new { Id = 3, Nome = "Resolvido" }
            );

            modelBuilder.Entity<Chamado>()
                .HasOne(c => c.Usuario)
                .WithMany(u => u.ChamadosAbertos)
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Chamado>()
                .HasOne(c => c.Tecnico)
                .WithMany(u => u.ChamadosAtendidos)
                .HasForeignKey(c => c.TecnicoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
