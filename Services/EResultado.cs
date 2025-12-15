using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEResultado
    {
        Task<string> CrearResultadoAsync(string pruebaCodigo, string colorHex, string palabra);
        Task<List<ResultadoModel>> ListarPorPruebaAsync(string pruebaCodigo);
    }

    public class EResultado : IEResultado
    {
        private readonly AppDbContext _db;

        public EResultado(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Inserta un resultado en neuromkt.resultados usando la función neuromkt.i_resultado.
        /// Asumimos firma: i_resultado(p_codigo, p_prueba_codigo, p_color_hex, p_palabra)
        /// y que devuelve el código generado.
        /// </summary>
        public async Task<string> CrearResultadoAsync(string pruebaCodigo, string colorHex, string palabra)
        {
            const string sql = @"
                SELECT neuromkt.i_resultado(
                    p_codigo         => :p_codigo,
                    p_prueba_codigo  => :p_prueba_codigo,
                    p_color_hex      => :p_color_hex,
                    p_palabra        => :p_palabra
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value);                 // que genere RES<n> o similar
                cmd.Parameters.AddWithValue("p_prueba_codigo", pruebaCodigo.Trim());
                cmd.Parameters.AddWithValue("p_color_hex", colorHex.Trim());
                cmd.Parameters.AddWithValue("p_palabra", palabra.Trim());

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

        /// <summary>
        /// Devuelve los resultados de una prueba.
        /// </summary>
        public async Task<List<ResultadoModel>> ListarPorPruebaAsync(string pruebaCodigo)
        {
            const string sql = @"
                SELECT codigo, prueba_codigo, color_hex, palabra
                FROM neuromkt.resultados
                WHERE prueba_codigo = :p_prueba_codigo
                ORDER BY codigo;
            ";

            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p_prueba_codigo", pruebaCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var r = new ResultadoModel
                    {
                        Codigo       = reader.GetString(0),
                        PruebaCodigo = reader.GetString(1),
                        ColorHex     = reader.GetString(2),
                        Palabra      = reader.GetString(3)
                    };

                    lista.Add(r);
                }

                return lista;
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
