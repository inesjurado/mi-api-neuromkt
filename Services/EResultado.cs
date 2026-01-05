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
        Task<string> GenerarCsvResultadosProyectoAsync(string proyectoCodigo);
        Task<List<(string ColorHex, int Total)>> EstadisticasColoresProyectoAsync(string proyectoCodigo);
        Task<List<(string Palabra, int Total)>> EstadisticasPalabrasProyectoAsync(string proyectoCodigo);
        Task<bool> ExistenResultadosParaPruebaAsync(string pruebaCodigo);
        // COLORES
        Task<List<(string ColorHex, string Genero, int Total)>> EstadisticasColoresPorGeneroAsync(string proyectoCodigo);
        Task<List<(string ColorHex, string RangoEdad, int Total)>> EstadisticasColoresPorEdadAsync(string proyectoCodigo);
        // PALABRAS
        Task<List<(string Genero, string Palabra, int Total)>> EstadisticasPalabrasPorGeneroAsync(string proyectoCodigo);
        Task<List<(string RangoEdad, string Palabra, int Total)>> EstadisticasPalabrasPorEdadAsync(string proyectoCodigo);
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


        public async Task<string> GenerarCsvResultadosProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"SELECT * FROM neuromkt.f_resultados_proyecto_csv(@p_codigo);";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_codigo", proyectoCodigo);

                await using var reader = await cmd.ExecuteReaderAsync();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("usuario,color,palabra");

                while (await reader.ReadAsync())
                {
                    var usuario = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var color   = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var palabra = reader.IsDBNull(2) ? "" : reader.GetString(2);

                    sb.AppendLine($"{Csv(usuario)},{Csv(color)},{Csv(palabra)}");
                }

                return sb.ToString();
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            static string Csv(string value)
            {
                // escapa comillas y separadores
                value ??= "";
                var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
                value = value.Replace("\"", "\"\"");
                return mustQuote ? $"\"{value}\"" : value;
            }
        }


        public async Task<List<(string ColorHex, int Total)>> EstadisticasColoresProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"SELECT color_hex, total FROM neuromkt.f_estadisticas_colores_proyecto(@p);";
            var lista = new List<(string ColorHex, int Total)>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", proyectoCodigo.Trim()); // <- SIN @ aquí

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var color = reader.GetString(0);
                    var total = reader.GetInt32(1);
                    lista.Add((color, total));
                }

                return lista;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }


        public async Task<List<(string Palabra, int Total)>> EstadisticasPalabrasProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"SELECT palabra, total FROM neuromkt.f_estadisticas_palabras_proyecto(@p);";
            var lista = new List<(string Palabra, int Total)>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", proyectoCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var palabra = reader.GetString(0);
                    var total   = reader.GetInt32(1);
                    lista.Add((palabra, total));
                }

                return lista;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

        public async Task<bool> ExistenResultadosParaPruebaAsync(string pruebaCodigo)
        {
            const string sql = @"
                SELECT EXISTS(
                    SELECT 1
                    FROM neuromkt.resultados
                    WHERE prueba_codigo = :p_prueba_codigo
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p_prueba_codigo", pruebaCodigo.Trim());

                var result = await cmd.ExecuteScalarAsync();
                return result is bool b && b;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

        public async Task<List<(string ColorHex, string Genero, int Total)>> EstadisticasColoresPorGeneroAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT color_hex, genero, total
                FROM neuromkt.f_colores_por_genero(@p);
            ";

            var lista = new List<(string ColorHex, string Genero, int Total)>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", proyectoCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add((
                        reader.GetString(0), // color_hex
                        reader.GetString(1), // genero
                        reader.GetInt32(2)   // total
                    ));
                }

                return lista;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

        public async Task<List<(string ColorHex, string RangoEdad, int Total)>> EstadisticasColoresPorEdadAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT color_hex, rango_edad, total
                FROM neuromkt.f_colores_por_edad(@p);
            ";

            var lista = new List<(string ColorHex, string RangoEdad, int Total)>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", proyectoCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add((
                        reader.GetString(0), // color_hex
                        reader.GetString(1), // rango_edad
                        reader.GetInt32(2)   // total
                    ));
                }

                return lista;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

        public async Task<List<(string Genero, string Palabra, int Total)>> EstadisticasPalabrasPorGeneroAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT genero, palabra, total
                FROM neuromkt.f_estadisticas_palabras_por_genero_proyecto(@p);
            ";

            var lista = new List<(string Genero, string Palabra, int Total)>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", proyectoCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var genero  = reader.IsDBNull(0) ? "Sin género" : reader.GetString(0);
                    var palabra = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var total   = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

                    lista.Add((genero, palabra, total));
                }

                return lista;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

        public async Task<List<(string RangoEdad, string Palabra, int Total)>> EstadisticasPalabrasPorEdadAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT rango_edad, palabra, total
                FROM neuromkt.f_estadisticas_palabras_por_edad_proyecto(@p);
            ";

            var lista = new List<(string RangoEdad, string Palabra, int Total)>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", proyectoCodigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rango   = reader.IsDBNull(0) ? "Sin edad" : reader.GetString(0);
                    var palabra = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var total   = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

                    lista.Add((rango, palabra, total));
                }

                return lista;
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }





            
    }   
}