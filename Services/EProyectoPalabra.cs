using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEProyectoPalabra
    {
        Task CrearProyectoPalabraAsync(string proyectoCodigo, string palabra);
    }

    public class EProyectoPalabra : IEProyectoPalabra
    {
        private readonly AppDbContext _db;

        public EProyectoPalabra(AppDbContext db)
        {
            _db = db;
        }

        public async Task CrearProyectoPalabraAsync(string proyectoCodigo, string palabra)
        {
            const string sql = @"
                SELECT neuromkt.i_proyecto_palabra(
                    @p_proyecto_codigo,
                    @p_palabra,
                    @p_codigo
                );";

            var pProyecto = new NpgsqlParameter("@p_proyecto_codigo", proyectoCodigo.Trim());
            var pPalabra  = new NpgsqlParameter("@p_palabra", palabra.Trim());
            var pCodigo   = new NpgsqlParameter("@p_codigo", DBNull.Value); // que genere PP<n>

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pProyecto, pPalabra, pCodigo);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }
    }


}