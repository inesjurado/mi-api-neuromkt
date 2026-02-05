using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NeuromktApi.Models;
using Npgsql;
using NpgsqlTypes;

namespace NeuromktApi.Services
{
    public interface IEParticipante
    {
        Task<List<ParticipanteModel>> ListarParticipantesPorCreadorAsync(string creadoPor);
        Task<List<ParticipanteModel>> ListarParticipantesDisponiblesPorProyectoAsync(string proyectoCodigo, string creadoPor, string? fraganciaCodigo = null);
        Task<string> CrearParticipanteAsync(ParticipanteModel participante);
        Task ActualizarParticipanteAsync(string email, ParticipanteModel participante);
        Task EliminarParticipanteAsync(string email);
        Task<string?> ObtenerCodigoPorEmailAsync(string email);
    }

    public class EParticipante : IEParticipante
    {
        private readonly AppDbContext _db;
        private readonly string _cs;

        public EParticipante(AppDbContext db, IConfiguration cfg)
        {
            _db = db;
            _cs = cfg.GetConnectionString("DefaultConnection")
                  ?? throw new Exception("Falta ConnectionString 'DefaultConnection' en appsettings.json");
        }

        // ==========================
        // INSERTAR PARTICIPANTE
        // ==========================
        public async Task<string> CrearParticipanteAsync(ParticipanteModel participante)
        {
            const string sql = @"
                SELECT neuromkt.i_participante(
                    p_codigo            => :p_codigo,
                    p_email             => :p_email,
                    p_fecha_nacimiento  => :p_fecha_nacimiento,
                    p_genero            => :p_genero,
                    p_notas             => :p_notas,
                    p_creado_por        => :p_creado_por
                );
            ";
            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value); 

                if (string.IsNullOrWhiteSpace(participante.Email))
                    throw new Exception("El email del participante viene vacío al servicio.");

                cmd.Parameters.AddWithValue("p_email", participante.Email.Trim().ToLower());

                cmd.Parameters.Add("p_fecha_nacimiento", NpgsqlDbType.Date).Value =
                    participante.FechaNacimiento.HasValue
                        ? participante.FechaNacimiento.Value.Date
                        : DBNull.Value;

                cmd.Parameters.AddWithValue("p_genero",
                    string.IsNullOrWhiteSpace(participante.Genero)
                        ? (object)DBNull.Value
                        : participante.Genero.Trim());

                cmd.Parameters.AddWithValue("p_notas",
                    string.IsNullOrWhiteSpace(participante.Notas)
                        ? (object)DBNull.Value
                        : participante.Notas.Trim());

                if (string.IsNullOrWhiteSpace(participante.CreadoPor))
                    throw new Exception("Falta CreadoPor (usuario logeado) para crear el participante.");

                cmd.Parameters.AddWithValue("p_creado_por", participante.CreadoPor.Trim().ToLower());

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToString(result) ?? string.Empty;
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

        // ==========================
        // LISTAR POR CREADOR 
        // ==========================
        public async Task<List<ParticipanteModel>> ListarParticipantesPorCreadorAsync(string creadoPor)
        {
            var lista = new List<ParticipanteModel>();

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT codigo, email, fecha_nacimiento, genero, notas, creado_por
                    FROM neuromkt.l_participantes_por_creador(@p_creado_por)
                    ORDER BY email;";

                cmd.Parameters.AddWithValue("@p_creado_por", creadoPor.Trim().ToLower());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ParticipanteModel
                    {
                        Codigo = reader["codigo"] as string ?? string.Empty,
                        Email = reader["email"] as string ?? string.Empty,
                        FechaNacimiento = reader["fecha_nacimiento"] is DBNull
                            ? (DateTime?)null
                            : Convert.ToDateTime(reader["fecha_nacimiento"]),
                        Genero = reader["genero"] as string,
                        Notas = reader["notas"] as string,
                        CreadoPor = reader["creado_por"] as string
                    });
                }
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }

            return lista;
        }

        // ==========================
        // DISPONIBLES PARA PROYECTO+FRAGANCIA
        // ==========================
        public async Task<List<ParticipanteModel>> ListarParticipantesDisponiblesPorProyectoAsync(
            string proyectoCodigo,
            string creadoPor,
            string? fraganciaCodigo = null)
        {
            if (string.IsNullOrWhiteSpace(proyectoCodigo))
                throw new ArgumentException("proyectoCodigo vacío", nameof(proyectoCodigo));

            if (string.IsNullOrWhiteSpace(creadoPor))
                throw new ArgumentException("creadoPor vacío", nameof(creadoPor));

            var lista = new List<ParticipanteModel>();

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
                    SELECT email, codigo
                    FROM neuromkt.f_participantes_disponibles_proyecto(
                        @p_proyecto_codigo,
                        @p_creado_por,
                        @p_fragancia_codigo
                    )
                    ORDER BY email;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue("@p_creado_por", creadoPor.Trim().ToLower());
                cmd.Parameters.AddWithValue("@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? (object)DBNull.Value : fraganciaCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ParticipanteModel
                    {
                        Email = reader["email"] as string ?? string.Empty,
                        Codigo = reader["codigo"] as string ?? string.Empty,
                        CreadoPor = creadoPor.Trim().ToLower()
                    });
                }
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }

            return lista;
        }

        // ==========================
        // ACTUALIZAR PARTICIPANTE
        // ==========================
        public async Task ActualizarParticipanteAsync(string email, ParticipanteModel participante)
        {
            const string sql = @"
                SELECT neuromkt.u_participante(
                    @p_email,
                    @p_fecha_nacimiento,
                    @p_genero,
                    @p_notas
                );";

            var pEmail = new NpgsqlParameter("@p_email", (email ?? string.Empty).Trim().ToLower());

            var pFechaNacimiento = new NpgsqlParameter("@p_fecha_nacimiento", NpgsqlDbType.Date)
            {
                Value = participante.FechaNacimiento.HasValue
                    ? participante.FechaNacimiento.Value.Date
                    : DBNull.Value
            };

            var pGenero = new NpgsqlParameter("@p_genero",
                string.IsNullOrWhiteSpace(participante.Genero) ? (object)DBNull.Value : participante.Genero.Trim());

            var pNotas = new NpgsqlParameter("@p_notas",
                string.IsNullOrWhiteSpace(participante.Notas) ? (object)DBNull.Value : participante.Notas.Trim());

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pEmail, pFechaNacimiento, pGenero, pNotas);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        // ==========================
        // ELIMINAR PARTICIPANTE 
        // ==========================
        public async Task EliminarParticipanteAsync(string email)
        {
            const string sql = @"SELECT neuromkt.d_participante(@p_email);";

            var param = new NpgsqlParameter("@p_email", (email ?? string.Empty).Trim().ToLower());

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, param);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        // ==========================
        // OBTENER CÓDIGO POR EMAIL 
        // ==========================
        public async Task<string?> ObtenerCodigoPorEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            const string sql = @"SELECT neuromkt.f_participante_codigo_por_email(@p_email);";

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_email", email.Trim().ToLower());

                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? null : Convert.ToString(result);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }
    }
}