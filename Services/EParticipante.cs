using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;
using NpgsqlTypes;

namespace NeuromktApi.Services
{
    public interface IEParticipante
    {
        Task<List<ParticipanteModel>> ListarParticipantesPorCreadorAsync(string creadoPor);
        Task<string> CrearParticipanteAsync(ParticipanteModel participante);   // 👈 CAMBIO
        Task ActualizarParticipanteAsync(string email, ParticipanteModel participante);
        Task EliminarParticipanteAsync(string email);
        Task<string?> ObtenerCodigoPorEmailAsync(string email);

    }

    public class EParticipante : IEParticipante
    {
        private readonly AppDbContext _db;

        public EParticipante(AppDbContext db)
        {
            _db = db;
        }

        // ==========================
        // INSERTAR PARTICIPANTE
        // ==========================
        public async Task<string> CrearParticipanteAsync(ParticipanteModel participante) // 👈 CAMBIO
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
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                // que genere U<n>
                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value);

                if (string.IsNullOrWhiteSpace(participante.Email))
                    throw new Exception("El email del participante viene vacío al servicio.");

                cmd.Parameters.AddWithValue("p_email", participante.Email.Trim().ToLower());

                // fecha_nacimiento DATE
                cmd.Parameters.Add("p_fecha_nacimiento", NpgsqlDbType.Date).Value =
                    participante.FechaNacimiento.HasValue
                        ? participante.FechaNacimiento.Value.Date
                        : DBNull.Value;

                // género opcional
                cmd.Parameters.AddWithValue("p_genero",
                    string.IsNullOrWhiteSpace(participante.Genero)
                        ? (object)DBNull.Value
                        : participante.Genero.Trim());

                // notas opcional
                cmd.Parameters.AddWithValue("p_notas",
                    string.IsNullOrWhiteSpace(participante.Notas)
                        ? (object)DBNull.Value
                        : participante.Notas.Trim());

                // creado_por obligatorio
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
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

        // ==========================
        // LISTAR POR CREADOR
        // ==========================
        public async Task<List<ParticipanteModel>> ListarParticipantesPorCreadorAsync(string creadoPor)
        {
            var lista = new List<ParticipanteModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
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
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
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


        public async Task<string?> ObtenerCodigoPorEmailAsync(string email)
        {
            const string sql = @"SELECT neuromkt.f_participante_codigo_por_email(:p_email);";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p_email", email.Trim().ToLower());
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? null : Convert.ToString(result);
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

    }
}
