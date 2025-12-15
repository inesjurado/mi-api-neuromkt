using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEPrueba
    {
        // AHORA RECIBE EL CÓDIGO DEL PARTICIPANTE (U01, U02, ...)
        Task<string> CrearPruebaAsync(string proyectoCodigo, string participanteCodigo);
        Task<string> ActualizarFechaPruebaAsync(string pruebaCodigo, DateTime? fecha = null);
        Task<List<PruebaModel>> ListarPruebasPorProyectoAsync(string proyectoCodigo);
        Task EliminarPruebasPorProyectoAsync(string proyectoCodigo);
    }

    public class EPrueba : IEPrueba
    {
        private readonly AppDbContext _db;

        public EPrueba(AppDbContext db)
        {
            _db = db;
        }

        public async Task<string> CrearPruebaAsync(string proyectoCodigo, string participanteCodigo)
        {
            const string sql = @"
                SELECT neuromkt.i_prueba(
                    p_codigo              => :p_codigo,
                    p_proyecto_codigo     => :p_proyecto_codigo,
                    p_participante_codigo => :p_participante_codigo
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                // que genere PRB<n>
                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value);
                cmd.Parameters.AddWithValue("p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue("p_participante_codigo", participanteCodigo.Trim());

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

        public async Task<string> ActualizarFechaPruebaAsync(string pruebaCodigo, DateTime? fecha = null)
        {
            const string sql = @"
                SELECT neuromkt.u_prueba(
                    p_codigo        => :p_codigo,
                    p_fecha_prueba  => :p_fecha_prueba
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("p_codigo", pruebaCodigo.Trim());

                // Si no se pasa fecha, mandamos NULL y la función pone NOW()
                if (fecha.HasValue)
                    cmd.Parameters.AddWithValue("p_fecha_prueba", fecha.Value);
                else
                    cmd.Parameters.AddWithValue("p_fecha_prueba", DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                var codigo = Convert.ToString(result);

                if (string.IsNullOrWhiteSpace(codigo))
                    throw new Exception("neuromkt.u_prueba no devolvió código.");

                return codigo;
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

        public async Task EliminarPruebasPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"SELECT neuromkt.d_pruebas_proyecto(@p_proyecto_codigo);";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo);
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

        public async Task<List<PruebaModel>> ListarPruebasPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT  pr.codigo,
                        pr.participante_codigo,
                        pa.email,
                        pr.fecha_prueba
                FROM    neuromkt.pruebas pr
                JOIN    neuromkt.participantes pa
                    ON pa.codigo = pr.participante_codigo
                WHERE   pr.proyecto_codigo = :p_proyecto_codigo
                ORDER BY pa.email;
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p_proyecto_codigo", proyectoCodigo.Trim());

                var pruebas = new List<PruebaModel>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    pruebas.Add(new PruebaModel
                    {
                        Codigo             = reader.GetString(0),                                      // pr.codigo
                        ParticipanteCodigo = reader.GetString(1),                                      // pr.participante_codigo
                        ParticipanteEmail  = reader.GetString(2),                                      // pa.email
                        FechaPrueba        = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3)
                    });
                }

                return pruebas;
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

    }
}
