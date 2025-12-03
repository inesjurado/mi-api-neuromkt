using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;
using System.Data;

namespace NeuromktApi.Services
{
    public interface IEProyectoColor
    {
        Task CrearProyectoColorAsync(string proyectoCodigo, string colorHex);
        Task<List<ProyectoColorModel>> ListarColoresPorProyectoAsync(string proyectoCodigo);
        Task EliminarPorProyectoAsync(string proyectoCodigo);
    }

    public class EProyectoColor : IEProyectoColor
    {
        private readonly AppDbContext _db;

        public EProyectoColor(AppDbContext db)
        {
            _db = db;
        }

        public async Task CrearProyectoColorAsync(string proyectoCodigo, string colorHex)
        {
            const string sql = @"
                SELECT neuromkt.i_proyecto_color(
                    @p_proyecto_codigo,
                    @p_color_hex,
                    @p_codigo
                );";

            var pProyecto = new NpgsqlParameter("@p_proyecto_codigo", proyectoCodigo.Trim());
            var pColor    = new NpgsqlParameter("@p_color_hex", colorHex.Trim());
            var pCodigo   = new NpgsqlParameter("@p_codigo", DBNull.Value); // que genere PC<n>

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pProyecto, pColor, pCodigo);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        public async Task<List<ProyectoColorModel>> ListarColoresPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT codigo, color_hex
                FROM neuromkt.f_proyecto_colores(@p_proyecto_codigo);
            ";

            var lista = new List<ProyectoColorModel>();

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
                    lista.Add(new ProyectoColorModel
                    {
                        Codigo         = reader.GetString(0),
                        ColorHex       = reader.GetString(1),
                        ProyectoCodigo = proyectoCodigo
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
            const string sql = @"SELECT neuromkt.d_proyecto_colores(@p_proyecto_codigo);";

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