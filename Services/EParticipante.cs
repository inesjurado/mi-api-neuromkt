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
        Task<List<ParticipanteModel>> ListarParticipantesAsync();
        Task CrearParticipanteAsync(ParticipanteModel participante);
        Task ActualizarParticipanteAsync(string email, ParticipanteModel participante);
        Task EliminarParticipanteAsync(string email);
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
        public async Task CrearParticipanteAsync(ParticipanteModel participante)
        {
            const string sql = @"
                SELECT neuromkt.i_participante(
                    p_email            => :p_email,
                    p_fecha_nacimiento => :p_fecha_nacimiento,
                    p_genero           => :p_genero,
                    p_notas            => :p_notas
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                // email obligatorio
                if (string.IsNullOrWhiteSpace(participante.Email))
                    throw new Exception("El email del participante viene vacío al servicio.");

                cmd.Parameters.AddWithValue("p_email", participante.Email.Trim().ToLower());

                // fecha_nacimiento como DATE (no timestamp)
                if (participante.FechaNacimiento.HasValue)
                {
                    cmd.Parameters.Add(
                        "p_fecha_nacimiento",
                        NpgsqlTypes.NpgsqlDbType.Date
                    ).Value = participante.FechaNacimiento.Value.Date;
                }
                else
                {
                    cmd.Parameters.Add(
                        "p_fecha_nacimiento",
                        NpgsqlTypes.NpgsqlDbType.Date
                    ).Value = DBNull.Value;
                }

                // género opcional
                if (string.IsNullOrWhiteSpace(participante.Genero))
                    cmd.Parameters.AddWithValue("p_genero", DBNull.Value);
                else
                    cmd.Parameters.AddWithValue("p_genero", participante.Genero!.Trim());

                // notas opcional
                if (string.IsNullOrWhiteSpace(participante.Notas))
                    cmd.Parameters.AddWithValue("p_notas", DBNull.Value);
                else
                    cmd.Parameters.AddWithValue("p_notas", participante.Notas!.Trim());

                await cmd.ExecuteNonQueryAsync();
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
        // LISTAR PARTICIPANTES
        // ==========================
        public async Task<List<ParticipanteModel>> ListarParticipantesAsync()
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
                    SELECT email, fecha_nacimiento, genero, notas
                    FROM neuromkt.participantes
                    ORDER BY email;";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var p = new ParticipanteModel
                    {
                        Email = reader["email"] as string ?? string.Empty,
                        FechaNacimiento = reader["fecha_nacimiento"] is DBNull
                            ? (DateTime?)null
                            : Convert.ToDateTime(reader["fecha_nacimiento"]),
                        Genero = reader["genero"] as string,
                        Notas = reader["notas"] as string
                    };

                    lista.Add(p);
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

            var pEmail = new NpgsqlParameter("@p_email", email?.Trim().ToLower() ?? string.Empty);

            // Siguiendo tu función:
            // - NULL  -> no se toca ese campo
            // - ''    -> en genero/notas se convierte a NULL con nullif(...)
            object fechaParam = participante.FechaNacimiento.HasValue
                ? participante.FechaNacimiento.Value
                : (object)DBNull.Value;

            var pFechaNacimiento = new NpgsqlParameter("@p_fecha_nacimiento", NpgsqlDbType.Date);
            if (participante.FechaNacimiento.HasValue)
                pFechaNacimiento.Value = participante.FechaNacimiento.Value.Date;
            else
                pFechaNacimiento.Value = DBNull.Value;


            var pGenero = new NpgsqlParameter("@p_genero",
                (object?)participante.Genero ?? DBNull.Value);

            var pNotas = new NpgsqlParameter("@p_notas",
                (object?)participante.Notas ?? DBNull.Value);

            var parametros = new[] { pEmail, pFechaNacimiento, pGenero, pNotas };

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, parametros);
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

            var param = new NpgsqlParameter("@p_email", email?.Trim().ToLower() ?? string.Empty);

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
    }
}