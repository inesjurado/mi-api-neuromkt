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
        Task<List<ProyectoFraganciaModel>> ListarFraganciasPorProyectoAsync(string proyectoCodigo);
        Task EliminarPorProyectoAsync(string proyectoCodigo);
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

        public async Task<List<ProyectoFraganciaModel>> ListarFraganciasPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT pf.codigo, pf.fragancia_codigo, f.nombre
                FROM neuromkt.f_proyecto_fragancias(@p_proyecto_codigo) pf
                JOIN neuromkt.fragancias f ON f.codigo = pf.fragancia_codigo
                ORDER BY f.nombre;
            ";

            var lista = new List<ProyectoFraganciaModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ProyectoFraganciaModel
                    {
                        Codigo          = reader.GetString(0),
                        FraganciaCodigo = reader.GetString(1),
                        FraganciaNombre = reader.GetString(2),
                        ProyectoCodigo  = proyectoCodigo
                    });
                }

                return lista;
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

        public async Task EliminarPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"SELECT neuromkt.d_proyecto_fragancias(@p_proyecto_codigo);";

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

    }
}
