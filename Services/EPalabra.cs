using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEPalabra
    {
        Task<List<PalabraModel>> ListarPalabrasAsync();
        Task CrearPalabraAsync(PalabraModel palabra);
        Task EliminarPalabraAsync(string palabra);
        Task ActualizarPalabraAsync(string palabraOriginal, string palabraNueva);
    }

    public class EPalabra : IEPalabra
    {
        private readonly AppDbContext _db;

        public EPalabra(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<PalabraModel>> ListarPalabrasAsync()
        {
            var lista = new List<PalabraModel>();

            var conn = _db.Database.GetDbConnection();

            try
            {
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM neuromkt.l_palabras();";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var p = new PalabraModel
                    {
                        Palabra = (string)reader["palabra"]
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
                await conn.CloseAsync();
            }

            return lista;
        }

        public async Task CrearPalabraAsync(PalabraModel p)
        {
            const string sql = @"
                SELECT neuromkt.i_palabra(
                    CAST(@p_palabra AS varchar)
                );";

            var pPalabra = new NpgsqlParameter("@p_palabra", p.Palabra);

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pPalabra);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        public async Task EliminarPalabraAsync(string palabra)
        {
            const string sql = "SELECT neuromkt.d_palabra(@p_palabra);";

            var pPalabra = new NpgsqlParameter("@p_palabra", palabra);

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pPalabra);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        /// <summary>
        /// Actualiza una palabra usando neuromkt.u_palabra(p_palabra_original, p_palabra_nueva)
        /// </summary>
        public async Task ActualizarPalabraAsync(string palabraOriginal, string palabraNueva)
        {
            const string sql = @"
                SELECT neuromkt.u_palabra(
                    CAST(@p_original AS varchar),
                    CAST(@p_nueva    AS varchar)
                );";

            var pOriginal = new NpgsqlParameter("@p_original", palabraOriginal);
            var pNueva    = new NpgsqlParameter("@p_nueva", palabraNueva);

            var parametros = new[] { pOriginal, pNueva };

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
    }
}
