using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using TechHelpSolutions.Models;

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

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Usuario>().HasData(
                new { Id = 1, Nome = "João da Silva", Email = "usuario@gmail.com", Senha = "Senha123", Tipo = "Usuario", DataCriacao = new DateTime(2026, 10, 27, 9, 31, 35).AddTicks(2929494) },
                new { Id = 2, Nome = "Gabriel Costa", Email = "tecnico@gmail.com", Senha = "Senha123", Tipo = "Tecnico", DataCriacao = new DateTime(2026, 10, 27, 9, 32, 2).AddTicks(6711620) },
                new { Id = 3, Nome = "Luiz Silveira", Email = "gestor@gmail.com", Senha = "Senha123", Tipo = "Admin", DataCriacao = new DateTime(2026, 10, 27, 9, 32, 31).AddTicks(6671228) }
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
                new { Id = 2, ChamadoId = 1, UsuarioId = 1, Mensagem = "Olá Gabriel!", DataEnvio = new DateTime(2026, 10, 27, 9, 38, 54).AddTicks(3671343) }
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
