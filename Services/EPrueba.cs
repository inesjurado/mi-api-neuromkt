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
        Task<string> CrearPruebaAsync(string proyectoCodigo, string participanteEmail);
    }

    public class EPrueba : IEPrueba
    {
        private readonly AppDbContext _db;

        public EPrueba(AppDbContext db)
        {
            _db = db;
        }

        public async Task<string> CrearPruebaAsync(string proyectoCodigo, string participanteEmail)
        {
            const string sql = @"
                SELECT neuromkt.i_prueba(
                    p_codigo           => :p_codigo,
                    p_proyecto_codigo  => :p_proyecto_codigo,
                    p_participante_email => :p_participante_email
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value); // que genere PRB<n>
                cmd.Parameters.AddWithValue("p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue("p_participante_email", participanteEmail.Trim().ToLower());

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
    }
}
