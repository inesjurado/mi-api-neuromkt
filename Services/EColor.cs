using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEColor
    {
        Task<List<ColorModel>> ListarColoresAsync();
        Task CrearColorAsync(ColorModel color);
        Task EliminarColorAsync(string hex);
        Task ActualizarColorAsync(string hexOriginal, ColorModel colorNuevo);
    }

    public class EColor : IEColor
    {
        private readonly AppDbContext _db;

        public EColor(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<ColorModel>> ListarColoresAsync()
        {
            var lista = new List<ColorModel>();

            var conn = _db.Database.GetDbConnection();

            try
            {
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM neuromkt.l_colores();";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var c = new ColorModel
                    {
                        Hex = (string)reader["hex"],
                        // 👇 si en BD hay NULL, lo convertimos a ""
                        Nombre = reader["nombre"] as string ?? string.Empty
                    };

                    lista.Add(c);
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


        public async Task CrearColorAsync(ColorModel c)
        {
            const string sql = @"
                SELECT neuromkt.i_color(
                    CAST(@p_hex    AS varchar),
                    CAST(@p_nombre AS varchar)
                );";

            // normalizamos hex
            var hexNormalizado = c.Hex?.Trim() ?? string.Empty;

            // 👇 si no viene nombre, lo generamos automáticamente
            var nombreFinal = string.IsNullOrWhiteSpace(c.Nombre)
                ? $"Color {hexNormalizado}"
                : c.Nombre!.Trim();

            var pHex = new NpgsqlParameter("@p_hex", hexNormalizado);
            var pNombre = new NpgsqlParameter("@p_nombre", nombreFinal);

            var parametros = new[] { pHex, pNombre };

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



        public async Task EliminarColorAsync(string hex)
        {
            const string sql = "SELECT neuromkt.d_color(@p_hex);";

            var pHex = new NpgsqlParameter("@p_hex", hex);

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pHex);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }
        /// <summary>
        /// Actualiza un color usando neuromkt.u_color(p_hex_original, p_hex_nuevo, p_nombre)
        /// - hexOriginal: valor actual que hay en la columna hex (ej: 'rgb(124, 55, 55)')
        /// - colorNuevo: Hex y/o Nombre nuevos. Si Nombre es null/empty, se deja el que había.
        /// </summary>
        public async Task ActualizarColorAsync(string hexOriginal, ColorModel colorNuevo)
        {
            const string sql = @"
                SELECT neuromkt.u_color(
                    CAST(@p_hex_original AS varchar),
                    CAST(@p_hex_nuevo    AS varchar),
                    CAST(@p_nombre       AS varchar)
                );";

            var pHexOriginal = new NpgsqlParameter("@p_hex_original", hexOriginal);
            var pHexNuevo    = new NpgsqlParameter("@p_hex_nuevo",
                string.IsNullOrWhiteSpace(colorNuevo.Hex)
                    ? (object)DBNull.Value
                    : colorNuevo.Hex);

            var pNombre = new NpgsqlParameter("@p_nombre",
                string.IsNullOrWhiteSpace(colorNuevo.Nombre)
                    ? (object)DBNull.Value
                    : colorNuevo.Nombre);

            var parametros = new[] { pHexOriginal, pHexNuevo, pNombre };

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
