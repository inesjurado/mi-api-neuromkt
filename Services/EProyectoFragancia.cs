using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEProyectoFragancia
    {
        Task<string> CrearProyectoFraganciaAsync(string proyectoCodigo, string fraganciaCodigo);
    }

    public class EProyectoFragancia : IEProyectoFragancia
    {
        private readonly AppDbContext _db;

        public EProyectoFragancia(AppDbContext db)
        {
            _db = db;
        }

        public async Task<string> CrearProyectoFraganciaAsync(string proyectoCodigo, string fraganciaCodigo)
        {
            const string sql = @"
                SELECT neuromkt.i_proyecto_fragancia(
                    p_codigo          => :p_codigo,
                    p_proyecto_codigo => :p_proyecto_codigo,
                    p_fragancia_codigo => :p_fragancia_codigo
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value); // que genere PF<n>
                cmd.Parameters.AddWithValue("p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue("p_fragancia_codigo", fraganciaCodigo.Trim());

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
