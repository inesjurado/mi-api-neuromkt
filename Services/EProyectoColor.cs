using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEProyectoColor
    {
        Task CrearProyectoColorAsync(string proyectoCodigo, string colorHex);
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

    }


}