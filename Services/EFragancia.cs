using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEFragancia
    {
        Task<List<FraganciaModel>> ListarFraganciasAsync();
        Task<string> CrearFraganciaAsync(FraganciaModel fragancia);
        Task ActualizarFraganciaAsync(string codigo, FraganciaModel fragancia);
        Task EliminarFraganciaAsync(string codigo);
    }

    public class EFragancia : IEFragancia
    {
        private readonly AppDbContext _db;

        public EFragancia(AppDbContext db)
        {
            _db = db;
        }

        // ======================
        // INSERTAR FRAGANCIA
        // ======================
        public async Task<string> CrearFraganciaAsync(FraganciaModel fragancia)
        {
            // Asumo que tu función se llama neuromkt.i_fragancia(...)
            const string sql = @"
                SELECT neuromkt.i_fragancia(
                    @p_nombre,
                    @p_proveedor,
                    @p_descripcion
                );";

            var pCodigo = new NpgsqlParameter("@p_codigo",
                string.IsNullOrWhiteSpace(fragancia.Codigo)
                    ? (object)DBNull.Value
                    : fragancia.Codigo.Trim());

            var pNombre = new NpgsqlParameter("@p_nombre", fragancia.Nombre?.Trim() ?? string.Empty);

            var pProveedor = new NpgsqlParameter("@p_proveedor",
                string.IsNullOrWhiteSpace(fragancia.Proveedor)
                    ? (object)DBNull.Value
                    : fragancia.Proveedor!.Trim());

            var pDescripcion = new NpgsqlParameter("@p_descripcion",
                string.IsNullOrWhiteSpace(fragancia.Descripcion)
                    ? (object)DBNull.Value
                    : fragancia.Descripcion!.Trim());

            var parametros = new[] { pCodigo, pNombre, pProveedor, pDescripcion };

            try
            {
                // La función devuelve el código generado
                var result = await _db.Database.ExecuteSqlRawAsync(sql, parametros);
                // Si quieres leer el código devuelto habría que usar FromSqlRaw/ExecuteScalar,
                // pero como la función ya genera el código y lanza excepciones, normalmente basta.
                return fragancia.Codigo; // o devolver el que te venga de la función si lo necesitas
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        // ======================
        // LISTAR FRAGANCIAS
        // ======================
        public async Task<List<FraganciaModel>> ListarFraganciasAsync()
        {
            var lista = new List<FraganciaModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT codigo, nombre, proveedor, descripcion
                    FROM neuromkt.fragancias
                    ORDER BY codigo;";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var f = new FraganciaModel
                    {
                        Codigo      = reader["codigo"]      as string ?? string.Empty,
                        Nombre      = reader["nombre"]      as string ?? string.Empty,
                        Proveedor   = reader["proveedor"]   as string,
                        Descripcion = reader["descripcion"] as string
                    };

                    lista.Add(f);
                }
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

            return lista;
        }

        // ======================
        // ACTUALIZAR FRAGANCIA
        // ======================
        public async Task ActualizarFraganciaAsync(string codigo, FraganciaModel fragancia)
        {
            const string sql = @"
                SELECT neuromkt.u_fragancia(
                    @p_codigo,
                    @p_nombre,
                    @p_proveedor,
                    @p_descripcion
                );";

            var pCodigo = new NpgsqlParameter("@p_codigo", codigo?.Trim() ?? string.Empty);

            // Siguiendo tu función PL/pgSQL:
            // - si un parámetro es NULL -> NO se toca ese campo
            // - si es '' en proveedor/descripcion -> se convertirá en NULL
            var pNombre = new NpgsqlParameter("@p_nombre",
                (object?)fragancia.Nombre ?? DBNull.Value);

            var pProveedor = new NpgsqlParameter("@p_proveedor",
                (object?)fragancia.Proveedor ?? DBNull.Value);

            var pDescripcion = new NpgsqlParameter("@p_descripcion",
                (object?)fragancia.Descripcion ?? DBNull.Value);

            var parametros = new[] { pCodigo, pNombre, pProveedor, pDescripcion };

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

        // ======================
        // ELIMINAR FRAGANCIA
        // ======================
        public async Task EliminarFraganciaAsync(string codigo)
        {
            const string sql = @"SELECT neuromkt.d_fragancia(@p_codigo);";

            var param = new NpgsqlParameter("@p_codigo", codigo?.Trim() ?? string.Empty);

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, param);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }
    }
}
