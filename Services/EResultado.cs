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
        Task<bool> ExistenResultadosParaPruebaAsync(string pruebaCodigo);

        // General
        Task<List<ResultadoModel>> EstadisticasColoresProyectoAsync(string proyectoCodigo, string? fraganciaCodigo = null);
        Task<List<ResultadoModel>> EstadisticasPalabrasProyectoAsync(string proyectoCodigo, string? fraganciaCodigo = null);

        // Genero
        Task<List<ResultadoModel>> EstadisticasColoresPorGeneroAsync(string proyectoCodigo, string? fraganciaCodigo = null);
        Task<List<ResultadoModel>> EstadisticasPalabrasPorGeneroAsync(string proyectoCodigo, string? fraganciaCodigo = null);

        // Edad
        Task<List<ResultadoModel>> EstadisticasColoresPorEdadAsync(string proyectoCodigo, string? fraganciaCodigo = null);
        Task<List<ResultadoModel>> EstadisticasPalabrasPorEdadAsync(string proyectoCodigo, string? fraganciaCodigo = null);
    }

    public class EResultado : IEResultado
    {
        private readonly AppDbContext _db;

        public EResultado(AppDbContext db)
        {
            _db = db;
        }

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

                cmd.Parameters.AddWithValue("p_codigo", DBNull.Value); 
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
                        Codigo = reader.GetString(0),
                        PruebaCodigo = reader.GetString(1),
                        ColorHex = reader.GetString(2),
                        Palabra = reader.GetString(3)
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
                    var color = reader.IsDBNull(1) ? "" : reader.GetString(1);
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
                value ??= "";
                var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
                value = value.Replace("\"", "\"\"");
                return mustQuote ? $"\"{value}\"" : value;
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

        public async Task<List<ResultadoModel>> EstadisticasColoresProyectoAsync(string proyectoCodigo, string? fraganciaCodigo = null)
        {
            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT color_hex, total
                    FROM neuromkt.f_estadisticas_colores_proyecto(@p_proyecto_codigo, @p_fragancia_codigo)
                    ORDER BY total DESC;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue(
                    "@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? DBNull.Value : fraganciaCodigo.Trim()
                );

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ResultadoModel
                    {
                        Tipo = "color",
                        Valor = reader["color_hex"] as string ?? string.Empty,
                        Total = reader["total"] is DBNull ? 0 : Convert.ToInt32(reader["total"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }

        public async Task<List<ResultadoModel>> EstadisticasPalabrasProyectoAsync(string proyectoCodigo, string? fraganciaCodigo = null)
        {
            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT palabra, total
                    FROM neuromkt.f_estadisticas_palabras_proyecto(@p_proyecto_codigo, @p_fragancia_codigo)
                    ORDER BY total DESC;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue(
                    "@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? DBNull.Value : fraganciaCodigo.Trim()
                );

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ResultadoModel
                    {
                        Tipo = "palabra",
                        Valor = reader["palabra"] as string ?? string.Empty,
                        Total = reader["total"] is DBNull ? 0 : Convert.ToInt32(reader["total"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }

        public async Task<List<ResultadoModel>> EstadisticasColoresPorGeneroAsync(string proyectoCodigo, string? fraganciaCodigo = null)
        {
            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT genero, color_hex, total
                    FROM neuromkt.f_estadisticas_colores_por_genero_proyecto(@p_proyecto_codigo, @p_fragancia_codigo)
                    ORDER BY genero, total DESC;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue(
                    "@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? DBNull.Value : fraganciaCodigo.Trim()
                );

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ResultadoModel
                    {
                        Tipo = "color",
                        UsuarioEmail = reader["genero"] as string ?? "Sin género", // grupo
                        Valor = reader["color_hex"] as string ?? string.Empty,
                        Total = reader["total"] is DBNull ? 0 : Convert.ToInt32(reader["total"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }

        public async Task<List<ResultadoModel>> EstadisticasPalabrasPorGeneroAsync(string proyectoCodigo, string? fraganciaCodigo = null)
        {
            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT genero, palabra, total
                    FROM neuromkt.f_estadisticas_palabras_por_genero_proyecto(@p_proyecto_codigo, @p_fragancia_codigo)
                    ORDER BY genero, total DESC, palabra;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue(
                    "@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? DBNull.Value : fraganciaCodigo.Trim()
                );

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ResultadoModel
                    {
                        Tipo = "palabra",
                        UsuarioEmail = reader["genero"] as string ?? "Sin género", 
                        Valor = reader["palabra"] as string ?? string.Empty,
                        Total = reader["total"] is DBNull ? 0 : Convert.ToInt32(reader["total"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }

        public async Task<List<ResultadoModel>> EstadisticasColoresPorEdadAsync(string proyectoCodigo, string? fraganciaCodigo = null)
        {
            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT rango_edad, color_hex, total
                    FROM neuromkt.f_estadisticas_colores_por_edad_proyecto(@p_proyecto_codigo, @p_fragancia_codigo)
                    ORDER BY rango_edad, total DESC;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue(
                    "@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? DBNull.Value : fraganciaCodigo.Trim()
                );

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ResultadoModel
                    {
                        Tipo = "color",
                        UsuarioEmail = reader["rango_edad"] as string ?? "Sin edad", // grupo
                        Valor = reader["color_hex"] as string ?? string.Empty,
                        Total = reader["total"] is DBNull ? 0 : Convert.ToInt32(reader["total"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }

        public async Task<List<ResultadoModel>> EstadisticasPalabrasPorEdadAsync(string proyectoCodigo, string? fraganciaCodigo = null)
        {
            var lista = new List<ResultadoModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT rango_edad, palabra, total
                    FROM neuromkt.f_estadisticas_palabras_por_edad_proyecto(@p_proyecto_codigo, @p_fragancia_codigo)
                    ORDER BY rango_edad, total DESC, palabra;
                ";

                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo.Trim());
                cmd.Parameters.AddWithValue(
                    "@p_fragancia_codigo",
                    string.IsNullOrWhiteSpace(fraganciaCodigo) ? DBNull.Value : fraganciaCodigo.Trim()
                );

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ResultadoModel
                    {
                        Tipo = "palabra",
                        UsuarioEmail = reader["rango_edad"] as string ?? "Sin edad", 
                        Valor = reader["palabra"] as string ?? string.Empty,
                        Total = reader["total"] is DBNull ? 0 : Convert.ToInt32(reader["total"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }
    }
}
